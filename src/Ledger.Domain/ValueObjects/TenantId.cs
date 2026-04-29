namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Identifier for a Ledger tenant. Tenancy is header-driven at the API
/// edge (<c>X-Tenant-Id</c>) and propagated into Marten sessions, the
/// outbox, and projections via <c>ITenantContext</c>. The string value
/// is canonicalised to lower-case so casing of the header does not
/// produce parallel tenant scopes.
/// </summary>
public readonly record struct TenantId
{
    public const int MaxLength = 64;

    public string Value { get; }

    private TenantId(string value)
    {
        Value = value;
    }

    public static TenantId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var canonical = value.Trim().ToLowerInvariant();

        if (canonical.Length > MaxLength)
        {
            throw new ArgumentException(
                $"TenantId '{value}' exceeds the {MaxLength} character limit.",
                nameof(value));
        }

        foreach (var c in canonical)
        {
            var allowed = char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_';
            if (!allowed)
            {
                throw new ArgumentException(
                    $"TenantId may only contain alphanumeric, '-', and '_' characters; got '{value}'.",
                    nameof(value));
            }
        }

        return new TenantId(canonical);
    }

    public static bool TryParse(string? value, out TenantId tenantId)
    {
        try
        {
            tenantId = Parse(value!);
            return true;
        }
        catch (ArgumentException)
        {
            tenantId = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
