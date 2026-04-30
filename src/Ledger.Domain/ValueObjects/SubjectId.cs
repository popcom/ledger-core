namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Identifier for a GDPR subject (a natural person whose PII the
/// Ledger encrypts). One subject id maps to one subject-key entry
/// per tenant; deleting the entry crypto-shreds every encrypted
/// PII field that references it.
/// </summary>
public readonly record struct SubjectId
{
    public const int MaxLength = 64;

    public string Value { get; }

    private SubjectId(string value)
    {
        Value = value;
    }

    public static SubjectId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"SubjectId '{value}' exceeds the {MaxLength} character limit.",
                nameof(value));
        }
        return new SubjectId(trimmed);
    }

    public override string ToString() => Value;
}
