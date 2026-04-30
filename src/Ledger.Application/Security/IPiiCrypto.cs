using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Security;

/// <summary>
/// Encrypts and decrypts PII fields with a per-subject key. The
/// returned ciphertext envelope is opaque and self-describing — it
/// carries the IV and authentication tag so storage does not need
/// to track them.
/// </summary>
public interface IPiiCrypto
{
    public Task<string> EncryptAsync(
        SubjectId subject,
        string plaintext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypt previously-encrypted ciphertext for the subject.
    /// Returns <c>null</c> when the subject's key has been forgotten;
    /// the immutable audit log keeps the row, but the PII field is
    /// rendered unreadable.
    /// </summary>
    public Task<string?> DecryptAsync(
        SubjectId subject,
        string ciphertext,
        CancellationToken cancellationToken = default);
}
