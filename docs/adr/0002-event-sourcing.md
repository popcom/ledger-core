# ADR-0002: Event sourcing for the Ledger write side

- Status: Accepted
- Date: 2026-04-29
- Deciders: Mohsen (architect), engineering team
- Tags: persistence, ledger, audit, cqrs

## Context

`ledger-core` is the system of record for money movement. The domain
mandates:

1. **Full auditability.** Every change to an account balance must be
   reconstructible from history. A regulator or a customer must be able
   to ask "why is the balance £42.17 on this date?" and get a precise
   answer that is identical no matter when the question is asked.
2. **Strong invariants.** Available balance never goes negative unless an
   `Overdraft` policy allows it. Sum of balances is conserved across a
   completed transfer. A frozen account rejects writes. A closed account
   is immutable.
3. **Idempotent commands.** A retried `OpenAccount` or `RequestTransfer`
   must yield the original outcome, not a duplicate.
4. **Time travel.** "What was the balance on 2026-03-31 23:59 UTC" is a
   first-class read query for reporting and statements.

The two natural persistence styles for this domain are:

- **State-stored**: Postgres rows for `accounts`, `transfers`, `holds`,
  with a parallel `audit_log` table written on every change.
- **Event-sourced**: an immutable event stream per aggregate as the
  source of truth; current state is a fold over the stream; read models
  are projections.

State-stored designs work, but the audit log becomes a second source of
truth that has to be kept consistent with the row model. Time travel
becomes "replay the audit log into a snapshot table" — which is event
sourcing under a different name and without the tooling. Edge cases
where the row update commits and the audit insert fails are exactly the
kinds of bugs the brief refuses to ship.

## Decision

The Ledger write side is **event sourced**.

- Each aggregate (`Account`, `Transfer`, `Hold`) owns an event stream
  identified by its aggregate id. Commands produce events; events are
  the only way to change state.
- State is rehydrated by folding the stream. Snapshots are taken every
  100 events (see ADR-003 for why Marten's snapshotting fits) so
  long-lived accounts do not pay an O(n) cost on every command.
- Read models are denormalised projections built from the same events:
  `account_balances`, `transfer_history`, `daily_statements`, etc. The
  brief's "balance as-of date" query becomes a fold up to a timestamp
  rather than a special-case query.
- Idempotency lives outside the stream: a `(tenant_id, idempotency_key)`
  table stores the original response payload for 24 hours, returning it
  verbatim on a replay. This separates "did the command run?" from "what
  did the aggregate decide?" cleanly.
- Events are immutable. Schema evolution uses up-casters — never edits
  to historical events. PII fields are encrypted at write time with a
  per-subject key (see future ADR-007 on crypto-shredding).

## Consequences

Positive:

- The audit log *is* the source of truth, not a copy of it. There is one
  thing to keep consistent.
- Time-travel queries are natural: a balance on a date is a fold up to
  the last event before that date. The brief's "as-of" endpoint becomes
  a small read model rather than a new schema.
- Sagas (the Transfer process manager) compose with the rest of the
  system: a saga is a stream like any other.
- GDPR via crypto-shredding fits cleanly: deleting the per-subject key
  renders the encrypted PII fields unreadable while the immutable audit
  log structure is preserved.

Negative:

- Higher up-front complexity than a row-based design. Engineers need to
  understand aggregate boundaries, optimistic concurrency, projection
  rebuilds, and event versioning.
- Read queries cannot hit aggregate state directly; they go through
  projections, which are eventually consistent. We accept this:
  projection lag is bounded by Marten's async daemon and the brief's
  SLOs are written with that lag in mind.
- Schema changes to events require disciplined up-casting. We mitigate
  by keeping events small, treating the `Contracts` shape as the public
  surface, and snapshot-testing the JSON shape with Verify.

Risks:

- **Projection rebuild cost on a large stream.** Mitigation: snapshots
  every 100 events, plus an admin endpoint (PR #19) to rebuild a single
  projection in the background.
- **Optimistic concurrency conflicts under load.** Mitigation: model
  Transfer as a saga so contention on a single account is serialised at
  the aggregate, not at the API layer; load tests in PR #24 establish
  the actual ceiling.
- **Event-store coupling.** Picking a specific store (Marten — see
  ADR-003) is itself a risk. The Application layer talks to a port,
  not Marten directly, so swapping the store is mechanical.

## Alternatives considered

- **State-stored with parallel audit log.** Rejected: two sources of
  truth that have to be kept consistent, and time-travel queries become
  a second projection system reinvented from scratch.
- **State-stored with database CDC (logical replication or
  Debezium).** Rejected for this domain: the audit log derived from CDC
  is opaque (row diffs, not domain events), and deriving "why" from
  it requires re-implementing the domain rules outside the aggregate.
- **Hybrid: event-sourced aggregates, but read models also store full
  rows alongside.** Rejected as a default: it doubles the write path and
  reintroduces the consistency problem we are trying to remove. Specific
  high-throughput projections may store snapshots, but those are still
  derived from events.

## References

- Greg Young, "Event Sourcing" (talks and writings, 2008-onward).
- Vaughn Vernon, *Implementing Domain-Driven Design*, chapter on
  aggregates and events.
- Martin Fowler, "Event Sourcing" article, for the "fold over events"
  framing this ADR uses.
- Marten documentation on async projections and snapshotting (see
  ADR-003 for the store choice).
