using Ledger.Application.Queries;
using Ledger.Application.Tenancy;
using Ledger.Domain.Aggregates;

using Marten;

namespace Ledger.Infrastructure.Projections;

public sealed class MartenAccountBalanceQuery : IAccountBalanceQuery
{
    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;

    public MartenAccountBalanceQuery(IDocumentStore store, ITenantContext tenantContext)
    {
        _store = store;
        _tenantContext = tenantContext;
    }

    public async Task<AccountBalanceView?> GetAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession(_tenantContext.Current.Value);
        var doc = await session.LoadAsync<AccountBalanceDocument>(
            accountId.Value, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : Map(doc);
    }

    public async Task<IReadOnlyList<AccountBalanceView>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession(_tenantContext.Current.Value);
        var docs = await session.Query<AccountBalanceDocument>()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return docs.Select(Map).ToList();
    }

    private static AccountBalanceView Map(AccountBalanceDocument doc) => new(
        doc.Id, doc.Owner, doc.Currency, doc.Balance, doc.Status, doc.OpenedAt, doc.LastEventAt);
}
