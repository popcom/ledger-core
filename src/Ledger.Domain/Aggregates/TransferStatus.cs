namespace Ledger.Domain.Aggregates;

/// <summary>
/// Lifecycle of a <c>Transfer</c>. The happy path is
/// <c>Initiated → AwaitingDebit → AwaitingCredit → Completed</c>.
/// On any step failure the saga moves to
/// <c>Compensating</c> and finally <c>Failed</c> once the debit is
/// reversed (or never happened).
/// </summary>
public enum TransferStatus
{
    Initiated = 0,
    AwaitingDebit = 1,
    AwaitingCredit = 2,
    Completed = 3,
    Compensating = 4,
    Failed = 5,
}
