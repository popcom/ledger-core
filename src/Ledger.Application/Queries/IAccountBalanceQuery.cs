using Ledger.Domain.Aggregates;

namespace Ledger.Application.Queries;

/// <summary>
/// Application port for reading the <c>account_balances</c> projection.
/// Lives here so command handlers and API endpoints can fetch a view
/// without depending on Marten directly.
/// </summary>
public interface IAccountBalanceQuery
{
    public Task<AccountBalanceView?> GetAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AccountBalanceView>> ListAsync(
        CancellationToken cancellationToken = default);
}
