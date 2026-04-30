using Ledger.Api.Endpoints;
using Ledger.Api.Observability;
using Ledger.Api.ProblemDetails;
using Ledger.Api.Tenancy;
using Ledger.Application;
using Ledger.Application.Tenancy;
using Ledger.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddLedgerObservability();
builder.AddLedgerProblemDetails();

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

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseLedgerProblemDetails();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAccountEndpoints();
app.MapTransferEndpoints();
app.MapAdminEndpoints();

await app.RunAsync().ConfigureAwait(false);
