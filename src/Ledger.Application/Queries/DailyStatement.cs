namespace Ledger.Application.Queries;

/// <summary>
/// One row in a daily statement: opening/closing balance for an
/// account on a given UTC date plus the gross credits and debits
/// observed in that window.
/// </summary>
public sealed record DailyStatementEntry(
    Guid AccountId,
    DateOnly Date,
    string Currency,
    decimal OpeningBalance,
    decimal Credits,
    decimal Debits,
    decimal ClosingBalance,
    int Movements);

/// <summary>Application port for reading the daily statement.</summary>
public interface IDailyStatementQuery
{
    public Task<IReadOnlyList<DailyStatementEntry>> ForAccountAsync(
        Guid accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
