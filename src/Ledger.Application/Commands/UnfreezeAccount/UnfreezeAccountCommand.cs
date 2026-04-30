using Ledger.Application.Idempotency;
using Ledger.Application.Persistence;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using MediatR;

namespace Ledger.Application.Commands.UnfreezeAccount;

public sealed record UnfreezeAccountCommand(
    AccountId AccountId,
    IdempotencyKey IdempotencyKey) : IIdempotentRequest<Unit>;

public sealed class UnfreezeAccountCommandHandler : IRequestHandler<UnfreezeAccountCommand, Unit>
{
    private readonly IAggregateRepository _repository;

    public UnfreezeAccountCommandHandler(IAggregateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UnfreezeAccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _repository.LoadAccountAsync(request.AccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccountNotOpenException(request.AccountId);

        account.Unfreeze();

        await _repository.SaveAccountAsync(account, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
