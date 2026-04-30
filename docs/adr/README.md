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
| [0004](0004-outbox-pattern.md)          | Custom outbox table over async daemon       | Accepted |
| [0005](0005-multi-tenancy.md)           | Conjoined tenancy over schema-per-tenant    | Accepted |
| [0006](0006-mediatr-over-wolverine.md)  | MediatR for in-process dispatch             | Accepted |
| [0007](0007-crypto-shredding.md)        | Per-subject AES-GCM-256 crypto-shredding    | Accepted |
