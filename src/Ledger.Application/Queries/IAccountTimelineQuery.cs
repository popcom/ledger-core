using Ledger.Domain.Aggregates;

namespace Ledger.Application.Queries;

/// <summary>
/// Application port for time-travel balance reads. Folds the
/// <see cref="Account"/> stream up to (and including) the supplied
/// timestamp and returns the resulting view. Returns <c>null</c> if
/// the account did not yet exist at the requested moment.
/// </summary>
public interface IAccountTimelineQuery
{
    public Task<AccountBalanceView?> GetAsOfAsync(
        AccountId accountId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);
}
