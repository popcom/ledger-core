using FluentValidation;

using Ledger.Api.Endpoints;
using Ledger.Api.Tenancy;
using Ledger.Application;
using Ledger.Application.Tenancy;
using Ledger.Application.Validation;
using Ledger.Domain;
using Ledger.Domain.Aggregates;
using Ledger.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddLedgerApplication();
builder.Services.AddLedgerInfrastructure(opts =>
{
    opts.ConnectionString = builder.Configuration.GetConnectionString("LedgerDb")
        ?? builder.Configuration["Ledger:ConnectionString"]
        ?? string.Empty;
    opts.DatabaseSchema = builder.Configuration["Ledger:DatabaseSchema"] ?? "ledger";
    opts.AutoCreateSchema = builder.Environment.IsDevelopment();
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var exception = feature?.Error;

    int status;
    string code;
    string detail;

    switch (exception)
    {
        case MissingTenantHeaderException missingTenant:
            status = StatusCodes.Status400BadRequest;
            code = "tenant.header_missing";
            detail = missingTenant.Message;
            break;
        case MissingIdempotencyKeyException missingKey:
            status = StatusCodes.Status400BadRequest;
            code = "idempotency.key_missing";
            detail = missingKey.Message;
            break;
        case CommandValidationException validation:
            status = StatusCodes.Status400BadRequest;
            code = validation.Code;
            detail = validation.Message;
            break;
        case AccountNotOpenException:
            status = StatusCodes.Status404NotFound;
            code = "account.not_open";
            detail = exception.Message;
            break;
        case DomainException domain:
            status = StatusCodes.Status409Conflict;
            code = domain.Code;
            detail = domain.Message;
            break;
        case ArgumentException arg:
            status = StatusCodes.Status400BadRequest;
            code = "argument.invalid";
            detail = arg.Message;
            break;
        default:
            status = StatusCodes.Status500InternalServerError;
            code = "server.unexpected";
            detail = "An unexpected error occurred.";
            break;
    }

    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new
    {
        type = "about:blank",
        title = code,
        status,
        detail,
    }).ConfigureAwait(false);
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAccountEndpoints();

await app.RunAsync().ConfigureAwait(false);
