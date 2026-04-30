// Despite living under Ledger.Infrastructure.IntegrationTests, these
// are unit tests for the projection's fold logic — they do not need
// Marten or a database. Co-located with the projection's project so
// they don't drag an Infrastructure reference into the Domain test
// project. The Marten wiring is proved separately by the
// Category=Integration tests in MartenWiringTests.

using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;
using Ledger.Infrastructure.Projections;

namespace Ledger.Infrastructure.IntegrationTests;

public sealed class AccountBalanceFoldTests
{
    [Fact]
    public void Create_initialises_view_from_AccountOpened()
    {
        var id = AccountId.New();
        var doc = AccountBalanceProjection.Create(
            new AccountOpened(id, "Mohsen", Currency.Eur));

        doc.Id.Should().Be(id.Value);
        doc.Owner.Should().Be("Mohsen");
        doc.Currency.Should().Be("EUR");
        doc.Balance.Should().Be(0m);
        doc.Status.Should().Be("Active");
    }

    [Fact]
    public void Apply_credit_and_debit_track_running_balance()
    {
        var id = AccountId.New();
        var doc = AccountBalanceProjection.Create(
            new AccountOpened(id, "Mohsen", Currency.Eur));

        AccountBalanceProjection.Apply(doc, new AccountCredited(
            id, new Money(100m, Currency.Eur), "salary"));
        AccountBalanceProjection.Apply(doc, new AccountDebited(
            id, new Money(40m, Currency.Eur), "rent"));

        doc.Balance.Should().Be(60m);
    }

    [Fact]
    public void Apply_freeze_and_unfreeze_toggle_status()
    {
        var id = AccountId.New();
        var doc = AccountBalanceProjection.Create(
            new AccountOpened(id, "Mohsen", Currency.Eur));

        AccountBalanceProjection.Apply(doc, new AccountFrozen(id, "compliance"));
        doc.Status.Should().Be("Frozen");

        AccountBalanceProjection.Apply(doc, new AccountUnfrozen(id));
        doc.Status.Should().Be("Active");
    }

    [Fact]
    public void Apply_close_marks_view_as_closed()
    {
        var id = AccountId.New();
        var doc = AccountBalanceProjection.Create(
            new AccountOpened(id, "Mohsen", Currency.Eur));

        AccountBalanceProjection.Apply(doc, new AccountClosed(id, "done"));
        doc.Status.Should().Be("Closed");
    }
}
