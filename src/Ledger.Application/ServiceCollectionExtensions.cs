using System.Reflection;

using FluentValidation;

using Ledger.Application.Idempotency;
using Ledger.Application.Validation;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Application;

/// <summary>
/// Composition root for the Ledger Application layer. Registers
/// MediatR with the validation and idempotency pipeline behaviors and
/// every <see cref="FluentValidation.IValidator{T}"/> defined in this
/// assembly. Infrastructure-side ports (
/// <see cref="Persistence.IAggregateRepository"/>,
/// <see cref="Idempotency.IIdempotencyStore"/>) must be registered by
/// the host's infrastructure composition root.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(ServiceCollectionExtensions).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Pipeline order: telemetry first so it sees every command
        // including idempotent replays, validation second (cheap and
        // deterministic), idempotency last (the handler-skipping
        // database round-trip).
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(Observability.TelemetryPipelineBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(IdempotencyPipelineBehavior<,>));

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
