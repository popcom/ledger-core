namespace Ledger.Infrastructure.Projections;

/// <summary>
/// Marten document that backs the <c>account_balances</c> projection.
/// Mutable so the single-stream projection can update it in place;
/// callers exit with a read-only <c>AccountBalanceView</c>.
/// </summary>
public sealed class AccountBalanceDocument
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset LastEventAt { get; set; }
}
