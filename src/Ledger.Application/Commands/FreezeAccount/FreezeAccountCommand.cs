using Ledger.Application.Idempotency;
using Ledger.Application.Persistence;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using MediatR;

namespace Ledger.Application.Commands.FreezeAccount;

public sealed record FreezeAccountCommand(
    AccountId AccountId,
    string Reason,
    IdempotencyKey IdempotencyKey) : IIdempotentRequest<Unit>;

public sealed class FreezeAccountCommandHandler : IRequestHandler<FreezeAccountCommand, Unit>
{
    private readonly IAggregateRepository _repository;

    public FreezeAccountCommandHandler(IAggregateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(FreezeAccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _repository.LoadAccountAsync(request.AccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccountNotOpenException(request.AccountId);

        account.Freeze(request.Reason);

        await _repository.SaveAccountAsync(account, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
