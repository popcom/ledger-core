using System.Text.Json;

using Ledger.Application.Idempotency;
using Ledger.Domain.ValueObjects;

using MediatR;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Ledger.Application.UnitTests.Idempotency;

public sealed class IdempotencyPipelineBehaviorTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private sealed record FakeRequest(IdempotencyKey IdempotencyKey)
        : IIdempotentRequest<FakeResponse>;

    private sealed record FakeResponse(string Value);

    [Fact]
    public async Task First_call_invokes_next_and_stores_response()
    {
        var store = Substitute.For<IIdempotencyStore>();
        store.TryGetAsync(Arg.Any<IdempotencyKey>(), Arg.Any<CancellationToken>())
            .Returns((StoredIdempotencyResponse?)null);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var behavior = new IdempotencyPipelineBehavior<FakeRequest, FakeResponse>(store, clock);

        var key = IdempotencyKey.Parse("first");
        var request = new FakeRequest(key);
        var nextCalls = 0;

        Task<FakeResponse> Next() { nextCalls++; return Task.FromResult(new FakeResponse("ok")); }

        var response = await behavior.Handle(request, Next, CancellationToken.None);

        response.Value.Should().Be("ok");
        nextCalls.Should().Be(1);
        await store.Received(1).PutAsync(
            Arg.Is<IdempotencyKey>(k => k == key),
            Arg.Any<StoredIdempotencyResponse>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replay_short_circuits_handler_and_returns_stored_payload()
    {
        var store = Substitute.For<IIdempotencyStore>();
        var stored = new StoredIdempotencyResponse(
            ContentType: "application/json",
            Body: JsonSerializer.Serialize(new FakeResponse("from-cache"), WebJson),
            StatusCode: 200,
            StoredAt: DateTimeOffset.UtcNow);

        store.TryGetAsync(Arg.Any<IdempotencyKey>(), Arg.Any<CancellationToken>())
            .Returns(stored);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var behavior = new IdempotencyPipelineBehavior<FakeRequest, FakeResponse>(store, clock);

        var nextCalls = 0;
        Task<FakeResponse> Next() { nextCalls++; return Task.FromResult(new FakeResponse("fresh")); }

        var response = await behavior.Handle(
            new FakeRequest(IdempotencyKey.Parse("replay")), Next, CancellationToken.None);

        response.Value.Should().Be("from-cache");
        nextCalls.Should().Be(0);
        await store.DidNotReceive().PutAsync(
            Arg.Any<IdempotencyKey>(),
            Arg.Any<StoredIdempotencyResponse>(),
            Arg.Any<CancellationToken>());
    }
}
