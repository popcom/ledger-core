using Ledger.Domain.Aggregates;

namespace Ledger.Application.Persistence;

/// <summary>
/// Application port for loading and saving aggregates against the event
/// store. The Application layer talks to this; the Infrastructure layer
/// (Marten) implements it. Keeping the contract small and aggregate-
/// specific avoids leaking persistence concerns into command handlers.
/// </summary>
public interface IAggregateRepository
{
    public Task<Account?> LoadAccountAsync(AccountId id, CancellationToken cancellationToken = default);

    public Task SaveAccountAsync(Account account, CancellationToken cancellationToken = default);

    public Task<Hold?> LoadHoldAsync(HoldId id, CancellationToken cancellationToken = default);

    public Task SaveHoldAsync(Hold hold, CancellationToken cancellationToken = default);

    public Task<Transfer?> LoadTransferAsync(TransferId id, CancellationToken cancellationToken = default);

    public Task SaveTransferAsync(Transfer transfer, CancellationToken cancellationToken = default);
}
