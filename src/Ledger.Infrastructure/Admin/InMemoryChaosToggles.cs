using System.Collections.Concurrent;

using Ledger.Application.Admin;

namespace Ledger.Infrastructure.Admin;

/// <summary>
/// Singleton-scoped, process-local <see cref="IChaosToggles"/>.
/// Restarts the process, restarts the toggles. The brief asks for
/// chaos-mode admin endpoints; persisting them across restarts is
/// out of scope — and accidentally durable chaos in production is
/// the exact failure mode to avoid.
/// </summary>
public sealed class InMemoryChaosToggles : IChaosToggles
{
    public const string FailEveryTransferAtCreditKey = "fail_every_transfer_at_credit";

    private readonly ConcurrentDictionary<string, bool> _flags = new(StringComparer.Ordinal);

    public bool FailEveryTransferAtCredit =>
        _flags.TryGetValue(FailEveryTransferAtCreditKey, out var value) && value;

    public void Toggle(string name, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _flags[name] = enabled;
    }

    public IReadOnlyDictionary<string, bool> Snapshot() =>
        new Dictionary<string, bool>(_flags, StringComparer.Ordinal);
}
