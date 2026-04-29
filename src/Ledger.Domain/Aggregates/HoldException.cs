namespace Ledger.Domain.Aggregates;

public abstract class HoldException : DomainException
{
    public HoldId HoldId { get; }

    protected HoldException(string code, HoldId holdId, string message) : base(code, message)
    {
        HoldId = holdId;
    }
}

public sealed class HoldNotPlacedException : HoldException
{
    public HoldNotPlacedException(HoldId holdId)
        : base("hold.not_placed", holdId, $"Hold {holdId} has not been placed.")
    {
    }
}

public sealed class HoldAlreadyTerminalException : HoldException
{
    public HoldStatus Status { get; }

    public HoldAlreadyTerminalException(HoldId holdId, HoldStatus status)
        : base("hold.already_terminal", holdId,
            $"Hold {holdId} has already reached terminal state {status}.")
    {
        Status = status;
    }
}

public sealed class HoldExpiredException : HoldException
{
    public HoldExpiredException(HoldId holdId, DateTimeOffset expiredAt)
        : base("hold.expired", holdId,
            $"Hold {holdId} expired at {expiredAt:O} and can no longer be captured.")
    {
    }
}
