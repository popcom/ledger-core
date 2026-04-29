using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using Microsoft.Extensions.Time.Testing;

namespace Ledger.Domain.UnitTests.Aggregates;

public sealed class HoldTests
{
    private static readonly Currency Eur = Currency.Eur;

    private static FakeTimeProvider NewClock(DateTimeOffset now) =>
        new(now);

    [Fact]
    public void Place_creates_active_hold_and_emits_event()
    {
        var clock = NewClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var expiry = clock.GetUtcNow().AddHours(1);

        var hold = Hold.Place(
            HoldId.New(),
            AccountId.New(),
            new Money(50m, Eur),
            expiry,
            "checkout-1234",
            clock);

        hold.Status.Should().Be(HoldStatus.Active);
        hold.Amount.Should().Be(new Money(50m, Eur));
        hold.ExpiresAt.Should().Be(expiry);
        hold.Reference.Should().Be("checkout-1234");
        hold.IsPlaced.Should().BeTrue();
        hold.Version.Should().Be(1);
        hold.PendingEvents.Should().ContainSingle()
            .Which.Should().BeOfType<HoldPlaced>();
    }

    [Fact]
    public void Place_rejects_non_positive_amount()
    {
        var clock = NewClock(DateTimeOffset.UtcNow);
        var act = () => Hold.Place(
            HoldId.New(), AccountId.New(),
            new Money(0m, Eur), clock.GetUtcNow().AddHours(1), "x", clock);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Place_rejects_expiry_in_the_past()
    {
        var clock = NewClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var act = () => Hold.Place(
            HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddSeconds(-1), "x", clock);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Capture_transitions_active_hold_to_captured()
    {
        var clock = NewClock(DateTimeOffset.UtcNow);
        var hold = Hold.Place(HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddHours(1), "x", clock);

        hold.Capture(clock);
        hold.Status.Should().Be(HoldStatus.Captured);
    }

    [Fact]
    public void Capture_throws_if_hold_has_expired()
    {
        var clock = NewClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var hold = Hold.Place(HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddMinutes(5), "x", clock);

        clock.Advance(TimeSpan.FromMinutes(10));

        var act = () => hold.Capture(clock);
        act.Should().Throw<HoldExpiredException>();
    }

    [Fact]
    public void Release_transitions_active_hold_to_released()
    {
        var clock = NewClock(DateTimeOffset.UtcNow);
        var hold = Hold.Place(HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddHours(1), "x", clock);

        hold.Release("user cancelled");
        hold.Status.Should().Be(HoldStatus.Released);
    }

    [Fact]
    public void Expire_transitions_active_hold_to_expired_when_past_expiry()
    {
        var clock = NewClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var hold = Hold.Place(HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddMinutes(5), "x", clock);

        clock.Advance(TimeSpan.FromMinutes(10));
        hold.Expire(clock);

        hold.Status.Should().Be(HoldStatus.Expired);
        hold.IsExpired(clock).Should().BeFalse(
            "IsExpired only returns true while the hold is still in Active state");
    }

    [Fact]
    public void Expire_throws_if_called_before_expiry()
    {
        var clock = NewClock(DateTimeOffset.UtcNow);
        var hold = Hold.Place(HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddMinutes(5), "x", clock);

        var act = () => hold.Expire(clock);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(HoldStatus.Captured)]
    [InlineData(HoldStatus.Released)]
    [InlineData(HoldStatus.Expired)]
    public void Terminal_holds_reject_further_transitions(HoldStatus terminal)
    {
        var clock = NewClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var holdId = HoldId.New();
        var accountId = AccountId.New();
        var amount = new Money(10m, Eur);
        var expiry = clock.GetUtcNow().AddMinutes(5);

        var history = new List<HoldEvent>
        {
            new HoldPlaced(holdId, accountId, amount, expiry, "ref"),
        };

        history.Add(terminal switch
        {
            HoldStatus.Captured => new HoldCaptured(holdId, accountId, amount),
            HoldStatus.Released => new HoldReleased(holdId, accountId, "test"),
            HoldStatus.Expired => new HoldExpired(holdId, accountId),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal)),
        });

        var hold = Hold.Rehydrate(history);
        hold.Status.Should().Be(terminal);

        var capture = () => hold.Capture(clock);
        var release = () => hold.Release("x");

        capture.Should().Throw<HoldAlreadyTerminalException>();
        release.Should().Throw<HoldAlreadyTerminalException>();
    }

    [Fact]
    public void IsExpired_returns_true_for_active_hold_past_expiry()
    {
        var clock = NewClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var hold = Hold.Place(HoldId.New(), AccountId.New(),
            new Money(10m, Eur), clock.GetUtcNow().AddMinutes(5), "x", clock);

        hold.IsExpired(clock).Should().BeFalse();
        clock.Advance(TimeSpan.FromMinutes(10));
        hold.IsExpired(clock).Should().BeTrue();
    }

    [Fact]
    public void Rehydrate_replays_history_into_current_state()
    {
        var holdId = HoldId.New();
        var accountId = AccountId.New();
        var expiry = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var history = new HoldEvent[]
        {
            new HoldPlaced(holdId, accountId, new Money(25m, Eur), expiry, "x"),
            new HoldCaptured(holdId, accountId, new Money(25m, Eur)),
        };

        var hold = Hold.Rehydrate(history);

        hold.Status.Should().Be(HoldStatus.Captured);
        hold.Version.Should().Be(history.Length);
        hold.PendingEvents.Should().BeEmpty();
    }
}
