using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Aggregates;

public abstract record TransferEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record TransferInitiated(
    TransferId TransferId,
    AccountId SourceAccountId,
    AccountId DestinationAccountId,
    Money Amount,
    string Reference) : TransferEvent;

public sealed record TransferDebitConfirmed(
    TransferId TransferId,
    AccountId SourceAccountId,
    Money Amount) : TransferEvent;

public sealed record TransferCreditConfirmed(
    TransferId TransferId,
    AccountId DestinationAccountId,
    Money Amount) : TransferEvent;

public sealed record TransferCompleted(
    TransferId TransferId) : TransferEvent;

public sealed record TransferCompensationStarted(
    TransferId TransferId,
    string Reason) : TransferEvent;

public sealed record TransferCompensationCompleted(
    TransferId TransferId) : TransferEvent;

public sealed record TransferFailed(
    TransferId TransferId,
    string Reason) : TransferEvent;
