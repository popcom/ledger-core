using Ledger.Application.Idempotency;
using Ledger.Application.Tenancy;
using Ledger.Domain.ValueObjects;

using Marten;

namespace Ledger.Infrastructure.Idempotency;

/// <summary>
/// Marten-backed <see cref="IIdempotencyStore"/>. Records are scoped
/// per tenant via Marten's conjoined tenancy and live for the configured
/// retention window (24h by default per the brief). Reads outside the
/// window return <see cref="StoredIdempotencyResponse"/> as <c>null</c>
/// — the handler runs again, with the same key, and writes a fresh
/// record.
/// </summary>
public sealed class MartenIdempotencyStore : IIdempotencyStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public MartenIdempotencyStore(
        IDocumentStore store,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _store = store;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<StoredIdempotencyResponse?> TryGetAsync(
        IdempotencyKey key,
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.LightweightSession(
            _tenantContext.Current.Value,
            System.Data.IsolationLevel.ReadCommitted);

        var record = await session.LoadAsync<IdempotencyRecord>(
            key.Value, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        if (record.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return null;
        }

        return new StoredIdempotencyResponse(
            record.ContentType,
            record.Body,
            record.StatusCode,
            record.StoredAt);
    }

    public async Task PutAsync(
        IdempotencyKey key,
        StoredIdempotencyResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        await using var session = _store.LightweightSession(
            _tenantContext.Current.Value,
            System.Data.IsolationLevel.ReadCommitted);

        var record = new IdempotencyRecord
        {
            Id = key.Value,
            ContentType = response.ContentType,
            Body = response.Body,
            StatusCode = response.StatusCode,
            StoredAt = response.StoredAt,
            ExpiresAt = response.StoredAt + Retention,
        };

        session.Store(record);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
