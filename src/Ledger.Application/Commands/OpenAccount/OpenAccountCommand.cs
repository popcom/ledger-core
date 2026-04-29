using Ledger.Application.Idempotency;
using Ledger.Domain.ValueObjects;

namespace Ledger.Application.Commands.OpenAccount;

/// <summary>
/// Command to open a new account for the current tenant. Returns the
/// new <see cref="OpenAccountResult"/>; replays via the idempotency
/// pipeline behavior return the original result without re-running.
/// </summary>
public sealed record OpenAccountCommand(
    string Owner,
    Currency Currency,
    IdempotencyKey IdempotencyKey) : IIdempotentRequest<OpenAccountResult>;

public sealed record OpenAccountResult(
    Guid AccountId,
    string Owner,
    string Currency,
    DateTimeOffset OpenedAt);
