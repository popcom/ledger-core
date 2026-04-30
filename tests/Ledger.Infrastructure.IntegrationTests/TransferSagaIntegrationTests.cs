using JasperFx;

using Ledger.Application.Commands.InitiateTransfer;
using Ledger.Application.Outbox;
using Ledger.Application.Tenancy;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;
using Ledger.Infrastructure;
using Ledger.Infrastructure.Outbox;
using Ledger.Infrastructure.Persistence;
using Ledger.Infrastructure.Projections;

using Marten;

using Testcontainers.PostgreSql;

namespace Ledger.Infrastructure.IntegrationTests;

/// <summary>
/// End-to-end saga tests against real Postgres via Testcontainers.
/// Covers the brief's two named integration scenarios — happy path
/// and compensation — plus the outbox transactional guarantee.
/// Tagged Category=Integration so the default CI lane skips them.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TransferSagaIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ledger")
        .WithUsername("ledger")
        .WithPassword("ledger")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private DocumentStore BuildStore()
    {
        var options = ServiceCollectionExtensions.BuildStoreOptions(
            new LedgerInfrastructureOptions
            {
                ConnectionString = _postgres.GetConnectionString(),
                DatabaseSchema = "ledger",
                AutoCreateSchema = true,
            });
        options.AutoCreateSchemaObjects = AutoCreate.All;
        return new DocumentStore(options);
    }

    private static (DocumentStore store, MartenAggregateRepository repo, MartenOutbox outbox, ITenantContext tenant)
        BuildHarness(DocumentStore store, string tenantId)
    {
        var tenant = new StaticTenantContext(TenantId.Parse(tenantId));
        var repo = new MartenAggregateRepository(store, tenant);
        var outbox = new MartenOutbox(store, tenant, TimeProvider.System);
        return (store, repo, outbox, tenant);
    }

    [Fact]
    public async Task Happy_path_moves_balance_and_completes_transfer()
    {
        await using var store = BuildStore();
        var (_, repo, outbox, tenant) = BuildHarness(store, "acme");

        var source = Account.Open(AccountId.New(), "src", Currency.Eur);
        source.Credit(new Money(100m, Currency.Eur), "seed");
        var destination = Account.Open(AccountId.New(), "dst", Currency.Eur);

        await repo.SaveAccountAsync(source);
        await repo.SaveAccountAsync(destination);

        var saga = new InitiateTransferSaga(repo, outbox);
        var result = await saga.Handle(
            new InitiateTransferCommand(
                source.Id, destination.Id,
                new Money(40m, Currency.Eur),
                "rent",
                IdempotencyKey.Parse("transfer-it-1")),
            CancellationToken.None);

        result.Status.Should().Be(nameof(TransferStatus.Completed));

        var query = new MartenAccountBalanceQuery(store, tenant);
        var srcView = await query.GetAsync(source.Id);
        var dstView = await query.GetAsync(destination.Id);

        srcView!.Balance.Should().Be(60m);
        dstView!.Balance.Should().Be(40m);

        await using var session = store.QuerySession(tenant.Current.Value);
        var outboxRows = await session.Query<OutboxMessage>().ToListAsync();
        outboxRows.Should().ContainSingle(
            "the saga should enqueue exactly one TransferCompletedIntegrationEvent");
    }

    [Fact]
    public async Task Compensation_refunds_source_when_destination_is_frozen()
    {
        await using var store = BuildStore();
        var (_, repo, outbox, tenant) = BuildHarness(store, "acme");

        var source = Account.Open(AccountId.New(), "src", Currency.Eur);
        source.Credit(new Money(100m, Currency.Eur), "seed");
        var destination = Account.Open(AccountId.New(), "dst", Currency.Eur);
        destination.Freeze("compliance");

        await repo.SaveAccountAsync(source);
        await repo.SaveAccountAsync(destination);

        var saga = new InitiateTransferSaga(repo, outbox);
        var result = await saga.Handle(
            new InitiateTransferCommand(
                source.Id, destination.Id,
                new Money(40m, Currency.Eur),
                "rent",
                IdempotencyKey.Parse("transfer-it-2")),
            CancellationToken.None);

        result.Status.Should().Be(nameof(TransferStatus.Failed));

        var query = new MartenAccountBalanceQuery(store, tenant);
        var srcView = await query.GetAsync(source.Id);

        srcView!.Balance.Should().Be(100m,
            "compensation refunds the source so the balance ends at the seed amount");
    }
}
