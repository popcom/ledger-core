using Ledger.Application.Admin;
using Ledger.Infrastructure.Projections;

using Marten;

namespace Ledger.Infrastructure.Admin;

/// <summary>
/// Marten-backed <see cref="IProjectionAdmin"/>. Today only the
/// inline <c>account_balances</c> projection lives here; rebuild
/// drops and re-runs it from the start of the events stream via
/// Marten's daemon. Adding a new projection is one new entry in the
/// known list.
/// </summary>
public sealed class MartenProjectionAdmin : IProjectionAdmin
{
    private static readonly IReadOnlyList<string> Known = new[]
    {
        nameof(AccountBalanceProjection),
    };

    private readonly IDocumentStore _store;

    public MartenProjectionAdmin(IDocumentStore store)
    {
        _store = store;
    }

    public IReadOnlyList<string> ListProjections() => Known;

    public async Task RebuildAsync(string projectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        if (!Known.Contains(projectionName, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Projection '{projectionName}' is not known to the admin.",
                nameof(projectionName));
        }

        using var daemon = await _store.BuildProjectionDaemonAsync().ConfigureAwait(false);
        await daemon.RebuildProjectionAsync(projectionName, cancellationToken).ConfigureAwait(false);
    }
}
