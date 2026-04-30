using FluentValidation;

namespace Ledger.Application.Commands.OpenAccount;

public sealed class OpenAccountCommandValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountCommandValidator()
    {
        RuleFor(x => x.Owner).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Currency).NotEqual(default(Domain.ValueObjects.Currency))
            .WithMessage("Currency must be a valid ISO 4217 code.");
        RuleFor(x => x.IdempotencyKey).NotEqual(default(Domain.ValueObjects.IdempotencyKey))
            .WithMessage("Idempotency-Key header is required.");
    }
}
