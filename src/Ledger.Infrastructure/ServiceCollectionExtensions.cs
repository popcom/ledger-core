using JasperFx;
using JasperFx.Events.Projections;
using Ledger.Application.Idempotency;
using Ledger.Application.Outbox;
using Ledger.Application.Persistence;
using Ledger.Application.Queries;
using Ledger.Domain.Aggregates;
using Ledger.Infrastructure.Idempotency;
using Ledger.Infrastructure.Outbox;
using Ledger.Infrastructure.Persistence;
using Ledger.Infrastructure.Projections;
using Marten;
using Marten.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ledger.Infrastructure;

/// <summary>
/// Composition root for the Ledger infrastructure layer. Wires Marten
/// with conjoined tenancy, registers the Ledger event types so the
/// store can deserialise streams without runtime reflection on every
/// load, and exposes <see cref="IAggregateRepository"/> as the single
/// persistence port the Application layer talks to.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerInfrastructure(
        this IServiceCollection services,
        Action<LedgerInfrastructureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<LedgerInfrastructureOptions>()
            .Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Ledger:ConnectionString must be set.")
            .ValidateOnStart();

        services.AddMarten(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LedgerInfrastructureOptions>>().Value;
            return BuildStoreOptions(options);
        });

        services.AddScoped<IAggregateRepository, MartenAggregateRepository>();
        services.AddScoped<IIdempotencyStore, MartenIdempotencyStore>();
        services.AddScoped<IAccountBalanceQuery, MartenAccountBalanceQuery>();
        services.AddScoped<IOutbox, MartenOutbox>();

        services.TryAddSingleton<IOutboxTransport, LoggingOutboxTransport>();
        services.AddHostedService<OutboxPublisher>();

        return services;
    }

    /// <summary>
    /// Build a <see cref="StoreOptions"/> instance independently of the
    /// service container. Useful for integration tests that need a
    /// store without going through DI.
    /// </summary>
    public static StoreOptions BuildStoreOptions(LedgerInfrastructureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException(
                "ConnectionString must be set.", nameof(options));
        }

        var storeOptions = new StoreOptions();
        storeOptions.Connection(options.ConnectionString);
        storeOptions.DatabaseSchemaName = options.DatabaseSchema;
        storeOptions.Events.DatabaseSchemaName = options.DatabaseSchema;

        storeOptions.Events.TenancyStyle = TenancyStyle.Conjoined;
        storeOptions.Policies.AllDocumentsAreMultiTenanted();

        storeOptions.AutoCreateSchemaObjects = options.AutoCreateSchema
            ? AutoCreate.CreateOrUpdate
            : AutoCreate.None;

        RegisterEventTypes(storeOptions);
        storeOptions.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);

        return storeOptions;
    }

    private static void RegisterEventTypes(StoreOptions options)
    {
        options.Events.AddEventType<AccountOpened>();
        options.Events.AddEventType<AccountFrozen>();
        options.Events.AddEventType<AccountUnfrozen>();
        options.Events.AddEventType<AccountClosed>();
        options.Events.AddEventType<AccountCredited>();
        options.Events.AddEventType<AccountDebited>();

        options.Events.AddEventType<HoldPlaced>();
        options.Events.AddEventType<HoldCaptured>();
        options.Events.AddEventType<HoldReleased>();
        options.Events.AddEventType<HoldExpired>();

        options.Events.AddEventType<TransferInitiated>();
        options.Events.AddEventType<TransferDebitConfirmed>();
        options.Events.AddEventType<TransferCreditConfirmed>();
        options.Events.AddEventType<TransferCompleted>();
        options.Events.AddEventType<TransferCompensationStarted>();
        options.Events.AddEventType<TransferCompensationCompleted>();
        options.Events.AddEventType<TransferFailed>();
    }
}
