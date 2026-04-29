using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Aggregates;

/// <summary>
/// Marker for the account event family. Sealed so the closed set of
/// concrete events is exhaustive — projections, snapshots, and tests can
/// switch without a default arm and the compiler will flag a new event
/// that has not been handled.
/// </summary>
public abstract record AccountEvent
{
    /// <summary>UTC timestamp when the event was recorded.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AccountOpened(
    AccountId AccountId,
    string Owner,
    Currency Currency) : AccountEvent;

public sealed record AccountFrozen(
    AccountId AccountId,
    string Reason) : AccountEvent;

public sealed record AccountUnfrozen(
    AccountId AccountId) : AccountEvent;

public sealed record AccountClosed(
    AccountId AccountId,
    string Reason) : AccountEvent;

public sealed record AccountCredited(
    AccountId AccountId,
    Money Amount,
    string Reference) : AccountEvent;

public sealed record AccountDebited(
    AccountId AccountId,
    Money Amount,
    string Reference) : AccountEvent;
