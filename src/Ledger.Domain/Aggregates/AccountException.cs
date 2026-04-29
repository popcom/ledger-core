namespace Ledger.Domain.Aggregates;

/// <summary>Base for all <c>Account</c> domain errors.</summary>
public abstract class AccountException : DomainException
{
    public AccountId AccountId { get; }

    protected AccountException(string code, AccountId accountId, string message)
        : base(code, message)
    {
        AccountId = accountId;
    }
}

public sealed class AccountAlreadyOpenException : AccountException
{
    public AccountAlreadyOpenException(AccountId accountId)
        : base("account.already_open", accountId,
            $"Account {accountId} is already open.")
    {
    }
}

public sealed class AccountNotOpenException : AccountException
{
    public AccountNotOpenException(AccountId accountId)
        : base("account.not_open", accountId,
            $"Account {accountId} has not been opened.")
    {
    }
}

public sealed class AccountClosedException : AccountException
{
    public AccountClosedException(AccountId accountId)
        : base("account.closed", accountId,
            $"Account {accountId} is closed; its event stream is sealed.")
    {
    }
}

public sealed class AccountFrozenException : AccountException
{
    public AccountFrozenException(AccountId accountId)
        : base("account.frozen", accountId,
            $"Account {accountId} is frozen and rejects this operation.")
    {
    }
}

public sealed class AccountNotFrozenException : AccountException
{
    public AccountNotFrozenException(AccountId accountId)
        : base("account.not_frozen", accountId,
            $"Account {accountId} is not frozen.")
    {
    }
}

public sealed class InsufficientFundsException : AccountException
{
    public InsufficientFundsException(AccountId accountId, string message)
        : base("account.insufficient_funds", accountId, message)
    {
    }
}
