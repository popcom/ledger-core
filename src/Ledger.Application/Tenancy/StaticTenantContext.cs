using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Tenancy;

/// <summary>
/// Trivial <see cref="ITenantContext"/> implementation for tests, hosted
/// services, and other call sites that already know the tenant.
/// </summary>
public sealed class StaticTenantContext : ITenantContext
{
    public StaticTenantContext(TenantId tenant)
    {
        Current = tenant;
    }

    public TenantId Current { get; }
}
