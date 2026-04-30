using FluentValidation.Results;

using Ledger.Domain;

namespace Ledger.Application.Validation;

/// <summary>
/// Application-level wrapper around <see cref="FluentValidation"/>
/// failures so the API layer can map them through the same
/// problem-details path used by domain errors.
/// </summary>
public sealed class CommandValidationException : DomainException
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public CommandValidationException(IEnumerable<ValidationFailure> failures)
        : base("validation.failed", BuildMessage(failures))
    {
        Failures = failures.ToList();
    }

    private static string BuildMessage(IEnumerable<ValidationFailure> failures)
    {
        var combined = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
        return string.IsNullOrEmpty(combined)
            ? "Command validation failed."
            : $"Command validation failed: {combined}";
    }
}
