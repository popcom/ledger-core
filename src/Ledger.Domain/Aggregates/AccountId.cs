namespace Ledger.Domain.Aggregates;

/// <summary>
/// Strongly-typed identifier for an <c>Account</c>. Wrapping <see cref="Guid"/>
/// instead of using it directly keeps account ids from being passed to APIs
/// that expect a different kind of id (e.g. <c>TransferId</c>, <c>HoldId</c>).
/// </summary>
public readonly record struct AccountId
{
    public Guid Value { get; }

    public AccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("AccountId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static AccountId New() => new(Guid.NewGuid());

    public static AccountId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new AccountId(Guid.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    public override string ToString() => Value.ToString();

    public string ToString(IFormatProvider? formatProvider) =>
        Value.ToString("D", formatProvider ?? System.Globalization.CultureInfo.InvariantCulture);
}
