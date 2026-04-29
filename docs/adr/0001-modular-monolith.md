# ADR-0001: Modular monolith over microservices

- Status: Accepted
- Date: 2026-04-29
- Deciders: Mohsen (architect), engineering team
- Tags: architecture, deployment, modules

## Context

`ledger-core` is the system of record for money movement. It owns three
aggregates today (Account, Transfer, Hold) and a small number of read
projections. The team is small, the deployment target is a single Postgres
plus RabbitMQ, and the domain is cohesive: every command and every
projection touches the same set of aggregates.

A microservices layout would split the Ledger into separate services per
aggregate (or per bounded context once more contexts are added). The cost
of that split shows up immediately:

- Distributed transactions to keep `Account` and `Transfer` consistent,
  replacing in-process aggregate boundaries with retry-and-compensate
  protocols across network calls.
- Schema duplication and event-version drift between services.
- Developer setup growing from "one solution, one Postgres" to a fleet of
  containers, each with its own migration story.
- Observability and debugging cost going from a single trace to
  cross-service correlation that has to be designed before it works.

None of those costs buy us anything yet: there is no team-scaling pressure
to split ownership, and there is no scale pressure that single-process
deployment cannot meet.

At the same time, the design has to keep the option to extract individual
modules into their own services later — funding round, regulatory split,
new bounded contexts — without rewriting the call sites.

## Decision

`ledger-core` is built as a **modular monolith**.

- One deployable (`Ledger.Api`) and one event store schema, but the code
  is partitioned into modules with explicit boundaries:
  - `Ledger.Domain`, `Ledger.Application`, `Ledger.Infrastructure`,
    `Ledger.Api`, and `Ledger.Contracts` for the Ledger module.
  - Future modules (e.g. `Statements`, `Reconciliation`) will follow the
    same five-project shape under their own namespace.
- Inter-module communication goes through `*.Contracts` only. Other
  modules may not reference a module's `Domain`, `Application`, or
  `Infrastructure` projects. This is enforced at compile time by project
  references and at CI time by ArchUnitNET tests.
- Cross-module side effects flow through integration events on the
  message bus, never via direct method calls into another module's
  internals. Even though the bus is in-process today (via MassTransit's
  in-memory transport for fast paths and RabbitMQ for durable ones), the
  call shape is identical to the eventual cross-service variant.
- Each module owns its own schema namespace inside the shared Postgres.
  Foreign keys never cross module boundaries.

These constraints make extraction a mechanical exercise: lift the
module's projects, point its `Contracts` at the new service's URL or
broker, copy its schema into a dedicated database. Nothing in the call
sites has to change.

## Consequences

Positive:

- Single repo, single solution, single `docker compose up`. Onboarding
  cost stays low while the team is small.
- One transactional boundary inside a module — the aggregate. Most
  consistency questions are settled by the database, not by sagas.
- Tests are fast: in-process WebApplicationFactory + Testcontainers
  Postgres covers integration without standing up a fleet.
- Refactors that span modules are easy: rename across boundaries in one
  commit instead of negotiating a breaking change across services.

Negative:

- A misuse of internal types from another module compiles unless the
  architecture tests catch it. This is mitigated by `ArchUnitNET` tests
  failing CI on cross-module references.
- A single deployment unit means a hot path in one module can starve
  others for CPU. We accept this in exchange for the simpler topology;
  if it becomes a real problem, that *is* a signal to extract.
- The team has to actively maintain module boundaries — code review and
  architecture tests are the two enforcement points.

Risks if we got this wrong:

- "Big ball of mud" if the boundaries erode. Counter: every PR touches a
  single module unless it changes a `Contracts` package; cross-module
  changes need an explicit reviewer note.
- Lock-in to in-process: counter is the rule that integration events
  flow over the message bus from day one, even when both producer and
  consumer live in the same process.

## Alternatives considered

- **Microservices per aggregate.** Rejected: see Context. The cost is real
  and immediate, the benefit is hypothetical and far away.
- **Single-project monolith (no module separation).** Rejected: collapsing
  Domain/Application/Infrastructure produces fast prototypes but makes
  later extraction a rewrite. The cost of starting modular is small (a
  few project files) compared to the cost of becoming modular later.
- **Module per assembly with shared database tables.** Rejected: shared
  tables mean schema changes ripple across modules, defeating the point.
  Schema namespaces per module preserve independent evolution.

## References

- The `BRIEF.md` for the project mandates a modular monolith with a
  dedicated `Ledger` module; this ADR records the rationale.
- Sam Newman, *Building Microservices*, Chapter 4: "When Should I Use
  Microservices?" — his answer ("not yet") is the same as ours.
- Simon Brown's "Modular monolith" talks for the project structure that
  makes extraction a mechanical operation rather than a rewrite.
