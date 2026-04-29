using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    private static readonly Money TenEur = new(10m, Currency.Eur);
    private static readonly Money FiveEur = new(5m, Currency.Eur);
    private static readonly Money TenUsd = new(10m, Currency.Usd);

    [Fact]
    public void Construction_requires_a_currency()
    {
        var act = () => new Money(1m, default);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Zero_constructs_zero_amount_in_currency()
    {
        var zero = Money.Zero(Currency.Eur);
        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be(Currency.Eur);
        zero.IsZero.Should().BeTrue();
        zero.IsPositive.Should().BeFalse();
        zero.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void Add_sums_amounts_when_currencies_match()
    {
        (TenEur + FiveEur).Should().Be(new Money(15m, Currency.Eur));
        TenEur.Add(FiveEur).Should().Be(new Money(15m, Currency.Eur));
    }

    [Fact]
    public void Subtract_returns_difference_when_currencies_match()
    {
        (TenEur - FiveEur).Should().Be(new Money(5m, Currency.Eur));
        (FiveEur - TenEur).Should().Be(new Money(-5m, Currency.Eur));
    }

    [Fact]
    public void Add_throws_on_currency_mismatch()
    {
        var act = () => TenEur + TenUsd;
        act.Should().Throw<CurrencyMismatchException>()
            .Which.Should().Match<CurrencyMismatchException>(
                ex => ex.Left == Currency.Eur && ex.Right == Currency.Usd);
    }

    [Fact]
    public void Subtract_throws_on_currency_mismatch()
    {
        var act = () => TenEur - TenUsd;
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Compare_throws_on_currency_mismatch()
    {
        var act = () => TenEur.CompareTo(TenUsd);
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0.5)]
    [InlineData(-1)]
    public void Multiply_scales_amount_and_preserves_currency(decimal factor)
    {
        var result = TenEur * factor;
        result.Amount.Should().Be(10m * factor);
        result.Currency.Should().Be(Currency.Eur);

        var commutative = factor * TenEur;
        commutative.Should().Be(result);
    }

    [Fact]
    public void Divide_scales_amount_and_preserves_currency()
    {
        var result = TenEur / 4m;
        result.Amount.Should().Be(2.5m);
        result.Currency.Should().Be(Currency.Eur);
    }

    [Fact]
    public void Negate_inverts_sign_and_preserves_currency()
    {
        var negative = -TenEur;
        negative.Amount.Should().Be(-10m);
        negative.Currency.Should().Be(Currency.Eur);
        negative.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Abs_returns_non_negative_amount()
    {
        new Money(-3m, Currency.Eur).Abs().Should().Be(new Money(3m, Currency.Eur));
        new Money(3m, Currency.Eur).Abs().Should().Be(new Money(3m, Currency.Eur));
    }

    [Fact]
    public void Comparison_operators_compare_amounts_when_currencies_match()
    {
        var anotherTenEur = new Money(10m, Currency.Eur);

        (TenEur > FiveEur).Should().BeTrue();
        (FiveEur < TenEur).Should().BeTrue();
        (TenEur >= anotherTenEur).Should().BeTrue();
        (TenEur <= anotherTenEur).Should().BeTrue();
    }

    [Fact]
    public void Equality_uses_amount_and_currency()
    {
        new Money(10m, Currency.Eur).Should().Be(TenEur);
        new Money(10m, Currency.Usd).Should().NotBe(TenEur);
        new Money(10.00m, Currency.Eur).Should().Be(TenEur);
    }

    [Fact]
    public void ToString_uses_invariant_culture_and_iso_code()
    {
        new Money(1234.56m, Currency.Eur).ToString().Should().Be("1234.56 EUR");
    }

    [Fact]
    public void Decimal_precision_is_preserved()
    {
        var penny = new Money(0.01m, Currency.Eur);
        var sum = Money.Zero(Currency.Eur);
        for (var i = 0; i < 100; i++)
        {
            sum += penny;
        }
        sum.Amount.Should().Be(1.00m);
    }
}
