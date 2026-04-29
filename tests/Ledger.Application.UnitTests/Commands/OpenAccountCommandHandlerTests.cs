using Ledger.Application.Commands.OpenAccount;
using Ledger.Application.Persistence;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Ledger.Application.UnitTests.Commands;

public sealed class OpenAccountCommandHandlerTests
{
    [Fact]
    public async Task Handle_persists_account_and_returns_result_with_new_id()
    {
        var repo = Substitute.For<IAggregateRepository>();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero));
        var handler = new OpenAccountCommandHandler(repo, clock);

        var command = new OpenAccountCommand(
            Owner: "Mohsen",
            Currency: Currency.Eur,
            IdempotencyKey: IdempotencyKey.Parse("req-1"));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Owner.Should().Be("Mohsen");
        result.Currency.Should().Be("EUR");
        result.AccountId.Should().NotBe(Guid.Empty);
        result.OpenedAt.Should().Be(clock.GetUtcNow());

        await repo.Received(1).SaveAccountAsync(
            Arg.Is<Account>(a => a.Owner == "Mohsen" && a.Currency == Currency.Eur),
            Arg.Any<CancellationToken>());
    }
}
