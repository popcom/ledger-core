using Ledger.Domain.ValueObjects;

using MediatR;

namespace Ledger.Application.Idempotency;

/// <summary>
/// Marker for MediatR requests that should be processed under the
/// idempotency pipeline. Implementations expose the
/// <see cref="IdempotencyKey"/> the caller supplied; the pipeline
/// behavior applies the deduplication.
/// </summary>
/// <typeparam name="TResponse">Response type the request returns.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design", "CA1040:Avoid empty interfaces",
    Justification = "Marker interface that constrains the pipeline-behavior generic.")]
public interface IIdempotentRequest<TResponse> : IRequest<TResponse>
{
    public IdempotencyKey IdempotencyKey { get; }
}
