namespace Ledger.Domain.ValueObjects;

/// <summary>
/// Thrown when an arithmetic or comparison operation is attempted between
/// <see cref="Money"/> values of different currencies. The Ledger never
/// performs implicit FX; mixing currencies is a programming error.
/// </summary>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    public Currency Left { get; }
    public Currency Right { get; }

    public CurrencyMismatchException(Currency left, Currency right)
        : base($"Cannot operate on Money values with different currencies: {left} vs {right}.")
    {
        Left = left;
        Right = right;
    }
}
