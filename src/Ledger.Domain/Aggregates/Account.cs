using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Aggregates;

/// <summary>
/// The <c>Account</c> aggregate. State is derived from a sequence of
/// <see cref="AccountEvent"/>s; commands validate against current state
/// and produce new events. The aggregate has no infrastructure
/// dependencies — Marten, MassTransit, etc. live in
/// <c>Ledger.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Invariants enforced here:
/// <list type="bullet">
///   <item>Available balance never goes below zero (no overdraft policy yet).</item>
///   <item>Frozen accounts reject debits, credits, and re-freezing.</item>
///   <item>Closed accounts are terminal — every command throws
///         <see cref="AccountClosedException"/>.</item>
///   <item>Opening an already-open account throws.</item>
///   <item>Money operations whose currency does not match the account's
///         throw <see cref="CurrencyMismatchException"/> via
///         <c>Money</c> arithmetic.</item>
/// </list>
/// </remarks>
public sealed class Account
{
    private readonly List<AccountEvent> _pendingEvents = [];

    public AccountId Id { get; private set; }
    public string Owner { get; private set; } = string.Empty;
    public Currency Currency { get; private set; }
    public AccountStatus Status { get; private set; }
    public Money Balance { get; private set; }
    public bool IsOpened { get; private set; }
    public int Version { get; private set; }

    /// <summary>Events produced by commands since the last
    /// <see cref="ClearPendingEvents"/>. The infrastructure layer flushes
    /// these to the event store on save.</summary>
    public IReadOnlyList<AccountEvent> PendingEvents => _pendingEvents;

    private Account()
    {
    }

    /// <summary>Open a brand-new account.</summary>
    public static Account Open(AccountId id, string owner, Currency currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var account = new Account();
        account.Apply(new AccountOpened(id, owner, currency));
        account._pendingEvents.Add(new AccountOpened(id, owner, currency));
        return account;
    }

    /// <summary>Rehydrate from a persisted event stream.</summary>
    public static Account Rehydrate(IEnumerable<AccountEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var account = new Account();
        foreach (var @event in history)
        {
            account.Apply(@event);
        }
        return account;
    }

    public void Freeze(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureOpened();
        EnsureNotClosed();
        if (Status == AccountStatus.Frozen)
        {
            return;
        }

        Raise(new AccountFrozen(Id, reason));
    }

    public void Unfreeze()
    {
        EnsureOpened();
        EnsureNotClosed();
        if (Status != AccountStatus.Frozen)
        {
            throw new AccountNotFrozenException(Id);
        }

        Raise(new AccountUnfrozen(Id));
    }

    public void Close(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureOpened();
        if (Status == AccountStatus.Closed)
        {
            return;
        }

        if (!Balance.IsZero)
        {
            throw new InsufficientFundsException(Id,
                $"Account {Id} cannot be closed with a non-zero balance ({Balance}).");
        }

        Raise(new AccountClosed(Id, reason));
    }

    public void Credit(Money amount, string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        EnsureOpened();
        EnsureNotClosed();
        EnsureNotFrozen();
        EnsurePositive(amount);
        EnsureSameCurrency(amount);

        Raise(new AccountCredited(Id, amount, reference));
    }

    public void Debit(Money amount, string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        EnsureOpened();
        EnsureNotClosed();
        EnsureNotFrozen();
        EnsurePositive(amount);
        EnsureSameCurrency(amount);

        if (Balance < amount)
        {
            throw new InsufficientFundsException(Id,
                $"Account {Id} balance {Balance} cannot cover debit {amount}.");
        }

        Raise(new AccountDebited(Id, amount, reference));
    }

    /// <summary>Clear pending events after the infrastructure layer has
    /// flushed them. Called by the unit-of-work, not by application code.</summary>
    public void ClearPendingEvents() => _pendingEvents.Clear();

    private void Raise(AccountEvent @event)
    {
        Apply(@event);
        _pendingEvents.Add(@event);
    }

    private void Apply(AccountEvent @event)
    {
        switch (@event)
        {
            case AccountOpened opened:
                if (IsOpened)
                {
                    throw new AccountAlreadyOpenException(Id);
                }
                Id = opened.AccountId;
                Owner = opened.Owner;
                Currency = opened.Currency;
                Status = AccountStatus.Active;
                Balance = Money.Zero(opened.Currency);
                IsOpened = true;
                break;

            case AccountFrozen:
                Status = AccountStatus.Frozen;
                break;

            case AccountUnfrozen:
                Status = AccountStatus.Active;
                break;

            case AccountClosed:
                Status = AccountStatus.Closed;
                break;

            case AccountCredited credited:
                Balance += credited.Amount;
                break;

            case AccountDebited debited:
                Balance -= debited.Amount;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled account event {@event.GetType().Name}.");
        }

        Version++;
    }

    private void EnsureOpened()
    {
        if (!IsOpened)
        {
            throw new AccountNotOpenException(Id);
        }
    }

    private void EnsureNotClosed()
    {
        if (Status == AccountStatus.Closed)
        {
            throw new AccountClosedException(Id);
        }
    }

    private void EnsureNotFrozen()
    {
        if (Status == AccountStatus.Frozen)
        {
            throw new AccountFrozenException(Id);
        }
    }

    private void EnsureSameCurrency(Money amount)
    {
        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(Currency, amount.Currency);
        }
    }

    private static void EnsurePositive(Money amount)
    {
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                amount, "Amount must be strictly positive.");
        }
    }
}
