namespace Ledger.Application.Admin;

/// <summary>
/// Application port for chaos-engineering toggles. Operators flip these
/// at runtime to inject failure modes (force a transfer to fail at the
/// credit phase, induce projection lag, etc.) and verify the
/// system handles them. Off by default; never reads from
/// configuration so a flag flipped in production cannot accidentally
/// persist across a restart.
/// </summary>
public interface IChaosToggles
{
    public bool FailEveryTransferAtCredit { get; }

    public void Toggle(string name, bool enabled);

    public IReadOnlyDictionary<string, bool> Snapshot();
}
