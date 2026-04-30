namespace Ledger.Infrastructure.Outbox;

/// <summary>
/// Persistent outbox row. Stored in Postgres in the same Marten
/// transaction as the events, drained by <c>OutboxPublisher</c>.
/// The payload is the JSON-serialised
/// <c>LedgerIntegrationEvent</c> so the publisher does not need to
/// know the concrete event type at compile time.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTimeOffset EnqueuedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
