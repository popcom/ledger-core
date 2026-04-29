# ADR-0003: Marten over EventStoreDB as the event store

- Status: Accepted
- Date: 2026-04-29
- Deciders: Mohsen (architect), engineering team
- Tags: persistence, marten, eventstoredb, postgres

## Context

ADR-0002 commits us to event sourcing. The runtime question is *where*
the events live. The realistic options for a .NET service in 2026 are:

1. **Marten** — event store and document database on top of PostgreSQL.
   Native .NET; same database powers projections; transactional
   semantics across events and projections; async daemon for projection
   rebuilds.
2. **EventStoreDB (Kurrent)** — purpose-built event database with
   persistent subscriptions, projections, and an HTTP/gRPC API.
3. **Roll-your-own on Postgres** — `events` table with append-only
   inserts, your own optimistic concurrency, your own projections.
4. **Kafka as the event log** — Kafka topics as the immutable log,
   external state in Postgres or RocksDB.

The decision is dominated by three properties of *this* service:

- The Ledger needs **transactional consistency between an aggregate
  command and any projection that fronts the API for the immediate read
  back** (e.g. "open account, then GET account"). Eventual consistency
  for everything is an option, but it pushes complexity into every
  endpoint.
- Operationally, the team is small. Standing up a second specialised
  data store doubles the on-call surface. PostgreSQL is already the
  choice for read projections, idempotency keys, and outbox.
- The `Money` and `Account` aggregates have small events (<1 KB) and
  modest fan-out (one stream per account, sub-second latency budgets).
  None of the choices struggle at these volumes; we are choosing on
  operational fit, not throughput ceiling.

## Decision

The Ledger event store is **Marten 7.x on PostgreSQL 16**.

- Marten provides the event store, the document store for snapshots,
  and the projection daemon. All three share the same Postgres
  connection and transaction.
- The `Application` layer talks to ports (`IEventStore`, `IUnitOfWork`,
  …) defined in terms of the domain. Marten is a `Ledger.Infrastructure`
  detail. If we ever swap stores, the Application code does not change.
- Snapshots are written every 100 events (Marten's `SnapshotEvery`
  policy) so long-lived account streams do not fold from zero on every
  command.
- Inline projections back the read endpoints that need
  read-after-write within the same request (e.g. an idempotent
  `OpenAccount` returning the new account's view). Async projections
  back everything else (statements, search-style queries) and run on
  Marten's daemon.
- Multi-tenancy uses Marten's tenant-scoped sessions, keyed on the
  `X-Tenant-Id` request header. ADR-0005 will document the trade-off
  against schema-per-tenant.

## Consequences

Positive:

- One database to operate, one backup story, one connection pool.
  Migrations and schema changes flow through a single tool chain.
- Atomic "append events + update inline projection" in the same
  transaction — no race window between writing the stream and writing
  the read model that endpoints serve immediately.
- First-class .NET API. Aggregates can be plain C# records; events are
  serialised as JSONB; projection definitions are C# classes. No
  client driver to keep version-aligned with a remote server.
- Marten's async daemon gives us projection rebuilds with replay
  control, which is what the brief's "admin endpoint to rebuild a
  projection" (PR #19) is built on top of.
- Same Postgres can host the outbox table (ADR-0004) so the brief's
  outbox pattern is also transactional with the events.

Negative:

- Marten is opinionated. Some patterns (e.g. cross-aggregate
  transactions, multi-stream projections) require careful use of the
  daemon; the team needs Marten-specific knowledge.
- Event-store throughput on Postgres is bounded by Postgres write
  performance. At our volume this is not the bottleneck; if it becomes
  one, partitioning by tenant or extracting hot streams to a dedicated
  store is the next step.
- We are coupling our event store to Postgres' availability. We accept
  this — the service depends on Postgres for projections and idempotency
  too, so adding a separate event-store dependency would *increase* the
  failure surface, not decrease it.

Risks:

- **Marten major-version upgrades.** Mitigation: pin via
  `Directory.Packages.props`, and gate upgrades on a green `dotnet
  list package --vulnerable` plus a full integration suite (PR #15
  onwards).
- **Locked into a specific async-daemon model.** Mitigation: keep
  projection code small and free of Marten-specific abstractions where
  possible; the daemon is plumbing, not domain.

## Alternatives considered

- **EventStoreDB (Kurrent).** Purpose-built and excellent at what it
  does. Rejected because it would add a second database with its own
  HA story, its own auth model, its own backup, and its own monitoring.
  The brief's operational target (one Postgres + one RabbitMQ) is the
  right size; adding a third stateful dependency is not earned by
  current scale or feature requirements. If we hit a write-throughput
  ceiling on Postgres that partitioning cannot resolve, EventStoreDB
  becomes the natural extraction target.
- **Roll-your-own events table.** Rejected: rebuilding optimistic
  concurrency, snapshotting, projection daemons, and tenant scoping
  is months of work that Marten ships in the box.
- **Kafka as the log, Postgres as state.** Rejected: Kafka is a fit
  for cross-system *integration* events (and we use RabbitMQ for that
  via MassTransit), but using it as the source of truth for aggregate
  state forfeits the transactional "events + projection" guarantee
  Marten gives us. The integration-event story stays separate; that
  is the outbox pattern, ADR-0004.

## References

- Marten documentation: https://martendb.io/
- Greg Young on event sourcing (see ADR-0002 references).
- The brief explicitly locks the stack to Marten + Postgres; this ADR
  captures why that is a defensible default rather than a dictate.
