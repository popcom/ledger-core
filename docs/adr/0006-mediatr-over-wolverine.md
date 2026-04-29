# ADR-0006: MediatR for in-process command/query dispatch

- Status: Accepted
- Date: 2026-04-29
- Deciders: Mohsen (architect), engineering team
- Tags: dispatch, mediatr, wolverine, application-layer

## Context

The Application layer needs an in-process dispatcher for commands,
queries, and pipeline behaviors (validation, idempotency, future
logging/tracing decorators). The brief explicitly leaves the choice
between **MediatR 12.x** and **Wolverine** to an ADR.

The two options sit at very different points on the
"do-one-thing-well" axis:

- **MediatR** is a small, single-purpose mediator. Pipeline behaviors
  are first-class. There is no built-in messaging, no built-in saga
  support, no scheduling, no transport. Anything beyond in-process
  request/response is the consumer's job.
- **Wolverine** is a full message bus that *also* handles in-process
  dispatch. It does sagas, scheduling, durable inbox/outbox, and
  RabbitMQ/Kafka/Azure Service Bus transports out of the box. Its
  dispatch surface (the `IMessageBus`) and Marten integration are
  excellent.

If we picked Wolverine, much of what later PRs hand-roll
(MassTransit + outbox in PRs #14, sagas in PR #13) would be subsumed
by the framework. That is a real benefit. The cost is two-fold:

1. Wolverine is opinionated. Once you adopt its message bus, you adopt
   its handler discovery, its conventions, its lifecycle. That is
   fine when you align with the conventions and friction when you do
   not — and the brief's stack pin includes MassTransit + RabbitMQ
   as the messaging substrate, which competes with Wolverine's
   transports.
2. The brief's stack table lists "MediatR for in-process command/query
   dispatch (or Wolverine — pick one, ADR it)". Picking Wolverine is
   not just a dispatcher swap; it pulls Wolverine into the
   integration-event story too, which then forks from the brief's
   MassTransit choice.

## Decision

`ledger-core` uses **MediatR 12.x** for in-process command/query
dispatch and pipeline behaviors. The integration-event story stays
with **MassTransit + RabbitMQ** as the brief specifies.

- `AddLedgerApplication` registers MediatR via assembly scan and wires
  two pipeline behaviors in order:
  1. `ValidationPipelineBehavior` — runs every registered FluentValidation
     `IValidator<T>` and short-circuits with
     `CommandValidationException` on failures (cheap, deterministic).
  2. `IdempotencyPipelineBehavior` — checks `IIdempotencyStore` for a
     cached response keyed on `(tenant, IdempotencyKey)` and returns
     it verbatim on a hit; otherwise runs the handler and writes the
     response.
- Commands that should be deduplicated implement
  `IIdempotentRequest<TResponse>`, which carries the
  `IdempotencyKey`. Commands that should not — queries, admin
  operations — do not.

## Consequences

Positive:

- MediatR is a small dependency. The pipeline-behavior shape is the
  exact API surface command handlers need; no message-bus concepts
  leak into command handling.
- MassTransit can stay focused on what it is good at: durable
  cross-process messaging on RabbitMQ.
- The two pipelines (in-process via MediatR, cross-process via
  MassTransit) sit at different layers; the boundary is the outbox
  (PR #14). Each can evolve independently.
- Replacing MediatR later with Wolverine, or with hand-rolled
  dispatch (a `class CommandBus { public Task Send<T>(T cmd) ... }`)
  is mechanical because every handler is a plain
  `IRequestHandler<,>` implementation.

Negative:

- We do not get Wolverine's saga or outbox plumbing. Both are
  required by the brief in PRs #13 and #14, and we have to provide
  them ourselves (Marten async daemon for the saga, Marten outbox
  for integration events). That is more code than depending on a
  framework, and the trade-off is documented in ADR-0004 (outbox).
- Pipeline behaviors are registered globally via open generics, so
  the *order* matters and is not visible at the call site. Mitigated
  by registering in a single composition root with comments.

Risks:

- **MediatR licensing.** MediatR 12.x is MIT today; the project's
  long-term licensing has been discussed publicly. If the licence
  changes in a way that does not work for the project, we adopt the
  fork or hand-roll. The dispatch surface is small enough that the
  swap is a focused PR.

## Alternatives considered

- **Wolverine.** Strong technical fit; would simplify PRs #13, #14,
  and #16 considerably. Rejected because the brief locks the
  cross-process messaging to MassTransit + RabbitMQ; adopting
  Wolverine's bus alongside MassTransit would mean two messaging
  frameworks coexisting. If we revisit, the natural path is
  Wolverine *replacing* both MediatR and MassTransit, which is a
  bigger architectural change than this ADR is sized for.
- **Hand-rolled dispatch.** A 50-line `CommandBus` that walks
  pipelines manually. Rejected because the cost is small but the
  benefit is too — MediatR already implements exactly the pattern
  we want.
- **No dispatcher; controllers call handlers directly.** Rejected
  because pipeline behaviors (validation, idempotency, future
  observability) need a uniform application point. Without a
  dispatcher, every endpoint reimplements the cross-cutting logic.

## References

- MediatR: https://github.com/jbogard/MediatR
- Wolverine: https://wolverinefx.net/
- The brief, "Tech Stack" section, which lists both options with a
  pick-one instruction.
- ADR-0004 (outbox) and the brief's PRs #13/#14 for the saga and
  outbox responsibilities Wolverine would have absorbed.
