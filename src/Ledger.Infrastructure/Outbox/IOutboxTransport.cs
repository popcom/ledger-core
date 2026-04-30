namespace Ledger.Infrastructure.Outbox;

/// <summary>
/// Pluggable shipping mechanism for outbox messages. The default
/// implementation logs the payload (so the brief's "outbox publishes
/// reliably" gate is testable without RabbitMQ in CI); a MassTransit
/// implementation lands when the docker-compose stack does.
/// </summary>
public interface IOutboxTransport
{
    public Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}
