using Ledger.Application.Persistence;
using Ledger.Domain;
using Ledger.Domain.Aggregates;

using MediatR;

namespace Ledger.Application.Commands.InitiateTransfer;

/// <summary>
/// Process manager for <see cref="InitiateTransferCommand"/>. Builds
/// the <see cref="Transfer"/> aggregate, debits the source, credits
/// the destination, and confirms each step back to the Transfer.
/// On any domain failure mid-saga, runs compensation: if the debit
/// has already been applied, refund it back to the source.
/// </summary>
public sealed class InitiateTransferSaga
    : IRequestHandler<InitiateTransferCommand, InitiateTransferResult>
{
    private readonly IAggregateRepository _repository;

    public InitiateTransferSaga(IAggregateRepository repository)
    {
        _repository = repository;
    }

    public async Task<InitiateTransferResult> Handle(
        InitiateTransferCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transferId = TransferId.New();
        var transfer = Transfer.Initiate(
            transferId,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Reference);

        await _repository.SaveTransferAsync(transfer, cancellationToken).ConfigureAwait(false);

        try
        {
            await DebitSourceAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            transfer.Fail(ex.Message);
            await _repository.SaveTransferAsync(transfer, cancellationToken).ConfigureAwait(false);
            return Result(transferId, transfer);
        }

        transfer.ConfirmDebit();
        await _repository.SaveTransferAsync(transfer, cancellationToken).ConfigureAwait(false);

        try
        {
            await CreditDestinationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            transfer.Fail(ex.Message);
            await _repository.SaveTransferAsync(transfer, cancellationToken).ConfigureAwait(false);

            await CompensateAsync(request, cancellationToken).ConfigureAwait(false);

            transfer.CompleteCompensation();
            await _repository.SaveTransferAsync(transfer, cancellationToken).ConfigureAwait(false);
            return Result(transferId, transfer);
        }

        transfer.ConfirmCredit();
        await _repository.SaveTransferAsync(transfer, cancellationToken).ConfigureAwait(false);

        return Result(transferId, transfer);
    }

    private async Task DebitSourceAsync(
        InitiateTransferCommand request,
        CancellationToken cancellationToken)
    {
        var source = await _repository.LoadAccountAsync(
            request.SourceAccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccountNotOpenException(request.SourceAccountId);

        source.Debit(request.Amount, request.Reference);
        await _repository.SaveAccountAsync(source, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreditDestinationAsync(
        InitiateTransferCommand request,
        CancellationToken cancellationToken)
    {
        var destination = await _repository.LoadAccountAsync(
            request.DestinationAccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccountNotOpenException(request.DestinationAccountId);

        destination.Credit(request.Amount, request.Reference);
        await _repository.SaveAccountAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task CompensateAsync(
        InitiateTransferCommand request,
        CancellationToken cancellationToken)
    {
        var source = await _repository.LoadAccountAsync(
            request.SourceAccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccountNotOpenException(request.SourceAccountId);

        source.Credit(request.Amount, $"compensation:{request.Reference}");
        await _repository.SaveAccountAsync(source, cancellationToken).ConfigureAwait(false);
    }

    private static InitiateTransferResult Result(TransferId id, Transfer transfer) =>
        new(id.Value, transfer.Status.ToString(), transfer.FailureReason);
}
