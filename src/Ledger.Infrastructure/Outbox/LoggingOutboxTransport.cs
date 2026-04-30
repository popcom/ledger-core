using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Outbox;

/// <summary>
/// Stand-in transport that logs the payload at Information level.
/// Lets the outbox publisher run end-to-end on the default CI lane
/// without RabbitMQ in scope; replaced by a MassTransit transport
/// alongside the docker-compose stack in PR #18.
/// </summary>
public sealed class LoggingOutboxTransport : IOutboxTransport
{
    private readonly ILogger<LoggingOutboxTransport> _logger;

    public LoggingOutboxTransport(ILogger<LoggingOutboxTransport> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "Outbox publish: tenant={TenantId} type={EventType} id={EventId}",
            message.TenantId, message.EventType, message.Id);

        return Task.CompletedTask;
    }
}
