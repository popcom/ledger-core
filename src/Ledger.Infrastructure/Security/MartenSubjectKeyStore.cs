using System.Security.Cryptography;

using Ledger.Application.Security;
using Ledger.Application.Tenancy;
using Ledger.Domain.ValueObjects;

using Marten;

namespace Ledger.Infrastructure.Security;

/// <summary>
/// Marten-backed <see cref="ISubjectKeyStore"/>. Keys are stored as
/// base64-encoded AES-256 secrets in a tenant-scoped Marten
/// collection. Forget deletes the row; conjoined tenancy ensures one
/// tenant cannot reach into another's key space.
/// </summary>
public sealed class MartenSubjectKeyStore : ISubjectKeyStore
{
    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public MartenSubjectKeyStore(
        IDocumentStore store,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _store = store;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<byte[]> GetOrCreateAsync(SubjectId subject, CancellationToken cancellationToken = default)
    {
        await using var session = _store.LightweightSession(
            _tenantContext.Current.Value, System.Data.IsolationLevel.ReadCommitted);

        var existing = await session.LoadAsync<SubjectKeyDocument>(subject.Value, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Convert.FromBase64String(existing.KeyBase64);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var doc = new SubjectKeyDocument
        {
            Id = subject.Value,
            KeyBase64 = Convert.ToBase64String(key),
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        session.Store(doc);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return key;
    }

    public async Task<byte[]?> TryGetAsync(SubjectId subject, CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession(_tenantContext.Current.Value);
        var doc = await session.LoadAsync<SubjectKeyDocument>(subject.Value, cancellationToken)
            .ConfigureAwait(false);
        return doc is null ? null : Convert.FromBase64String(doc.KeyBase64);
    }

    public async Task ForgetAsync(SubjectId subject, CancellationToken cancellationToken = default)
    {
        await using var session = _store.LightweightSession(
            _tenantContext.Current.Value, System.Data.IsolationLevel.ReadCommitted);
        session.Delete<SubjectKeyDocument>(subject.Value);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
