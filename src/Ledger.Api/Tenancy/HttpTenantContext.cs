using Ledger.Application.Tenancy;
using Ledger.Domain.ValueObjects;

namespace Ledger.Api.Tenancy;

/// <summary>
/// Per-request <see cref="ITenantContext"/> that resolves the tenant
/// from the <c>X-Tenant-Id</c> header on the current
/// <see cref="HttpContext"/>. Registered scoped; one resolved tenant
/// per request.
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    public const string HeaderName = "X-Tenant-Id";

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "ITenantContext requested outside an HTTP request scope.");

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || values.Count == 0
            || !TenantId.TryParse(values[0], out var tenant))
        {
            throw new MissingTenantHeaderException();
        }

        Current = tenant;
    }

    public TenantId Current { get; }
}

/// <summary>
/// Thrown when an HTTP request does not carry a valid
/// <c>X-Tenant-Id</c> header. Mapped to a 400 by the problem-details
/// pipeline (PR #17); for now the global exception handler returns 400
/// with a stable error code.
/// </summary>
public sealed class MissingTenantHeaderException : Exception
{
    public MissingTenantHeaderException()
        : base($"Request is missing a valid '{HttpTenantContext.HeaderName}' header.")
    {
    }
}
