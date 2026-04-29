namespace Ledger.Domain;

/// <summary>
/// Base type for exceptions raised by domain invariant violations. Carries
/// a stable <see cref="Code"/> so the API problem-details layer (PR #17)
/// can map domain errors to typed problem URLs without reflecting on
/// exception types.
/// </summary>
public abstract class DomainException : Exception
{
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    protected DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }
}
