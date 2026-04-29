namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Caller-supplied key that tells the Ledger "this command and any
/// retry of it produce the same outcome". Stored alongside the response
/// payload for 24 hours; replays return the original payload verbatim
/// rather than re-running the command.
/// </summary>
public readonly record struct IdempotencyKey
{
    public const int MaxLength = 128;

    public string Value { get; }

    private IdempotencyKey(string value)
    {
        Value = value;
    }

    public static IdempotencyKey Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"IdempotencyKey '{value}' exceeds the {MaxLength} character limit.",
                nameof(value));
        }

        foreach (var c in trimmed)
        {
            var allowed = char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == ':' || c == '.';
            if (!allowed)
            {
                throw new ArgumentException(
                    $"IdempotencyKey may only contain alphanumeric, '-', '_', ':', and '.' characters; got '{value}'.",
                    nameof(value));
            }
        }

        return new IdempotencyKey(trimmed);
    }

    public static bool TryParse(string? value, out IdempotencyKey key)
    {
        try
        {
            key = Parse(value!);
            return true;
        }
        catch (ArgumentException)
        {
            key = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
