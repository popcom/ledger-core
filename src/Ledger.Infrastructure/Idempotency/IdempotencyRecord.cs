namespace Ledger.Infrastructure.Idempotency;

/// <summary>
/// Marten document used to persist idempotency replays. Lives in the
/// Infrastructure layer so the Application layer can stay free of
/// document-store types; the Application port talks in
/// <c>StoredIdempotencyResponse</c>.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>The idempotency key value, scoped per tenant by Marten conjoined tenancy.</summary>
    public string Id { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTimeOffset StoredAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
