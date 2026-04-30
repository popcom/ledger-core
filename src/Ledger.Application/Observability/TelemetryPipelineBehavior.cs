using System.Diagnostics;

using MediatR;

namespace Ledger.Application.Observability;

/// <summary>
/// MediatR pipeline behavior that opens an OpenTelemetry
/// <see cref="Activity"/> per command and increments
/// <see cref="LedgerTelemetry.CommandsHandled"/> with the outcome
/// tag. Sits before validation and idempotency in the pipeline
/// registration so it sees every command, including the ones that
/// short-circuit on a cached idempotent reply.
/// </summary>
public sealed class TelemetryPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var commandName = typeof(TRequest).Name;
        using var activity = LedgerTelemetry.ActivitySource.StartActivity(
            $"command.{commandName}", ActivityKind.Internal);

        activity?.SetTag("ledger.command", commandName);

        try
        {
            var response = await next().ConfigureAwait(false);
            LedgerTelemetry.CommandsHandled.Add(1,
                new KeyValuePair<string, object?>("command", commandName),
                new KeyValuePair<string, object?>("outcome", "success"));
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            LedgerTelemetry.CommandsHandled.Add(1,
                new KeyValuePair<string, object?>("command", commandName),
                new KeyValuePair<string, object?>("outcome", "failure"));
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }
}
