using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Security;

/// <summary>
/// Per-tenant-per-subject key store. Encrypted PII fields stored
/// alongside events reference the key by <see cref="SubjectId"/>;
/// deleting the entry renders past PII unreadable while leaving the
/// immutable audit log intact.
/// </summary>
public interface ISubjectKeyStore
{
    /// <summary>
    /// Get the existing key bytes for the subject, or create a new
    /// one and return it. Idempotent: calling twice yields the same
    /// key while the key exists.
    /// </summary>
    public Task<byte[]> GetOrCreateAsync(SubjectId subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the key bytes for the subject. Returns <c>null</c> when
    /// the subject has been forgotten.
    /// </summary>
    public Task<byte[]?> TryGetAsync(SubjectId subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the subject's key. Past PII encrypted with this key
    /// becomes unrecoverable; the call is idempotent and safe to
    /// repeat.
    /// </summary>
    public Task ForgetAsync(SubjectId subject, CancellationToken cancellationToken = default);
}
