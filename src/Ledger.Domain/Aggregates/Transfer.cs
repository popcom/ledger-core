using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Aggregates;

/// <summary>
/// The <c>Transfer</c> aggregate. Models the saga state machine the
/// brief mandates: <c>Initiated → AwaitingDebit → AwaitingCredit →
/// Completed</c> on the happy path; <c>Compensating → Failed</c> on
/// any step failure.
/// </summary>
/// <remarks>
/// Transfer is a process aggregate, not a money-mover. It records the
/// saga state and emits events; the actual debit and credit happen on
/// the Account aggregates and are confirmed back to the Transfer via
/// <see cref="ConfirmDebit"/> and <see cref="ConfirmCredit"/>. The
/// process manager landing in PR #13 wires those calls together.
/// </remarks>
public sealed class Transfer
{
    private readonly List<TransferEvent> _pendingEvents = [];

    public TransferId Id { get; private set; }
    public AccountId SourceAccountId { get; private set; }
    public AccountId DestinationAccountId { get; private set; }
    public Money Amount { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public TransferStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public bool IsInitiated { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyList<TransferEvent> PendingEvents => _pendingEvents;

    private Transfer()
    {
    }

    public static Transfer Initiate(
        TransferId id,
        AccountId source,
        AccountId destination,
        Money amount,
        string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                amount, "Transfer amount must be strictly positive.");
        }
        if (source == destination)
        {
            throw new TransferSameAccountException(id);
        }

        var transfer = new Transfer();
        transfer.Raise(new TransferInitiated(id, source, destination, amount, reference));
        return transfer;
    }

    public static Transfer Rehydrate(IEnumerable<TransferEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var transfer = new Transfer();
        foreach (var @event in history)
        {
            transfer.Apply(@event);
        }
        return transfer;
    }

    public void ConfirmDebit()
    {
        EnsureInitiated();
        if (Status != TransferStatus.AwaitingDebit)
        {
            throw new TransferInvalidStateException(Id, Status, "confirm debit");
        }

        Raise(new TransferDebitConfirmed(Id, SourceAccountId, Amount));
    }

    public void ConfirmCredit()
    {
        EnsureInitiated();
        if (Status != TransferStatus.AwaitingCredit)
        {
            throw new TransferInvalidStateException(Id, Status, "confirm credit");
        }

        Raise(new TransferCreditConfirmed(Id, DestinationAccountId, Amount));
        Raise(new TransferCompleted(Id));
    }

    /// <summary>
    /// Mark the transfer as failing. If the debit has already been
    /// applied, the saga must compensate before marking the transfer
    /// failed; otherwise the transfer transitions directly to
    /// <see cref="TransferStatus.Failed"/>.
    /// </summary>
    public void Fail(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureInitiated();

        switch (Status)
        {
            case TransferStatus.AwaitingDebit:
                Raise(new TransferFailed(Id, reason));
                break;

            case TransferStatus.AwaitingCredit:
                Raise(new TransferCompensationStarted(Id, reason));
                break;

            case TransferStatus.Compensating:
                throw new TransferInvalidStateException(Id, Status, "fail (already compensating)");

            case TransferStatus.Completed:
            case TransferStatus.Failed:
                throw new TransferInvalidStateException(Id, Status, "fail (terminal)");

            default:
                throw new TransferInvalidStateException(Id, Status, "fail");
        }
    }

    public void CompleteCompensation()
    {
        EnsureInitiated();
        if (Status != TransferStatus.Compensating)
        {
            throw new TransferInvalidStateException(Id, Status, "complete compensation");
        }

        Raise(new TransferCompensationCompleted(Id));
        Raise(new TransferFailed(Id, FailureReason ?? "compensation completed"));
    }

    public void ClearPendingEvents() => _pendingEvents.Clear();

    private void Raise(TransferEvent @event)
    {
        Apply(@event);
        _pendingEvents.Add(@event);
    }

    private void Apply(TransferEvent @event)
    {
        switch (@event)
        {
            case TransferInitiated initiated:
                Id = initiated.TransferId;
                SourceAccountId = initiated.SourceAccountId;
                DestinationAccountId = initiated.DestinationAccountId;
                Amount = initiated.Amount;
                Reference = initiated.Reference;
                Status = TransferStatus.AwaitingDebit;
                IsInitiated = true;
                break;

            case TransferDebitConfirmed:
                Status = TransferStatus.AwaitingCredit;
                break;

            case TransferCreditConfirmed:
                // Credit confirmed; awaiting Completed event next.
                break;

            case TransferCompleted:
                Status = TransferStatus.Completed;
                break;

            case TransferCompensationStarted started:
                Status = TransferStatus.Compensating;
                FailureReason = started.Reason;
                break;

            case TransferCompensationCompleted:
                // Compensation done; awaiting Failed event.
                break;

            case TransferFailed failed:
                Status = TransferStatus.Failed;
                FailureReason = failed.Reason;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled transfer event {@event.GetType().Name}.");
        }

        Version++;
    }

    private void EnsureInitiated()
    {
        if (!IsInitiated)
        {
            throw new TransferNotInitiatedException(Id);
        }
    }
}
