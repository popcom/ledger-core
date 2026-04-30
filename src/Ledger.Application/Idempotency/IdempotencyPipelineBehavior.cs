using System.Text.Json;

using MediatR;

namespace Ledger.Application.Idempotency;

/// <summary>
/// MediatR pipeline behavior that short-circuits replays of an
/// <see cref="IIdempotentRequest{T}"/> by returning the cached
/// response. Applied uniformly so future commands inherit idempotency
/// without copy-paste in every handler.
/// </summary>
public sealed class IdempotencyPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IIdempotentRequest<TResponse>
    where TResponse : notnull
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyStore _store;
    private readonly TimeProvider _timeProvider;

    public IdempotencyPipelineBehavior(IIdempotencyStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var key = request.IdempotencyKey;
        var existing = await _store.TryGetAsync(key, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return JsonSerializer.Deserialize<TResponse>(existing.Body, SerializerOptions)
                ?? throw new InvalidOperationException(
                    $"Stored idempotency payload for key '{key}' was empty or malformed.");
        }

        var response = await next().ConfigureAwait(false);

        var serialised = JsonSerializer.Serialize(response, SerializerOptions);
        await _store.PutAsync(
            key,
            new StoredIdempotencyResponse(
                ContentType: "application/json",
                Body: serialised,
                StatusCode: 200,
                StoredAt: _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        return response;
    }
}
