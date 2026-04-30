namespace Ledger.Application.Admin;

/// <summary>
/// Application port the admin endpoints call to drive Marten's
/// projection daemon: list known projections, rebuild one by name.
/// Implementations live in Infrastructure because they need
/// Marten's daemon API.
/// </summary>
public interface IProjectionAdmin
{
    public IReadOnlyList<string> ListProjections();

    public Task RebuildAsync(string projectionName, CancellationToken cancellationToken = default);
}
