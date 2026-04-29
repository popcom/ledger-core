using JasperFx;

using Ledger.Application.Persistence;
using Ledger.Application.Tenancy;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;
using Ledger.Infrastructure;
using Ledger.Infrastructure.Persistence;

using Marten;

using Testcontainers.PostgreSql;

namespace Ledger.Infrastructure.IntegrationTests;

/// <summary>
/// Smoke tests for the Marten wiring. Uses Testcontainers Postgres
/// rather than mocks because the value of this layer is precisely the
/// thing a mock cannot prove: that events round-trip through real
/// JSONB columns under conjoined tenancy. Tagged
/// <c>Category=Integration</c> so the default CI filter skips them
/// until a dedicated integration job lands; running them locally only
/// requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MartenWiringTests : IAsyncLifetime
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

        // Force schema creation on first use.
        options.AutoCreateSchemaObjects = AutoCreate.All;

        return new DocumentStore(options);
    }

    [Fact]
    public async Task Account_round_trips_through_marten_with_tenant_scope()
    {
        await using var store = BuildStore();
        var tenantContext = new StaticTenantContext(TenantId.Parse("acme"));
        var repo = new MartenAggregateRepository(store, tenantContext);

        var accountId = AccountId.New();
        var account = Account.Open(accountId, "Mohsen", Currency.Eur);
        account.Credit(new Money(100m, Currency.Eur), "salary");
        account.Debit(new Money(40m, Currency.Eur), "rent");

        await repo.SaveAccountAsync(account);

        var loaded = await repo.LoadAccountAsync(accountId);

        loaded.Should().NotBeNull();
        loaded!.Owner.Should().Be("Mohsen");
        loaded.Currency.Should().Be(Currency.Eur);
        loaded.Balance.Should().Be(new Money(60m, Currency.Eur));
        loaded.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task Streams_in_one_tenant_are_invisible_to_another_tenant()
    {
        await using var store = BuildStore();

        var tenantA = new StaticTenantContext(TenantId.Parse("tenant-a"));
        var tenantB = new StaticTenantContext(TenantId.Parse("tenant-b"));

        var repoA = new MartenAggregateRepository(store, tenantA);
        var repoB = new MartenAggregateRepository(store, tenantB);

        var accountId = AccountId.New();
        var account = Account.Open(accountId, "TenantA Customer", Currency.Eur);
        await repoA.SaveAccountAsync(account);

        var fromA = await repoA.LoadAccountAsync(accountId);
        var fromB = await repoB.LoadAccountAsync(accountId);

        fromA.Should().NotBeNull("tenant A wrote the stream and should see it");
        fromB.Should().BeNull("conjoined tenancy must hide tenant A's stream from tenant B");
    }

    [Fact]
    public async Task Hold_round_trips_through_marten()
    {
        await using var store = BuildStore();
        var tenantContext = new StaticTenantContext(TenantId.Parse("acme"));
        var repo = new MartenAggregateRepository(store, tenantContext);

        var holdId = HoldId.New();
        var accountId = AccountId.New();
        var hold = Hold.Place(
            holdId, accountId,
            new Money(50m, Currency.Eur),
            DateTimeOffset.UtcNow.AddHours(1),
            "checkout-1234");

        await repo.SaveHoldAsync(hold);

        var loaded = await repo.LoadHoldAsync(holdId);

        loaded.Should().NotBeNull();
        loaded!.Amount.Should().Be(new Money(50m, Currency.Eur));
        loaded.Status.Should().Be(HoldStatus.Active);
        loaded.AccountId.Should().Be(accountId);
    }
}
