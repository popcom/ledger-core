using Ledger.Application.Security;
using Ledger.Domain.ValueObjects;

namespace Ledger.Api.Endpoints;

internal static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/privacy").WithTags("Privacy");

        group.MapPost("/forget/{subjectId}", ForgetSubject).WithName("ForgetSubject");

        return app;
    }

    private static async Task<IResult> ForgetSubject(
        string subjectId,
        ISubjectKeyStore keyStore,
        CancellationToken cancellationToken)
    {
        var subject = SubjectId.Parse(subjectId);
        await keyStore.ForgetAsync(subject, cancellationToken).ConfigureAwait(false);
        return Results.Accepted(value: new { subjectId, status = "forgotten" });
    }
}
