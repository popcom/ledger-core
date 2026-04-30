using Ledger.Application.Queries;
using Ledger.Application.Tenancy;
using Ledger.Domain.Aggregates;

using Marten;

namespace Ledger.Infrastructure.Projections;

/// <summary>
/// Time-travel balance reader. Pulls the full event stream for the
/// account, filters to events that occurred at or before the
/// requested instant, then folds the prefix into an
/// <see cref="AccountBalanceDocument"/> via the same projection rules
/// the live read model uses.
/// </summary>
public sealed class MartenAccountTimelineQuery : IAccountTimelineQuery
{
    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;

    public MartenAccountTimelineQuery(IDocumentStore store, ITenantContext tenantContext)
    {
        _store = store;
        _tenantContext = tenantContext;
    }

    public async Task<AccountBalanceView?> GetAsOfAsync(
        AccountId accountId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession(_tenantContext.Current.Value);
        var events = await session.Events.FetchStreamAsync(
            accountId.Value, token: cancellationToken).ConfigureAwait(false);

        var prefix = events
            .Where(e => e.Timestamp <= asOf)
            .Select(e => e.Data)
            .OfType<AccountEvent>()
            .ToList();

        if (prefix.Count == 0)
        {
            return null;
        }

        AccountBalanceDocument? doc = null;
        foreach (var @event in prefix)
        {
            switch (@event)
            {
                case AccountOpened opened when doc is null:
                    doc = AccountBalanceProjection.Create(opened);
                    break;
                case AccountCredited credited when doc is not null:
                    AccountBalanceProjection.Apply(doc, credited);
                    break;
                case AccountDebited debited when doc is not null:
                    AccountBalanceProjection.Apply(doc, debited);
                    break;
                case AccountFrozen frozen when doc is not null:
                    AccountBalanceProjection.Apply(doc, frozen);
                    break;
                case AccountUnfrozen unfrozen when doc is not null:
                    AccountBalanceProjection.Apply(doc, unfrozen);
                    break;
                case AccountClosed closed when doc is not null:
                    AccountBalanceProjection.Apply(doc, closed);
                    break;
            }
        }

        return doc is null
            ? null
            : new AccountBalanceView(
                doc.Id, doc.Owner, doc.Currency, doc.Balance,
                doc.Status, doc.OpenedAt, doc.LastEventAt);
    }
}
