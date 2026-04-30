namespace Ledger.Domain.Aggregates;

public readonly record struct TransferId
{
    public Guid Value { get; }

    public TransferId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("TransferId cannot be empty.", nameof(value));
        }
        Value = value;
    }

    public static TransferId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
