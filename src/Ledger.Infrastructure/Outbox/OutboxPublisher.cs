using Marten;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ledger.Infrastructure.Outbox;

/// <summary>
/// Hosted service that drains the outbox table. Polls at a fixed
/// interval, picks up the oldest unpublished messages, hands each to
/// the configured <see cref="IOutboxTransport"/>, and marks the row
/// published when the transport returns. Failures bump the attempts
/// counter and store the last error so the publisher backs off
/// without blocking the loop.
/// </summary>
public sealed class OutboxPublisher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    private readonly IDocumentStore _store;
    private readonly IOutboxTransport _transport;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IDocumentStore store,
        IOutboxTransport transport,
        TimeProvider timeProvider,
        ILogger<OutboxPublisher> logger)
    {
        _store = store;
        _transport = transport;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox publisher starting (pollInterval={PollInterval}, batchSize={BatchSize}).",
            PollInterval, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publisher loop encountered an unexpected error.");
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(PollInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        await using var session = _store.LightweightSession();
        var pending = await session.Query<OutboxMessage>()
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.EnqueuedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var message in pending)
        {
            try
            {
                await _transport.PublishAsync(message, cancellationToken).ConfigureAwait(false);
                message.PublishedAt = _timeProvider.GetUtcNow();
                message.LastError = null;
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                _logger.LogWarning(ex,
                    "Outbox publish failed for {EventType} {EventId}; attempt {Attempts}.",
                    message.EventType, message.Id, message.Attempts);
            }
#pragma warning restore CA1031

            session.Store(message);
        }

        if (pending.Count > 0)
        {
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
