using Ledger.Contracts.IntegrationEvents;

namespace Ledger.Application.Outbox;

/// <summary>
/// Application port for enqueuing integration events into the outbox.
/// Command handlers call this <em>inside</em> the same logical
/// operation as their aggregate save; the outbox row commits with the
/// domain events, and a separate hosted publisher drains the row and
/// pushes it to the message bus. Either both land or neither does.
/// </summary>
public interface IOutbox
{
    public Task EnqueueAsync(
        LedgerIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
