using Ledger.Application.Persistence;
using Ledger.Application.Tenancy;
using Ledger.Domain.Aggregates;

using Marten;

namespace Ledger.Infrastructure.Persistence;

/// <summary>
/// Marten-backed implementation of <see cref="IAggregateRepository"/>.
/// Sessions are opened tenant-scoped — every read and write carries the
/// current <see cref="ITenantContext"/>'s tenant id and Marten enforces
/// the conjoined-tenancy filter on every projection and event stream.
/// </summary>
public sealed class MartenAggregateRepository : IAggregateRepository
{
    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;

    public MartenAggregateRepository(IDocumentStore store, ITenantContext tenantContext)
    {
        _store = store;
        _tenantContext = tenantContext;
    }

    public async Task<Account?> LoadAccountAsync(
        AccountId id,
        CancellationToken cancellationToken = default)
    {
        await using var session = OpenSession();
        var events = await session.Events.FetchStreamAsync(
            id.Value, token: cancellationToken).ConfigureAwait(false);

        if (events.Count == 0)
        {
            return null;
        }

        return Account.Rehydrate(events.Select(e => (AccountEvent)e.Data));
    }

    public async Task SaveAccountAsync(
        Account account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.PendingEvents.Count == 0)
        {
            return;
        }

        await using var session = OpenSession();
        var events = account.PendingEvents.Cast<object>().ToArray();

        if (account.Version == account.PendingEvents.Count)
        {
            session.Events.StartStream<Account>(account.Id.Value, events);
        }
        else
        {
            await session.Events.AppendOptimistic(
                account.Id.Value, events).ConfigureAwait(false);
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        account.ClearPendingEvents();
    }

    public async Task<Hold?> LoadHoldAsync(
        HoldId id,
        CancellationToken cancellationToken = default)
    {
        await using var session = OpenSession();
        var events = await session.Events.FetchStreamAsync(
            id.Value, token: cancellationToken).ConfigureAwait(false);

        if (events.Count == 0)
        {
            return null;
        }

        return Hold.Rehydrate(events.Select(e => (HoldEvent)e.Data));
    }

    public async Task SaveHoldAsync(
        Hold hold,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hold);
        if (hold.PendingEvents.Count == 0)
        {
            return;
        }

        await using var session = OpenSession();
        var events = hold.PendingEvents.Cast<object>().ToArray();

        if (hold.Version == hold.PendingEvents.Count)
        {
            session.Events.StartStream<Hold>(hold.Id.Value, events);
        }
        else
        {
            await session.Events.AppendOptimistic(
                hold.Id.Value, events).ConfigureAwait(false);
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        hold.ClearPendingEvents();
    }

    private IDocumentSession OpenSession() =>
        _store.LightweightSession(
            _tenantContext.Current.Value,
            System.Data.IsolationLevel.ReadCommitted);
}
