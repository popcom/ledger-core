namespace Ledger.Infrastructure;

/// <summary>
/// Configuration for the Ledger infrastructure layer. Exposed as
/// <c>IOptions&lt;LedgerInfrastructureOptions&gt;</c>; bind from the
/// <c>Ledger</c> configuration section in host code.
/// </summary>
public sealed class LedgerInfrastructureOptions
{
    /// <summary>Postgres connection string used for the event store and projections.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Marten database schema. Defaults to <c>ledger</c>.</summary>
    public string DatabaseSchema { get; set; } = "ledger";

    /// <summary>Whether to apply all schema changes at startup. Set true in dev/test only.</summary>
    public bool AutoCreateSchema { get; set; }
}
