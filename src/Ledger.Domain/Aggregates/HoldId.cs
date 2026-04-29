namespace Ledger.Domain.Aggregates;

/// <summary>Strongly-typed identifier for a <c>Hold</c>.</summary>
public readonly record struct HoldId
{
    public Guid Value { get; }

    public HoldId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("HoldId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static HoldId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
