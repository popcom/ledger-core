# ADR-0004: Custom outbox table over Marten async daemon

- Status: Accepted
- Date: 2026-04-30
- Deciders: Mohsen (architect), engineering team
- Tags: outbox, marten, integration-events, masstransit

## Context

The brief mandates the outbox pattern for reliable publication of
integration events: a domain command must either record both the
internal events *and* the integration-event commitment, or neither.
The two ways Marten supports this in 2026 are:

1. **Marten's async daemon as the outbox.** Configure async
   projections that listen to event streams and emit integration
   events through a side effect on a publisher. The daemon owns
   ordering and offset checkpointing.
2. **A custom outbox table** persisted in the same Postgres database
   as the events. Command handlers `Store(outboxRow)` in the same
   transaction as the events; a hosted publisher polls the table
   and ships rows to the message bus, marking them published.

The trade-offs:

- The async daemon is elegant when integration events map 1:1 onto
  domain events — no extra row, no extra publisher, no extra
  document type. It is harder to use when the integration shape
  differs (e.g. `TransferCompleted` is a single integration event
  but corresponds to `TransferCreditConfirmed` + `TransferCompleted`
  in the stream), or when multiple domain events conditionally roll
  up into one integration event (a saga that completes only after
  N steps).
- The custom outbox table makes the integration-event boundary
  explicit. Command handlers (or sagas) decide *when* to enqueue,
  and the row carries everything the publisher needs (tenant id,
  event type, payload, attempts) without the publisher reaching
  back into the event store. The cost is one extra table and a
  hosted publisher loop.

The brief's integration-event shapes (`AccountOpenedIntegrationEvent`,
`TransferCompletedIntegrationEvent`, `TransferFailedIntegrationEvent`)
do not map 1:1 onto stream events: they are saga outcomes and
contract-stable shapes that may not change when an internal event
record gets a new field. The async daemon would be possible, but it
would put projection-ish code in the path of integration events
that are conceptually at a different layer than read models.

## Decision

`ledger-core` uses **a custom outbox table** persisted in the same
Postgres database as the events.

- `IOutbox` (Application port) exposes a single `EnqueueAsync` that
  command handlers and sagas call inside their handler. The Marten
  implementation writes an `OutboxMessage` row in a
  `LightweightSession` scoped to the current tenant; the row commits
  with the rest of the handler's writes.
- `IOutboxTransport` is the pluggable shipping mechanism. The
  default is `LoggingOutboxTransport` so the outbox can run in CI
  without RabbitMQ in scope. The MassTransit transport lands in the
  PR that brings the docker-compose stack online.
- `OutboxPublisher` is a `BackgroundService` that polls every
  2 seconds, pulls up to 50 unpublished rows ordered by
  `EnqueuedAt`, hands each to the transport, and marks the row
  published with the wall-clock timestamp. Failures bump the
  attempts counter and store `LastError`; the publisher does not
  block on a single bad row.

## Consequences

Positive:

- Atomic commit of "domain change happened" and "integration event
  promised". Either both land or neither does, because both are
  rows in the same Postgres transaction.
- Saga shape stays explicit. The `InitiateTransferSaga` decides to
  enqueue the integration event after `TransferCompleted`, not as a
  side effect of any individual stream event. Future sagas inherit
  the same call site.
- Transport independence. Today the transport is a logger; tomorrow
  it is MassTransit on RabbitMQ; next year it could be Kafka. The
  outbox does not change.
- Tenant isolation falls out for free — `OutboxMessage` rows carry
  `TenantId`; Marten's conjoined tenancy filters them on read; the
  publisher fans out per tenant if it ever needs to.

Negative:

- An extra table and an extra hosted service. Both are small, but
  they exist.
- Polling latency: in the worst case an integration event waits
  ~2 seconds before publish. Acceptable for the current SLOs;
  tunable down to ~250ms before the polling overhead bites.
- The publisher is single-process today; horizontal scale-out
  requires a row-level lock or a leader election. Acceptable
  because the API is single-process at the brief's deployment
  target; a row lock can be added in a follow-up if/when we run
  multiple replicas.

Risks:

- **Duplicate publishes on transport failure between
  `transport.PublishAsync` returning success and the row being
  marked published.** Mitigated by making integration-event
  consumers idempotent (every event carries `EventId`); MassTransit
  consumer-side outbox handles the rest.
- **Drift between domain event and integration event.** Mitigated
  by keeping integration events small, contract-stable, and
  derived in the saga rather than in the projection.

## Alternatives considered

- **Marten's async daemon.** Cleaner when integration events map 1:1
  onto domain events. Rejected because the Ledger's integration
  events are saga outcomes that do not match individual stream
  events; the daemon would either need a custom projection that
  emits side effects (which is what we are building anyway, with
  more ceremony) or tightly couple the integration shape to the
  domain shape.
- **MassTransit's transactional outbox.** First-class support and
  used widely. Rejected because it ties the outbox to MassTransit's
  schema and requires the bus to be present even in CI lanes that
  do not need RabbitMQ. Our transport-agnostic abstraction lets
  tests run without a transport, and lets us swap MassTransit
  implementation details without touching the saga code.
- **Kafka-as-the-outbox.** Use a Kafka topic as the durable log
  rather than a Postgres table. Rejected: Kafka is a fit for
  cross-system integration *transport*, not for "this is what the
  domain promised to publish". Adding it as a dependency for the
  outbox doubles the operational surface; we already use Postgres
  for events, projections, idempotency, and now the outbox.

## References

- Chris Richardson, "Pattern: Transactional outbox" (microservices.io).
- Marten async daemon documentation.
- MassTransit transactional outbox docs.
- ADR-0003 (Marten on Postgres) — the database choice that makes the
  outbox a small extension rather than a new system.
