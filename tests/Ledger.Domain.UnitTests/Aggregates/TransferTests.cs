using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

namespace Ledger.Domain.UnitTests.Aggregates;

public sealed class TransferTests
{
    private static readonly Money Ten = new(10m, Currency.Eur);

    private static (Transfer transfer, AccountId src, AccountId dst) NewTransfer()
    {
        var src = AccountId.New();
        var dst = AccountId.New();
        var transfer = Transfer.Initiate(TransferId.New(), src, dst, Ten, "ref-1");
        return (transfer, src, dst);
    }

    [Fact]
    public void Initiate_emits_event_and_starts_in_AwaitingDebit()
    {
        var (transfer, src, dst) = NewTransfer();

        transfer.Status.Should().Be(TransferStatus.AwaitingDebit);
        transfer.SourceAccountId.Should().Be(src);
        transfer.DestinationAccountId.Should().Be(dst);
        transfer.Amount.Should().Be(Ten);
        transfer.Reference.Should().Be("ref-1");
        transfer.IsInitiated.Should().BeTrue();
        transfer.Version.Should().Be(1);
        transfer.PendingEvents.Should().ContainSingle().Which.Should().BeOfType<TransferInitiated>();
    }

    [Fact]
    public void Initiate_rejects_same_source_and_destination()
    {
        var same = AccountId.New();
        var act = () => Transfer.Initiate(TransferId.New(), same, same, Ten, "x");
        act.Should().Throw<TransferSameAccountException>();
    }

    [Fact]
    public void Initiate_rejects_non_positive_amount()
    {
        var act = () => Transfer.Initiate(
            TransferId.New(), AccountId.New(), AccountId.New(),
            new Money(0m, Currency.Eur), "x");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Happy_path_walks_AwaitingDebit_to_Completed()
    {
        var (transfer, _, _) = NewTransfer();

        transfer.ConfirmDebit();
        transfer.Status.Should().Be(TransferStatus.AwaitingCredit);

        transfer.ConfirmCredit();
        transfer.Status.Should().Be(TransferStatus.Completed);

        transfer.PendingEvents.Should().Contain(e => e is TransferDebitConfirmed);
        transfer.PendingEvents.Should().Contain(e => e is TransferCreditConfirmed);
        transfer.PendingEvents.Should().Contain(e => e is TransferCompleted);
    }

    [Fact]
    public void Fail_before_debit_transitions_directly_to_Failed()
    {
        var (transfer, _, _) = NewTransfer();

        transfer.Fail("source frozen");

        transfer.Status.Should().Be(TransferStatus.Failed);
        transfer.FailureReason.Should().Be("source frozen");
    }

    [Fact]
    public void Fail_after_debit_starts_compensation_and_completes_it_to_Failed()
    {
        var (transfer, _, _) = NewTransfer();
        transfer.ConfirmDebit();

        transfer.Fail("destination closed");
        transfer.Status.Should().Be(TransferStatus.Compensating);

        transfer.CompleteCompensation();
        transfer.Status.Should().Be(TransferStatus.Failed);
        transfer.FailureReason.Should().Be("destination closed");
    }

    [Fact]
    public void Fail_in_terminal_state_throws()
    {
        var (transfer, _, _) = NewTransfer();
        transfer.ConfirmDebit();
        transfer.ConfirmCredit();

        var act = () => transfer.Fail("late");
        act.Should().Throw<TransferInvalidStateException>();
    }

    [Fact]
    public void ConfirmCredit_before_ConfirmDebit_throws()
    {
        var (transfer, _, _) = NewTransfer();
        var act = () => transfer.ConfirmCredit();
        act.Should().Throw<TransferInvalidStateException>();
    }

    [Fact]
    public void Rehydrate_replays_history_into_current_state()
    {
        var src = AccountId.New();
        var dst = AccountId.New();
        var id = TransferId.New();

        var history = new TransferEvent[]
        {
            new TransferInitiated(id, src, dst, Ten, "ref"),
            new TransferDebitConfirmed(id, src, Ten),
            new TransferCreditConfirmed(id, dst, Ten),
            new TransferCompleted(id),
        };

        var transfer = Transfer.Rehydrate(history);

        transfer.Status.Should().Be(TransferStatus.Completed);
        transfer.Version.Should().Be(history.Length);
        transfer.PendingEvents.Should().BeEmpty();
    }
}
