var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync().ConfigureAwait(false);

namespace Ledger.Api
{
    /// <summary>
    /// Marker type used by <c>WebApplicationFactory</c> in integration tests.
    /// </summary>
    public partial class Program;
}
