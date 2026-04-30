using Ledger.Application.Admin;

namespace Ledger.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin").WithTags("Admin");

        group.MapGet("/projections", ListProjections).WithName("ListProjections");
        group.MapPost("/projections/{name}/rebuild", RebuildProjection).WithName("RebuildProjection");

        group.MapGet("/chaos", ListChaos).WithName("ListChaos");
        group.MapPost("/chaos/{name}", SetChaos).WithName("SetChaos");

        return app;
    }

    private static IResult ListProjections(IProjectionAdmin admin)
    {
        return Results.Ok(new { projections = admin.ListProjections() });
    }

    private static async Task<IResult> RebuildProjection(
        string name,
        IProjectionAdmin admin,
        CancellationToken cancellationToken)
    {
        await admin.RebuildAsync(name, cancellationToken).ConfigureAwait(false);
        return Results.Accepted(value: new { projection = name, status = "rebuilt" });
    }

    private static IResult ListChaos(IChaosToggles chaos)
    {
        return Results.Ok(chaos.Snapshot());
    }

    public sealed record ChaosRequest(bool Enabled);

    private static IResult SetChaos(string name, ChaosRequest body, IChaosToggles chaos)
    {
        ArgumentNullException.ThrowIfNull(body);
        chaos.Toggle(name, body.Enabled);
        return Results.Ok(new { name, enabled = body.Enabled });
    }
}
