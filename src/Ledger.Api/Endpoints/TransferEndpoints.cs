using Ledger.Application.Commands.InitiateTransfer;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using MediatR;

namespace Ledger.Api.Endpoints;

internal static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/transfers").WithTags("Transfers");

        group.MapPost("/", InitiateTransfer).WithName("InitiateTransfer");

        return app;
    }

    public sealed record InitiateTransferRequest(
        Guid SourceAccountId,
        Guid DestinationAccountId,
        decimal Amount,
        string Currency,
        string Reference);

    private static async Task<IResult> InitiateTransfer(
        InitiateTransferRequest body,
        [AsParameters] IdempotencyHeader idempotency,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(body.Currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(body.Reference);

        var key = string.IsNullOrWhiteSpace(idempotency.Value)
            ? throw new MissingIdempotencyKeyException()
            : IdempotencyKey.Parse(idempotency.Value);

        var command = new InitiateTransferCommand(
            new AccountId(body.SourceAccountId),
            new AccountId(body.DestinationAccountId),
            new Money(body.Amount, Currency.Parse(body.Currency)),
            body.Reference,
            key);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return result.Status == nameof(TransferStatus.Completed)
            ? Results.Created($"/v1/transfers/{result.TransferId}", result)
            : Results.Ok(result);
    }
}
