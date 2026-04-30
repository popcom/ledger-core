using System.Text.Json;

using Ledger.Application.Outbox;
using Ledger.Application.Tenancy;
using Ledger.Contracts.IntegrationEvents;

using Marten;

namespace Ledger.Infrastructure.Outbox;

/// <summary>
/// Marten-backed <see cref="IOutbox"/>. Writes an
/// <see cref="OutboxMessage"/> document in the current tenant's scope.
/// Marten conjoined tenancy gives us the tenant filter for free; the
/// publisher reads the same column to fan out per-tenant if it ever
/// needs to.
/// </summary>
public sealed class MartenOutbox : IOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public MartenOutbox(
        IDocumentStore store,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _store = store;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task EnqueueAsync(
        LedgerIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var tenantId = _tenantContext.Current.Value;
        var stamped = integrationEvent with { TenantId = tenantId };

        var message = new OutboxMessage
        {
            Id = stamped.EventId,
            EventType = integrationEvent.GetType().FullName ?? integrationEvent.GetType().Name,
            Payload = JsonSerializer.Serialize<object>(stamped, Json),
            TenantId = tenantId,
            EnqueuedAt = _timeProvider.GetUtcNow(),
            Attempts = 0,
        };

        await using var session = _store.LightweightSession(
            tenantId, System.Data.IsolationLevel.ReadCommitted);
        session.Store(message);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
