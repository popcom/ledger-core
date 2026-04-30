using Ledger.Application.Persistence;
using Ledger.Domain.Aggregates;

using MediatR;

namespace Ledger.Application.Commands.OpenAccount;

/// <summary>
/// Handler for <see cref="OpenAccountCommand"/>. Idempotency is applied
/// by the <see cref="Idempotency.IdempotencyPipelineBehavior{TRequest,TResponse}"/>
/// pipeline behavior, not here — this handler stays focused on the
/// domain logic.
/// </summary>
public sealed class OpenAccountCommandHandler
    : IRequestHandler<OpenAccountCommand, OpenAccountResult>
{
    private readonly IAggregateRepository _repository;
    private readonly TimeProvider _timeProvider;

    public OpenAccountCommandHandler(
        IAggregateRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<OpenAccountResult> Handle(
        OpenAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accountId = AccountId.New();
        var account = Account.Open(accountId, request.Owner, request.Currency);

        await _repository.SaveAccountAsync(account, cancellationToken).ConfigureAwait(false);

        return new OpenAccountResult(
            accountId.Value,
            request.Owner,
            request.Currency.Code,
            _timeProvider.GetUtcNow());
    }
}
