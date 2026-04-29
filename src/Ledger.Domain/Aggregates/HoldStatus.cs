namespace Ledger.Domain.Aggregates;

/// <summary>
/// Lifecycle of a <c>Hold</c>. The three terminal states (Captured,
/// Released, Expired) are mutually exclusive — once a hold reaches one
/// of them it stays there.
/// </summary>
public enum HoldStatus
{
    Active = 0,
    Captured = 1,
    Released = 2,
    Expired = 3,
}
