using Ledger.Application.Idempotency;
using Ledger.Application.Persistence;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using MediatR;

namespace Ledger.Application.Commands.CloseAccount;

public sealed record CloseAccountCommand(
    AccountId AccountId,
    string Reason,
    IdempotencyKey IdempotencyKey) : IIdempotentRequest<Unit>;

public sealed class CloseAccountCommandHandler : IRequestHandler<CloseAccountCommand, Unit>
{
    private readonly IAggregateRepository _repository;

    public CloseAccountCommandHandler(IAggregateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _repository.LoadAccountAsync(request.AccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccountNotOpenException(request.AccountId);

        account.Close(request.Reason);

        await _repository.SaveAccountAsync(account, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
