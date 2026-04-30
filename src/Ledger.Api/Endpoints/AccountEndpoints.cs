using Ledger.Api.Tenancy;
using Ledger.Application.Commands.CloseAccount;
using Ledger.Application.Commands.FreezeAccount;
using Ledger.Application.Commands.OpenAccount;
using Ledger.Application.Commands.UnfreezeAccount;
using Ledger.Application.Queries;
using Ledger.Domain.Aggregates;
using Ledger.Domain.ValueObjects;

using MediatR;

namespace Ledger.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/accounts").WithTags("Accounts");

        group.MapPost("/", OpenAccount).WithName("OpenAccount");
        group.MapGet("/", ListAccounts).WithName("ListAccounts");
        group.MapGet("/{accountId:guid}", GetAccount).WithName("GetAccount");
        group.MapGet("/{accountId:guid}/balance", GetBalanceAsOf).WithName("GetBalanceAsOf");
        group.MapPost("/{accountId:guid}/freeze", FreezeAccount).WithName("FreezeAccount");
        group.MapPost("/{accountId:guid}/unfreeze", UnfreezeAccount).WithName("UnfreezeAccount");
        group.MapPost("/{accountId:guid}/close", CloseAccount).WithName("CloseAccount");

        return app;
    }

    public sealed record OpenAccountRequest(string Owner, string Currency);

    private static async Task<IResult> OpenAccount(
        OpenAccountRequest body,
        [AsParameters] IdempotencyHeader idempotency,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(body.Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(body.Currency);

        var key = ParseIdempotencyKey(idempotency);
        var currency = Currency.Parse(body.Currency);

        var command = new OpenAccountCommand(body.Owner, currency, key);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.CreatedAtRoute("GetAccount",
            new { accountId = result.AccountId }, result);
    }

    private static async Task<IResult> GetAccount(
        Guid accountId,
        IAccountBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var view = await query.GetAsync(new AccountId(accountId), cancellationToken)
            .ConfigureAwait(false);

        return view is null
            ? Results.NotFound(new { error = "account.not_found", accountId })
            : Results.Ok(view);
    }

    private static async Task<IResult> GetBalanceAsOf(
        Guid accountId,
        DateTimeOffset? asOf,
        IAccountBalanceQuery balance,
        IAccountTimelineQuery timeline,
        CancellationToken cancellationToken)
    {
        var id = new AccountId(accountId);

        if (!asOf.HasValue)
        {
            var view = await balance.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return view is null
                ? Results.NotFound(new { error = "account.not_found", accountId })
                : Results.Ok(view);
        }

        var asOfView = await timeline.GetAsOfAsync(id, asOf.Value, cancellationToken)
            .ConfigureAwait(false);
        return asOfView is null
            ? Results.NotFound(new { error = "account.not_found_at_time", accountId, asOf = asOf.Value })
            : Results.Ok(asOfView);
    }

    private static async Task<IResult> ListAccounts(
        IAccountBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var views = await query.ListAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(views);
    }

    public sealed record ReasonRequest(string Reason);

    private static async Task<IResult> FreezeAccount(
        Guid accountId,
        ReasonRequest body,
        [AsParameters] IdempotencyHeader idempotency,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var key = ParseIdempotencyKey(idempotency);
        var command = new FreezeAccountCommand(new AccountId(accountId), body.Reason, key);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> UnfreezeAccount(
        Guid accountId,
        [AsParameters] IdempotencyHeader idempotency,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var key = ParseIdempotencyKey(idempotency);
        var command = new UnfreezeAccountCommand(new AccountId(accountId), key);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> CloseAccount(
        Guid accountId,
        ReasonRequest body,
        [AsParameters] IdempotencyHeader idempotency,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var key = ParseIdempotencyKey(idempotency);
        var command = new CloseAccountCommand(new AccountId(accountId), body.Reason, key);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static IdempotencyKey ParseIdempotencyKey(IdempotencyHeader header)
    {
        if (string.IsNullOrWhiteSpace(header.Value))
        {
            throw new MissingIdempotencyKeyException();
        }
        return IdempotencyKey.Parse(header.Value);
    }
}

/// <summary>
/// Binds the <c>Idempotency-Key</c> header on a request via Minimal
/// API's <c>[AsParameters]</c> + <c>[FromHeader]</c> support.
/// </summary>
public sealed class IdempotencyHeader
{
    [Microsoft.AspNetCore.Mvc.FromHeader(Name = "Idempotency-Key")]
    public string? Value { get; set; }
}

public sealed class MissingIdempotencyKeyException : Exception
{
    public MissingIdempotencyKeyException()
        : base("Request is missing an 'Idempotency-Key' header.")
    {
    }
}
