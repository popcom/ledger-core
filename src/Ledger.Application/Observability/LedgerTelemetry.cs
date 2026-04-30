using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ledger.Application.Observability;

/// <summary>
/// Single source of truth for the OpenTelemetry Activity and Meter
/// names the Ledger module exports. Hosts opt in via OpenTelemetry
/// SDK builders that subscribe to <see cref="ActivitySourceName"/> and
/// <see cref="MeterName"/>; the rest of the codebase only ever creates
/// activities and counters through these instances.
/// </summary>
public static class LedgerTelemetry
{
    public const string ActivitySourceName = "Ledger";
    public const string MeterName = "Ledger";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    /// <summary>Counter for command attempts, tagged with command name and outcome.</summary>
    public static readonly Counter<long> CommandsHandled =
        Meter.CreateCounter<long>(
            "ledger.commands_handled",
            unit: "{command}",
            description: "Number of MediatR commands handled, tagged by command name and outcome.");

    /// <summary>Counter for transfer outcomes (Completed, Failed).</summary>
    public static readonly Counter<long> TransfersByOutcome =
        Meter.CreateCounter<long>(
            "ledger.transfers_by_outcome",
            unit: "{transfer}",
            description: "Transfers grouped by terminal status.");

    /// <summary>Counter for outbox publish attempts.</summary>
    public static readonly Counter<long> OutboxPublishes =
        Meter.CreateCounter<long>(
            "ledger.outbox_publishes",
            unit: "{message}",
            description: "Outbox publish attempts, tagged by outcome.");
}
