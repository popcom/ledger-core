namespace Ledger.Infrastructure.Security;

/// <summary>
/// Marten document holding a tenant-scoped subject key. Conjoined
/// tenancy isolates one tenant's keys from another's; deleting the
/// document is the GDPR forget operation.
/// </summary>
public sealed class SubjectKeyDocument
{
    /// <summary>The subject id, stable per-tenant.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Raw 256-bit AES key, base64-encoded.</summary>
    public string KeyBase64 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
