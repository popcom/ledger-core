using Ledger.Domain;

namespace Ledger.Application.Admin;

/// <summary>
/// Synthetic failure raised when an operator-driven chaos toggle is
/// active. Subclasses <see cref="DomainException"/> so the existing
/// problem-details mapping treats it the same as any other 409 —
/// chaos events should be indistinguishable from the real ones they
/// simulate.
/// </summary>
public sealed class ChaosInducedFailureException : DomainException
{
    public ChaosInducedFailureException(string message)
        : base("chaos.induced", message)
    {
    }
}
