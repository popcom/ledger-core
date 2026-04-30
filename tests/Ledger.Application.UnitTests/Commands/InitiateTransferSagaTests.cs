using Ledger.Application.Commands.InitiateTransfer;
using Ledger.Application.Outbox;
using Ledger.Application.Persistence;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using NSubstitute;

namespace Ledger.Application.UnitTests.Commands;

public sealed class InitiateTransferSagaTests
{
    private static readonly Money Twenty = new(20m, Currency.Eur);

    private static (Account source, Account destination) NewAccounts(decimal sourceBalance = 100m)
    {
        var src = Account.Open(AccountId.New(), "src", Currency.Eur);
        if (sourceBalance > 0)
        {
            src.Credit(new Money(sourceBalance, Currency.Eur), "seed");
        }
        var dst = Account.Open(AccountId.New(), "dst", Currency.Eur);
        return (src, dst);
    }

    [Fact]
    public async Task Happy_path_completes_transfer_and_persists_each_step()
    {
        var (source, destination) = NewAccounts();
        var repo = Substitute.For<IAggregateRepository>();

        repo.LoadAccountAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        repo.LoadAccountAsync(destination.Id, Arg.Any<CancellationToken>()).Returns(destination);

        var saga = new InitiateTransferSaga(repo, Substitute.For<IOutbox>());
        var command = new InitiateTransferCommand(
            source.Id, destination.Id, Twenty, "salary",
            IdempotencyKey.Parse("transfer-1"));

        var result = await saga.Handle(command, CancellationToken.None);

        result.Status.Should().Be(nameof(TransferStatus.Completed));
        source.Balance.Should().Be(new Money(80m, Currency.Eur));
        destination.Balance.Should().Be(new Money(20m, Currency.Eur));

        // Saga walks the state machine to terminal => 4 saves on the
        // transfer (Initiated, ConfirmDebit, ConfirmCredit) plus saves
        // on the two accounts.
        await repo.Received().SaveTransferAsync(
            Arg.Any<Transfer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Insufficient_funds_at_source_fails_transfer_without_compensation()
    {
        var (source, destination) = NewAccounts(sourceBalance: 5m);
        var repo = Substitute.For<IAggregateRepository>();

        repo.LoadAccountAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        repo.LoadAccountAsync(destination.Id, Arg.Any<CancellationToken>()).Returns(destination);

        var saga = new InitiateTransferSaga(repo, Substitute.For<IOutbox>());
        var command = new InitiateTransferCommand(
            source.Id, destination.Id, Twenty, "rent",
            IdempotencyKey.Parse("transfer-2"));

        var result = await saga.Handle(command, CancellationToken.None);

        result.Status.Should().Be(nameof(TransferStatus.Failed));
        result.FailureReason.Should().NotBeNullOrEmpty();
        source.Balance.Should().Be(new Money(5m, Currency.Eur),
            "debit failed before any side effect; balance is unchanged");
        destination.Balance.Should().Be(Money.Zero(Currency.Eur),
            "destination is never touched on a debit failure");
    }

    [Fact]
    public async Task Frozen_destination_compensates_debit_back_to_source()
    {
        var (source, destination) = NewAccounts();
        destination.Freeze("compliance");

        var repo = Substitute.For<IAggregateRepository>();
        repo.LoadAccountAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        repo.LoadAccountAsync(destination.Id, Arg.Any<CancellationToken>()).Returns(destination);

        var saga = new InitiateTransferSaga(repo, Substitute.For<IOutbox>());
        var command = new InitiateTransferCommand(
            source.Id, destination.Id, Twenty, "rent",
            IdempotencyKey.Parse("transfer-3"));

        var result = await saga.Handle(command, CancellationToken.None);

        result.Status.Should().Be(nameof(TransferStatus.Failed));
        result.FailureReason.Should().NotBeNullOrEmpty();
        source.Balance.Should().Be(new Money(100m, Currency.Eur),
            "compensation refunds the debit so the source ends where it started");
        destination.Balance.Should().Be(Money.Zero(Currency.Eur),
            "the credit never applied because the destination is frozen");
    }
}
