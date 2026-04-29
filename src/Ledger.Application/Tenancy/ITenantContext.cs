using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Tenancy;

/// <summary>
/// Resolves the tenant scope for the current operation. The Api layer
/// resolves it from the <c>X-Tenant-Id</c> request header; background
/// jobs and tests provide it explicitly. Marten sessions, projections,
/// and the outbox all key on this value.
/// </summary>
public interface ITenantContext
{
    public TenantId Current { get; }
}
