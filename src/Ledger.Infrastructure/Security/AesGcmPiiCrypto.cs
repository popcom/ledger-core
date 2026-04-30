using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Ledger.Application.Security;
using Ledger.Domain.ValueObjects;

namespace Ledger.Infrastructure.Security;

/// <summary>
/// AES-GCM-256 PII crypto. Each call generates a 96-bit nonce, packs
/// (version | nonce | ciphertext | tag) into a single byte array, and
/// returns the result base64-encoded. Decrypt unpacks the same shape.
/// </summary>
/// <remarks>
/// Envelope layout:
///   byte[0]      = version (currently 1)
///   byte[1..12]  = 12-byte nonce
///   byte[13..n]  = ciphertext
///   byte[n..n+16]= 16-byte tag
/// Storing the nonce inline keeps key rotation simple — a per-row
/// nonce allows the same key to encrypt many fields safely. The
/// version byte gives us room to swap algorithms in a future
/// migration without re-encrypting every event.
/// </remarks>
public sealed class AesGcmPiiCrypto : IPiiCrypto
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly ISubjectKeyStore _keyStore;

    public AesGcmPiiCrypto(ISubjectKeyStore keyStore)
    {
        _keyStore = keyStore;
    }

    public async Task<string> EncryptAsync(
        SubjectId subject,
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var key = await _keyStore.GetOrCreateAsync(subject, cancellationToken).ConfigureAwait(false);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[bytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, bytes, ciphertext, tag);

        var envelope = new byte[1 + NonceSize + ciphertext.Length + TagSize];
        envelope[0] = Version;
        nonce.CopyTo(envelope.AsSpan(1));
        ciphertext.CopyTo(envelope.AsSpan(1 + NonceSize));
        tag.CopyTo(envelope.AsSpan(1 + NonceSize + ciphertext.Length));

        return Convert.ToBase64String(envelope);
    }

    public async Task<string?> DecryptAsync(
        SubjectId subject,
        string ciphertext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        var key = await _keyStore.TryGetAsync(subject, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            return null;
        }

        var envelope = Convert.FromBase64String(ciphertext);
        if (envelope.Length < 1 + NonceSize + TagSize)
        {
            throw new CryptographicException("PII envelope is too short.");
        }
        if (envelope[0] != Version)
        {
            throw new CryptographicException(
                $"Unknown PII envelope version {envelope[0]}.");
        }

        var nonce = envelope.AsSpan(1, NonceSize);
        var cipherLength = envelope.Length - 1 - NonceSize - TagSize;
        var cipher = envelope.AsSpan(1 + NonceSize, cipherLength);
        var tag = envelope.AsSpan(1 + NonceSize + cipherLength, TagSize);
        var plain = new byte[cipherLength];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
