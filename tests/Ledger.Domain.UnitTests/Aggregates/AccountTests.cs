using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.UnitTests.Aggregates;

public sealed class AccountTests
{
    private static readonly AccountId TestId = AccountId.New();
    private static readonly Currency Eur = Currency.Eur;
    private static readonly Currency Usd = Currency.Usd;

    [Fact]
    public void Open_emits_AccountOpened_and_initialises_state()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);

        account.Id.Should().Be(TestId);
        account.Owner.Should().Be("Mohsen");
        account.Currency.Should().Be(Eur);
        account.Status.Should().Be(AccountStatus.Active);
        account.Balance.Should().Be(Money.Zero(Eur));
        account.IsOpened.Should().BeTrue();
        account.Version.Should().Be(1);

        account.PendingEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AccountOpened>();
    }

    [Fact]
    public void Open_rejects_blank_owner()
    {
        var act = () => Account.Open(TestId, "  ", Eur);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Credit_increases_balance()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Credit(new Money(100m, Eur), "salary");

        account.Balance.Should().Be(new Money(100m, Eur));
        account.PendingEvents.OfType<AccountCredited>().Should().ContainSingle();
    }

    [Fact]
    public void Debit_decreases_balance()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Credit(new Money(100m, Eur), "salary");
        account.Debit(new Money(40m, Eur), "rent");

        account.Balance.Should().Be(new Money(60m, Eur));
    }

    [Fact]
    public void Debit_throws_when_insufficient_funds()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Credit(new Money(10m, Eur), "deposit");

        var act = () => account.Debit(new Money(40m, Eur), "rent");
        act.Should().Throw<InsufficientFundsException>()
            .Which.Code.Should().Be("account.insufficient_funds");
    }

    [Fact]
    public void Credit_throws_on_currency_mismatch()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        var act = () => account.Credit(new Money(10m, Usd), "deposit");
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Credit_rejects_non_positive_amounts(decimal amount)
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        var act = () => account.Credit(new Money(amount, Eur), "deposit");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Debit_rejects_non_positive_amounts(decimal amount)
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        var act = () => account.Debit(new Money(amount, Eur), "withdraw");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Freeze_changes_status_and_emits_event()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Freeze("compliance hold");

        account.Status.Should().Be(AccountStatus.Frozen);
        account.PendingEvents.OfType<AccountFrozen>().Should().ContainSingle();
    }

    [Fact]
    public void Freeze_is_idempotent()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Freeze("compliance");
        var snapshotCount = account.PendingEvents.Count;

        account.Freeze("compliance");

        account.PendingEvents.Should().HaveCount(snapshotCount);
    }

    [Fact]
    public void Frozen_account_rejects_credit_and_debit()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Credit(new Money(100m, Eur), "deposit");
        account.Freeze("compliance");

        var credit = () => account.Credit(new Money(10m, Eur), "x");
        var debit = () => account.Debit(new Money(10m, Eur), "x");

        credit.Should().Throw<AccountFrozenException>();
        debit.Should().Throw<AccountFrozenException>();
    }

    [Fact]
    public void Unfreeze_restores_active_status()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Freeze("compliance");
        account.Unfreeze();

        account.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public void Unfreeze_throws_when_not_frozen()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        var act = () => account.Unfreeze();
        act.Should().Throw<AccountNotFrozenException>();
    }

    [Fact]
    public void Close_succeeds_when_balance_is_zero()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Close("customer request");

        account.Status.Should().Be(AccountStatus.Closed);
    }

    [Fact]
    public void Close_throws_when_balance_is_non_zero()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Credit(new Money(5m, Eur), "deposit");

        var act = () => account.Close("trying");
        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Closed_account_rejects_every_command()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Close("done");

        var freeze = () => account.Freeze("x");
        var credit = () => account.Credit(new Money(1m, Eur), "x");
        var debit = () => account.Debit(new Money(1m, Eur), "x");

        freeze.Should().Throw<AccountClosedException>();
        credit.Should().Throw<AccountClosedException>();
        debit.Should().Throw<AccountClosedException>();
    }

    [Fact]
    public void Rehydrate_replays_history_into_current_state()
    {
        var history = new AccountEvent[]
        {
            new AccountOpened(TestId, "Mohsen", Eur),
            new AccountCredited(TestId, new Money(100m, Eur), "salary"),
            new AccountDebited(TestId, new Money(40m, Eur), "rent"),
            new AccountFrozen(TestId, "compliance"),
        };

        var account = Account.Rehydrate(history);

        account.Owner.Should().Be("Mohsen");
        account.Balance.Should().Be(new Money(60m, Eur));
        account.Status.Should().Be(AccountStatus.Frozen);
        account.Version.Should().Be(history.Length);
        account.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearPendingEvents_drops_buffered_events_after_flush()
    {
        var account = Account.Open(TestId, "Mohsen", Eur);
        account.Credit(new Money(100m, Eur), "salary");
        account.PendingEvents.Should().HaveCount(2);

        account.ClearPendingEvents();
        account.PendingEvents.Should().BeEmpty();
    }
}
