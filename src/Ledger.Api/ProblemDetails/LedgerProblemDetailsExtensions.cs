using Ledger.Api.Endpoints;
using Ledger.Api.Tenancy;
using Ledger.Application.Validation;
using Ledger.Domain;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ledger.Api.ProblemDetails;

/// <summary>
/// Maps the Ledger's domain exception family to RFC 7807 problem
/// responses. Stable <c>type</c> URLs (relative to the Ledger
/// docs) and stable <c>title</c> codes give clients a contract to
/// branch on without parsing free-text messages.
/// </summary>
internal static class LedgerProblemDetailsExtensions
{
    private const string TypeBase = "https://ledger.popcom.dev/problems/";

    public static WebApplicationBuilder AddLedgerProblemDetails(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Extensions["traceId"] =
                    System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                ctx.ProblemDetails.Extensions["instance"] = ctx.HttpContext.Request.Path.Value;
            };
        });

        return builder;
    }

    public static IApplicationBuilder UseLedgerProblemDetails(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            var exception = feature?.Error;
            var problem = Map(exception);

            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            problem.Extensions["traceId"] =
                System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
            problem.Extensions["instance"] = context.Request.Path.Value;

            await context.Response.WriteAsJsonAsync(problem,
                problem.GetType(),
                options: null,
                contentType: "application/problem+json").ConfigureAwait(false);
        }));

        return app;
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Map(Exception? exception)
    {
        return exception switch
        {
            MissingTenantHeaderException ex => Build(
                "tenant.header_missing", StatusCodes.Status400BadRequest, ex.Message),

            MissingIdempotencyKeyException ex => Build(
                "idempotency.key_missing", StatusCodes.Status400BadRequest, ex.Message),

            CommandValidationException ex => BuildValidation(ex),

            CurrencyMismatchException ex => Build(
                "money.currency_mismatch", StatusCodes.Status409Conflict, ex.Message),

            AccountNotOpenException ex => Build(
                ex.Code, StatusCodes.Status404NotFound, ex.Message),

            HoldNotPlacedException ex => Build(
                ex.Code, StatusCodes.Status404NotFound, ex.Message),

            TransferNotInitiatedException ex => Build(
                ex.Code, StatusCodes.Status404NotFound, ex.Message),

            DomainException ex => Build(
                ex.Code, StatusCodes.Status409Conflict, ex.Message),

            ArgumentException ex => Build(
                "argument.invalid", StatusCodes.Status400BadRequest, ex.Message),

            _ => Build(
                "server.unexpected",
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred."),
        };
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Build(string code, int status, string detail)
    {
        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = TypeBase + code,
            Title = code,
            Status = status,
            Detail = detail,
        };
    }

    private static ValidationProblemDetails BuildValidation(CommandValidationException exception)
    {
        var errors = exception.Failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return new ValidationProblemDetails(errors)
        {
            Type = TypeBase + exception.Code,
            Title = exception.Code,
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
        };
    }
}
