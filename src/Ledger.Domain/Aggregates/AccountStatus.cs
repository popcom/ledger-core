namespace Ledger.Domain.Aggregates;

/// <summary>
/// Lifecycle state of an <c>Account</c>. Transitions are
/// <c>Active &lt;-&gt; Frozen</c> and either of those to <c>Closed</c>.
/// <c>Closed</c> is terminal; the brief seals the event stream.
/// </summary>
public enum AccountStatus
{
    Active = 0,
    Frozen = 1,
    Closed = 2,
}
