using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Idempotency;

/// <summary>
/// Application port for the idempotency cache. Replays of the same
/// command (same tenant + same idempotency key) return the original
/// payload verbatim rather than re-running the handler. Implementations
/// must be tenant-scoped — keys are unique per tenant, never globally.
/// </summary>
public interface IIdempotencyStore
{
    public Task<StoredIdempotencyResponse?> TryGetAsync(
        IdempotencyKey key,
        CancellationToken cancellationToken = default);

    public Task PutAsync(
        IdempotencyKey key,
        StoredIdempotencyResponse response,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Serialised payload of a previously-handled command's response.
/// </summary>
/// <param name="ContentType">MIME type of <see cref="Body"/>.</param>
/// <param name="Body">Serialised response body.</param>
/// <param name="StatusCode">HTTP-style status the original response carried.</param>
/// <param name="StoredAt">When the response was first stored.</param>
public sealed record StoredIdempotencyResponse(
    string ContentType,
    string Body,
    int StatusCode,
    DateTimeOffset StoredAt);
