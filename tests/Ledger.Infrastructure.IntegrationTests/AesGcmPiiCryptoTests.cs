using Ledger.Application.Security;
using Ledger.Domain.ValueObjects;
using Ledger.Infrastructure.Security;

using NSubstitute;

namespace Ledger.Infrastructure.IntegrationTests;

/// <summary>
/// Unit tests for the crypto envelope. Co-located with the
/// implementation rather than in the Domain test project to avoid
/// pulling Infrastructure into Domain. These do not need Marten
/// or a database — the key store is stubbed.
/// </summary>
public sealed class AesGcmPiiCryptoTests
{
    private static readonly SubjectId Subject = SubjectId.Parse("user-123");

    [Fact]
    public async Task Encrypt_then_decrypt_roundtrips_the_plaintext()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);

        var store = Substitute.For<ISubjectKeyStore>();
        store.GetOrCreateAsync(Subject, Arg.Any<CancellationToken>()).Returns(key);
        store.TryGetAsync(Subject, Arg.Any<CancellationToken>()).Returns(key);

        var crypto = new AesGcmPiiCrypto(store);

        var ciphertext = await crypto.EncryptAsync(Subject, "alice@example.com");
        var roundTrip = await crypto.DecryptAsync(Subject, ciphertext);

        roundTrip.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task Decrypt_returns_null_when_subject_key_was_forgotten()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);

        var store = Substitute.For<ISubjectKeyStore>();
        store.GetOrCreateAsync(Subject, Arg.Any<CancellationToken>()).Returns(key);
        store.TryGetAsync(Subject, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var crypto = new AesGcmPiiCrypto(store);
        var ciphertext = await crypto.EncryptAsync(Subject, "alice@example.com");

        var roundTrip = await crypto.DecryptAsync(Subject, ciphertext);

        roundTrip.Should().BeNull(
            "the key has been forgotten; decryption MUST surface as null, not throw");
    }

    [Fact]
    public async Task Successive_encryptions_produce_distinct_envelopes_due_to_random_nonce()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);

        var store = Substitute.For<ISubjectKeyStore>();
        store.GetOrCreateAsync(Subject, Arg.Any<CancellationToken>()).Returns(key);

        var crypto = new AesGcmPiiCrypto(store);

        var first = await crypto.EncryptAsync(Subject, "secret");
        var second = await crypto.EncryptAsync(Subject, "secret");

        first.Should().NotBe(second);
    }
}
