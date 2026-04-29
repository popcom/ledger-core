using System.Globalization;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// ISO 4217 currency code. The code is canonicalised to upper case at
/// construction; equality is case-insensitive only at parse time. Inside
/// the domain two <see cref="Currency"/> values are equal if and only if
/// their canonical codes match.
/// </summary>
public readonly record struct Currency
{
    /// <summary>The canonical ISO 4217 alphabetic code, e.g. <c>EUR</c>.</summary>
    public string Code { get; }

    private Currency(string code)
    {
        Code = code;
    }

    public static Currency Parse(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var canonical = code.Trim().ToUpperInvariant();
        if (canonical.Length != 3 || !canonical.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException(
                $"Currency code must be a three-letter ISO 4217 alphabetic code; got '{code}'.",
                nameof(code));
        }

        return new Currency(canonical);
    }

    public static bool TryParse(string? code, out Currency currency)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var canonical = code.Trim().ToUpperInvariant();
            if (canonical.Length == 3 && canonical.All(char.IsAsciiLetterUpper))
            {
                currency = new Currency(canonical);
                return true;
            }
        }

        currency = default;
        return false;
    }

    public override string ToString() => Code;

    // Common currencies used in tests and samples. The list is intentionally
    // small; production code does not branch on these values, it parses
    // whatever the caller supplies.
    public static Currency Eur { get; } = Parse("EUR");
    public static Currency Gbp { get; } = Parse("GBP");
    public static Currency Usd { get; } = Parse("USD");
    public static Currency Chf { get; } = Parse("CHF");

    public string ToString(IFormatProvider? formatProvider) =>
        Code.ToString(formatProvider ?? CultureInfo.InvariantCulture);
}
