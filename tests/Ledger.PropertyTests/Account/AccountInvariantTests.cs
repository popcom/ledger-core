using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

namespace Ledger.PropertyTests.Account;

/// <summary>
/// Property-based tests assert the invariants the brief lists for the
/// Account aggregate over arbitrary sequences of valid commands. The
/// generators produce well-formed inputs only; the goal is to exercise
/// state transitions, not to spam the constructor with invalid data
/// (the example-based unit tests already cover argument validation).
/// </summary>
public sealed class AccountInvariantTests
{
    private static readonly Currency Eur = Currency.Eur;

    [Property(MaxTest = 200)]
    public Property Replaying_pending_events_into_a_fresh_aggregate_reproduces_state()
    {
        return Prop.ForAll(GenCommands(), commands =>
        {
            var account = global::Ledger.Domain.Aggregates.Account.Open(AccountId.New(), "Mohsen", Eur);
            ApplyCommands(account, commands);

            var history = new List<AccountEvent>(account.PendingEvents);
            var rehydrated = global::Ledger.Domain.Aggregates.Account.Rehydrate(history);

            return rehydrated.Balance == account.Balance
                && rehydrated.Status == account.Status
                && rehydrated.Owner == account.Owner
                && rehydrated.Currency == account.Currency
                && rehydrated.Version == account.Version;
        });
    }

    [Property(MaxTest = 200)]
    public Property Balance_equals_credits_minus_debits_for_any_valid_history()
    {
        return Prop.ForAll(GenCommands(), commands =>
        {
            var account = global::Ledger.Domain.Aggregates.Account.Open(AccountId.New(), "Mohsen", Eur);
            ApplyCommands(account, commands);

            var credited = account.PendingEvents.OfType<AccountCredited>().Sum(e => e.Amount.Amount);
            var debited = account.PendingEvents.OfType<AccountDebited>().Sum(e => e.Amount.Amount);

            return account.Balance.Amount == credited - debited;
        });
    }

    [Property(MaxTest = 200)]
    public Property Balance_never_goes_negative_through_any_valid_sequence()
    {
        return Prop.ForAll(GenCommands(), commands =>
        {
            var account = global::Ledger.Domain.Aggregates.Account.Open(AccountId.New(), "Mohsen", Eur);
            ApplyCommands(account, commands);

            return account.Balance.Amount >= 0m;
        });
    }

    [Property(MaxTest = 100)]
    public Property Frozen_account_rejects_credits_and_debits_for_any_amount()
    {
        return Prop.ForAll(
            ArbMap.Default.GeneratorFor<decimal>()
                .Where(d => d > 0m && d < 1_000_000_000m)
                .ToArbitrary(),
            amount =>
            {
                var account = global::Ledger.Domain.Aggregates.Account.Open(
                    AccountId.New(), "Mohsen", Eur);
                account.Credit(new Money(1_000_000m, Eur), "seed");
                account.Freeze("test");

                var credit = () => account.Credit(new Money(amount, Eur), "x");
                var debit = () => account.Debit(new Money(amount, Eur), "x");

                try
                {
                    credit();
                    return false;
                }
                catch (AccountFrozenException) { }

                try
                {
                    debit();
                    return false;
                }
                catch (AccountFrozenException) { }

                return true;
            });
    }

    [Property(MaxTest = 50)]
    public Property Closed_account_rejects_every_state_changing_command()
    {
        return Prop.ForAll(
            ArbMap.Default.GeneratorFor<decimal>()
                .Where(d => d > 0m && d < 1_000_000m)
                .ToArbitrary(),
            amount =>
            {
                var account = global::Ledger.Domain.Aggregates.Account.Open(
                    AccountId.New(), "Mohsen", Eur);
                account.Close("done");

                var allRejected = true;

                try { account.Credit(new Money(amount, Eur), "x"); allRejected = false; }
                catch (AccountClosedException) { }

                try { account.Debit(new Money(amount, Eur), "x"); allRejected = false; }
                catch (AccountClosedException) { }

                try { account.Freeze("x"); allRejected = false; }
                catch (AccountClosedException) { }

                return allRejected;
            });
    }

    private enum CommandKind { Credit, Debit, Freeze, Unfreeze, NoOp }

    private sealed record Command(CommandKind Kind, decimal Amount);

    private static Arbitrary<List<Command>> GenCommands()
    {
        var amountGen = Gen.Choose(1, 1_000).Select(x => (decimal)x);
        var kindGen = Gen.Frequency(
            (40, Gen.Constant(CommandKind.Credit)),
            (40, Gen.Constant(CommandKind.Debit)),
            (5, Gen.Constant(CommandKind.Freeze)),
            (5, Gen.Constant(CommandKind.Unfreeze)),
            (10, Gen.Constant(CommandKind.NoOp)));

        var commandGen =
            from kind in kindGen
            from amt in amountGen
            select new Command(kind, amt);

        return Gen.ListOf(commandGen).Select(seq => seq.ToList()).ToArbitrary();
    }

    private static void ApplyCommands(
        global::Ledger.Domain.Aggregates.Account account,
        List<Command> commands)
    {
        foreach (var cmd in commands)
        {
            try
            {
                switch (cmd.Kind)
                {
                    case CommandKind.Credit:
                        account.Credit(new Money(cmd.Amount, Eur), "test-credit");
                        break;
                    case CommandKind.Debit:
                        account.Debit(new Money(cmd.Amount, Eur), "test-debit");
                        break;
                    case CommandKind.Freeze:
                        account.Freeze("test-freeze");
                        break;
                    case CommandKind.Unfreeze:
                        if (account.Status == AccountStatus.Frozen)
                        {
                            account.Unfreeze();
                        }
                        break;
                    case CommandKind.NoOp:
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled {cmd.Kind}");
                }
            }
            catch (InsufficientFundsException) { /* expected — debit beyond balance */ }
            catch (AccountFrozenException) { /* expected — credit/debit on frozen */ }
        }
    }
}
