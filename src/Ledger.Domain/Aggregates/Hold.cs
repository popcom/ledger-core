using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Aggregates;

/// <summary>
/// A temporary reservation against an <c>Account</c>'s available
/// balance. A <c>Hold</c> reduces the funds an account can spend until
/// it is captured (the funds become a real debit), released (the
/// reservation is dropped), or expires.
/// </summary>
/// <remarks>
/// The Hold aggregate is intentionally agnostic about how Account
/// computes available balance. Whichever projection answers
/// <c>availableBalance(accountId)</c> reads the active holds on that
/// account and subtracts them from the actual balance. Keeping that
/// projection separate lets us evolve the read model (admin views,
/// pending-transactions APIs) without changing the Hold aggregate.
/// </remarks>
public sealed class Hold
{
    private readonly List<HoldEvent> _pendingEvents = [];

    public HoldId Id { get; private set; }
    public AccountId AccountId { get; private set; }
    public Money Amount { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public HoldStatus Status { get; private set; }
    public bool IsPlaced { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyList<HoldEvent> PendingEvents => _pendingEvents;

    private Hold()
    {
    }

    public static Hold Place(
        HoldId id,
        AccountId accountId,
        Money amount,
        DateTimeOffset expiresAt,
        string reference,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                amount, "Hold amount must be strictly positive.");
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt),
                expiresAt, "Hold expiry must be in the future.");
        }

        var hold = new Hold();
        hold.Raise(new HoldPlaced(id, accountId, amount, expiresAt, reference));
        return hold;
    }

    public static Hold Rehydrate(IEnumerable<HoldEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var hold = new Hold();
        foreach (var @event in history)
        {
            hold.Apply(@event);
        }
        return hold;
    }

    public void Capture(TimeProvider? timeProvider = null)
    {
        EnsurePlaced();
        EnsureActive();

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (ExpiresAt <= now)
        {
            throw new HoldExpiredException(Id, ExpiresAt);
        }

        Raise(new HoldCaptured(Id, AccountId, Amount));
    }

    public void Release(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsurePlaced();
        EnsureActive();

        Raise(new HoldReleased(Id, AccountId, reason));
    }

    public void Expire(TimeProvider? timeProvider = null)
    {
        EnsurePlaced();
        EnsureActive();

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (ExpiresAt > now)
        {
            throw new InvalidOperationException(
                $"Hold {Id} has not yet reached its expiry ({ExpiresAt:O}).");
        }

        Raise(new HoldExpired(Id, AccountId));
    }

    public bool IsExpired(TimeProvider? timeProvider = null)
    {
        if (!IsPlaced)
        {
            return false;
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return Status == HoldStatus.Active && ExpiresAt <= now;
    }

    public void ClearPendingEvents() => _pendingEvents.Clear();

    private void Raise(HoldEvent @event)
    {
        Apply(@event);
        _pendingEvents.Add(@event);
    }

    private void Apply(HoldEvent @event)
    {
        switch (@event)
        {
            case HoldPlaced placed:
                Id = placed.HoldId;
                AccountId = placed.AccountId;
                Amount = placed.Amount;
                ExpiresAt = placed.ExpiresAt;
                Reference = placed.Reference;
                Status = HoldStatus.Active;
                IsPlaced = true;
                break;

            case HoldCaptured:
                Status = HoldStatus.Captured;
                break;

            case HoldReleased:
                Status = HoldStatus.Released;
                break;

            case HoldExpired:
                Status = HoldStatus.Expired;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled hold event {@event.GetType().Name}.");
        }

        Version++;
    }

    private void EnsurePlaced()
    {
        if (!IsPlaced)
        {
            throw new HoldNotPlacedException(Id);
        }
    }

    private void EnsureActive()
    {
        if (Status != HoldStatus.Active)
        {
            throw new HoldAlreadyTerminalException(Id, Status);
        }
    }
}
