using System.Globalization;

namespace Ledger.Domain.ValueObjects;

/// <summary>
/// A monetary amount in a specific currency. <see cref="Amount"/> is a
/// <see cref="decimal"/> to avoid the rounding errors of binary floating
/// point; the Ledger never stores or computes monetary values as
/// <see cref="double"/> or <see cref="float"/>.
/// </summary>
/// <remarks>
/// Operators throw <see cref="CurrencyMismatchException"/> on currency
/// mismatch — silent FX conversion is a class of bug we refuse to ship.
/// Consumers that need to convert between currencies must do so
/// explicitly through an FX service before reaching the domain.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (currency == default)
        {
            throw new ArgumentException("Currency must be set.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public bool IsZero => Amount == 0m;
    public bool IsPositive => Amount > 0m;
    public bool IsNegative => Amount < 0m;

    public Money Negate() => new(-Amount, Currency);
    public Money Abs() => Amount < 0m ? Negate() : this;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);
    public Money Divide(decimal divisor) => new(Amount / divisor, Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator -(Money value) => value.Negate();
    public static Money operator *(Money left, decimal right) => left.Multiply(right);
    public static Money operator *(decimal left, Money right) => right.Multiply(left);
    public static Money operator /(Money left, decimal right) => left.Divide(right);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new CurrencyMismatchException(Currency, other.Currency);
        }
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency}");

    public string ToString(IFormatProvider? formatProvider) =>
        string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{Amount} {Currency}");
}
