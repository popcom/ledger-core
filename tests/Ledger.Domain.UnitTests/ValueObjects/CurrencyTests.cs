using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.UnitTests.ValueObjects;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("EUR", "EUR")]
    [InlineData("eur", "EUR")]
    [InlineData(" gbp ", "GBP")]
    [InlineData("Usd", "USD")]
    public void Parse_normalises_to_upper_case_iso_code(string input, string expected)
    {
        Currency.Parse(input).Code.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("123")]
    public void Parse_rejects_non_iso_codes(string input)
    {
        var act = () => Currency.Parse(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_rejects_null()
    {
        var act = () => Currency.Parse(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_returns_false_for_invalid_codes()
    {
        Currency.TryParse("EU", out _).Should().BeFalse();
        Currency.TryParse(null, out _).Should().BeFalse();
        Currency.TryParse("", out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_returns_true_and_currency_for_valid_codes()
    {
        Currency.TryParse("eur", out var eur).Should().BeTrue();
        eur.Code.Should().Be("EUR");
    }

    [Fact]
    public void Equality_is_by_canonical_code()
    {
        Currency.Parse("eur").Should().Be(Currency.Parse("EUR"));
        Currency.Eur.Should().Be(Currency.Parse("EUR"));
        Currency.Eur.Should().NotBe(Currency.Usd);
    }

    [Fact]
    public void ToString_returns_canonical_code()
    {
        Currency.Eur.ToString().Should().Be("EUR");
    }
}
