namespace Ledger.Contracts.IntegrationEvents;

/// <summary>
/// Public contract for integration events the Ledger module publishes
/// to the rest of the world. These shapes are stable across module
/// boundaries and across services after extraction; do not bind them
/// to internal aggregate types.
/// </summary>
public abstract record LedgerIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string TenantId { get; init; } = string.Empty;
}

public sealed record AccountOpenedIntegrationEvent : LedgerIntegrationEvent
{
    public Guid AccountId { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
}

public sealed record TransferCompletedIntegrationEvent : LedgerIntegrationEvent
{
    public Guid TransferId { get; init; }
    public Guid SourceAccountId { get; init; }
    public Guid DestinationAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

public sealed record TransferFailedIntegrationEvent : LedgerIntegrationEvent
{
    public Guid TransferId { get; init; }
    public Guid SourceAccountId { get; init; }
    public Guid DestinationAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string FailureReason { get; init; } = string.Empty;
}
