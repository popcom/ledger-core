using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Tenancy;

/// <summary>
/// AsyncLocal-backed <see cref="ITenantContext"/> for non-request flows
/// (background jobs, hosted services, integration tests). The Api layer
/// resolves tenants from the request header into a per-request scoped
/// context; this class is the fallback for paths that are not running
/// inside an HTTP request.
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantId?> Ambient = new();

    public TenantId Current =>
        Ambient.Value
        ?? throw new InvalidOperationException(
            "No ambient tenant has been set. Call AmbientTenantContext.With(tenant) first.");

    /// <summary>
    /// Set the ambient tenant for the duration of the returned scope.
    /// Disposing the scope restores the previous tenant (if any).
    /// </summary>
    public static IDisposable With(TenantId tenant)
    {
        var previous = Ambient.Value;
        Ambient.Value = tenant;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly TenantId? _previous;
        private bool _disposed;

        public Scope(TenantId? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            Ambient.Value = _previous;
            _disposed = true;
        }
    }
}
