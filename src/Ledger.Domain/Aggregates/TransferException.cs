namespace Ledger.Domain.Aggregates;

public abstract class TransferException : DomainException
{
    public TransferId TransferId { get; }

    protected TransferException(string code, TransferId transferId, string message)
        : base(code, message)
    {
        TransferId = transferId;
    }
}

public sealed class TransferNotInitiatedException : TransferException
{
    public TransferNotInitiatedException(TransferId id)
        : base("transfer.not_initiated", id, $"Transfer {id} has not been initiated.")
    {
    }
}

public sealed class TransferInvalidStateException : TransferException
{
    public TransferStatus Current { get; }

    public TransferInvalidStateException(TransferId id, TransferStatus current, string operation)
        : base("transfer.invalid_state", id,
            $"Transfer {id} is in state {current}; cannot {operation}.")
    {
        Current = current;
    }
}

public sealed class TransferSameAccountException : TransferException
{
    public TransferSameAccountException(TransferId id)
        : base("transfer.same_account", id,
            $"Transfer {id} cannot route source and destination to the same account.")
    {
    }
}
