# Architecture Decision Records

Architectural choices that shape `ledger-core` are recorded here as ADRs.
The format is loosely based on
[Michael Nygard's template](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions);
each record is short, dated, and immutable once accepted. Superseding an ADR
means writing a new one that links back to the one it replaces — never
silently editing the original.

| #     | Title                                       | Status   |
| ----- | ------------------------------------------- | -------- |
| [0001](0001-modular-monolith.md)        | Modular monolith over microservices         | Accepted |
| [0002](0002-event-sourcing.md)          | Event sourcing for the Ledger write side    | Accepted |
| [0003](0003-marten-over-eventstoredb.md)| Marten over EventStoreDB as the event store | Accepted |

ADRs to be written as the relevant code lands:

- ADR-004 outbox: Marten async daemon vs custom outbox table (PR #14).
- ADR-005 multi-tenancy: header-driven tenant-scoped sessions vs schema-per-tenant (PR #8).
- ADR-006 in-process dispatch: MediatR vs Wolverine (PR #9).
- ADR-007 crypto-shredding posture (PR #21).
