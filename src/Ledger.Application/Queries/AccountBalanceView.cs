namespace Ledger.Application.Queries;

/// <summary>
/// Read model for an account's current balance plus enough metadata
/// for the API to render an account view without re-folding the
/// stream. Fed by an inline Marten projection so reads after a write
/// see the new balance within the same transaction.
/// </summary>
/// <param name="AccountId">Aggregate id, stable across the account's lifetime.</param>
/// <param name="Owner">Account owner — copied from <c>AccountOpened</c> at projection time.</param>
/// <param name="Currency">ISO 4217 code of the account's currency.</param>
/// <param name="Balance">Current balance, in the account's currency.</param>
/// <param name="Status">Lifecycle state (Active/Frozen/Closed) as a string.</param>
/// <param name="OpenedAt">UTC instant the account opened.</param>
/// <param name="LastEventAt">UTC instant of the latest event folded into this view.</param>
public sealed record AccountBalanceView(
    Guid AccountId,
    string Owner,
    string Currency,
    decimal Balance,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastEventAt);
