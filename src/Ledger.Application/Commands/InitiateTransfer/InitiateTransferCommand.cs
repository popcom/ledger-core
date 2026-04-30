using Ledger.Application.Idempotency;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Commands.InitiateTransfer;

/// <summary>
/// Command to start a transfer between two accounts in the same
/// tenant. The handler is a saga: it debits the source, credits the
/// destination, and walks the <see cref="Transfer"/> state machine
/// through to <see cref="TransferStatus.Completed"/> or
/// <see cref="TransferStatus.Failed"/>.
/// </summary>
public sealed record InitiateTransferCommand(
    AccountId SourceAccountId,
    AccountId DestinationAccountId,
    Money Amount,
    string Reference,
    IdempotencyKey IdempotencyKey) : IIdempotentRequest<InitiateTransferResult>;

public sealed record InitiateTransferResult(
    Guid TransferId,
    string Status,
    string? FailureReason);
