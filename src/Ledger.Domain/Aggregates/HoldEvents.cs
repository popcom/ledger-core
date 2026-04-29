using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.Aggregates;

public abstract record HoldEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record HoldPlaced(
    HoldId HoldId,
    AccountId AccountId,
    Money Amount,
    DateTimeOffset ExpiresAt,
    string Reference) : HoldEvent;

public sealed record HoldCaptured(
    HoldId HoldId,
    AccountId AccountId,
    Money Amount) : HoldEvent;

public sealed record HoldReleased(
    HoldId HoldId,
    AccountId AccountId,
    string Reason) : HoldEvent;

public sealed record HoldExpired(
    HoldId HoldId,
    AccountId AccountId) : HoldEvent;
