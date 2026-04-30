using Ledger.Application.Queries;
using Ledger.Application.Tenancy;
using Ledger.Domain.Aggregates;

using Marten;

namespace Ledger.Infrastructure.Projections;

/// <summary>
/// Daily-statement reader. Folds the account stream into one row per
/// UTC day across the requested window, computing opening / closing /
/// credits / debits / movement count per day.
/// </summary>
/// <remarks>
/// Computed live from the event stream rather than materialised into
/// its own projection table. Statements are read rarely and the
/// stream fold is bounded by the account's lifetime; a materialised
/// projection (likely async, async lifecycle) is the natural next
/// step once the brief's load tests show the fold is the bottleneck.
/// </remarks>
public sealed class MartenDailyStatementQuery : IDailyStatementQuery
{
    private readonly IDocumentStore _store;
    private readonly ITenantContext _tenantContext;

    public MartenDailyStatementQuery(IDocumentStore store, ITenantContext tenantContext)
    {
        _store = store;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<DailyStatementEntry>> ForAccountAsync(
        Guid accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("'toDate' must be on or after 'fromDate'.", nameof(toDate));
        }

        await using var session = _store.QuerySession(_tenantContext.Current.Value);
        var events = await session.Events.FetchStreamAsync(
            accountId, token: cancellationToken).ConfigureAwait(false);

        if (events.Count == 0)
        {
            return Array.Empty<DailyStatementEntry>();
        }

        string currency = string.Empty;
        decimal balance = 0m;

        var dailyEntries = new SortedDictionary<DateOnly, DailyStatementEntry>();

        foreach (var record in events)
        {
            var date = DateOnly.FromDateTime(record.Timestamp.UtcDateTime);

            if (record.Data is AccountOpened opened)
            {
                currency = opened.Currency.Code;
            }
            else if (record.Data is AccountCredited credited && date >= fromDate && date <= toDate)
            {
                balance += credited.Amount.Amount;
                dailyEntries[date] = WithCredit(dailyEntries.GetValueOrDefault(date), date, accountId, currency, balance, credited.Amount.Amount);
            }
            else if (record.Data is AccountDebited debited && date >= fromDate && date <= toDate)
            {
                balance -= debited.Amount.Amount;
                dailyEntries[date] = WithDebit(dailyEntries.GetValueOrDefault(date), date, accountId, currency, balance, debited.Amount.Amount);
            }
            else if (record.Data is AccountCredited preCredit)
            {
                balance += preCredit.Amount.Amount;
            }
            else if (record.Data is AccountDebited preDebit)
            {
                balance -= preDebit.Amount.Amount;
            }
        }

        return dailyEntries.Values.ToList();
    }

    private static DailyStatementEntry WithCredit(
        DailyStatementEntry? existing,
        DateOnly date,
        Guid accountId,
        string currency,
        decimal closing,
        decimal credit)
    {
        if (existing is null)
        {
            var opening = closing - credit;
            return new DailyStatementEntry(accountId, date, currency, opening, credit, 0m, closing, 1);
        }

        return existing with
        {
            Credits = existing.Credits + credit,
            ClosingBalance = closing,
            Movements = existing.Movements + 1,
        };
    }

    private static DailyStatementEntry WithDebit(
        DailyStatementEntry? existing,
        DateOnly date,
        Guid accountId,
        string currency,
        decimal closing,
        decimal debit)
    {
        if (existing is null)
        {
            var opening = closing + debit;
            return new DailyStatementEntry(accountId, date, currency, opening, 0m, debit, closing, 1);
        }

        return existing with
        {
            Debits = existing.Debits + debit,
            ClosingBalance = closing,
            Movements = existing.Movements + 1,
        };
    }
}
