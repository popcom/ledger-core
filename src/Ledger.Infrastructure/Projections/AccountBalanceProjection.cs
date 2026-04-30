using Ledger.Domain.Aggregates;

using Marten.Events.Aggregation;

namespace Ledger.Infrastructure.Projections;

/// <summary>
/// Marten single-stream projection that folds <c>AccountEvent</c>s
/// into the <c>account_balances</c> read model. Inline lifecycle:
/// the projection writes within the same transaction as the events
/// so a command handler can read the view back immediately after
/// writing the event stream.
/// </summary>
public sealed class AccountBalanceProjection : SingleStreamProjection<AccountBalanceDocument, Guid>
{
    public static AccountBalanceDocument Create(AccountOpened evt) => new()
    {
        Id = evt.AccountId.Value,
        Owner = evt.Owner,
        Currency = evt.Currency.Code,
        Balance = 0m,
        Status = "Active",
        OpenedAt = evt.OccurredAt,
        LastEventAt = evt.OccurredAt,
    };

    public static void Apply(AccountBalanceDocument view, AccountCredited evt)
    {
        view.Balance += evt.Amount.Amount;
        view.LastEventAt = evt.OccurredAt;
    }

    public static void Apply(AccountBalanceDocument view, AccountDebited evt)
    {
        view.Balance -= evt.Amount.Amount;
        view.LastEventAt = evt.OccurredAt;
    }

    public static void Apply(AccountBalanceDocument view, AccountFrozen evt)
    {
        view.Status = "Frozen";
        view.LastEventAt = evt.OccurredAt;
    }

    public static void Apply(AccountBalanceDocument view, AccountUnfrozen evt)
    {
        view.Status = "Active";
        view.LastEventAt = evt.OccurredAt;
    }

    public static void Apply(AccountBalanceDocument view, AccountClosed evt)
    {
        view.Status = "Closed";
        view.LastEventAt = evt.OccurredAt;
    }
}
