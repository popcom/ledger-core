# ADR-0005: Multi-tenancy via conjoined tenant id, not schema-per-tenant

- Status: Accepted
- Date: 2026-04-29
- Deciders: Mohsen (architect), engineering team
- Tags: tenancy, marten, postgres, isolation

## Context

`ledger-core` is multi-tenant from day one — each customer/operator
("tenant") sees only their own accounts, transfers, and statements.
The brief mandates header-based tenant resolution
(`X-Tenant-Id`) and tenant-scoped Marten sessions; this ADR records
*how* the storage layer implements that separation, of three plausible
options.

The realistic choices for a Marten-on-Postgres event store are:

1. **Conjoined tenancy.** One schema, every event/document table has
   a `tenant_id` column, every read and write filters on it. Marten
   has a first-class `TenancyStyle.Conjoined` mode for exactly this.
2. **Schema-per-tenant.** A separate Postgres schema per tenant, all
   carrying the same tables. Sessions are routed at startup by the
   tenant resolver.
3. **Database-per-tenant.** A separate database (sometimes a separate
   server) per tenant.

Cost shape changes the answer:

- Operationally, schema-per-tenant adds one DDL fan-out per tenant
  (schema creation, migrations, index changes), and projection
  rebuilds need a list of all schemas to walk. Database-per-tenant
  multiplies that by also adding per-tenant connection pooling and
  per-tenant backup/restore. Both are real costs paid every time the
  schema changes.
- Conjoined adds runtime cost (one extra column in every WHERE clause)
  but no operational fan-out: one schema, one migration, one daemon.
- For the team size and tenant count this product is sized for
  (single-digit to low-hundreds), the runtime cost of the
  `tenant_id` filter is invisible compared to the I/O of reading the
  event itself; the operational cost of fan-out is not.

The decision is also constrained by what Marten makes easy.
Conjoined is a one-line setting (`TenancyStyle.Conjoined` plus
`Policies.AllDocumentsAreMultiTenanted`). Schema-per-tenant requires
either a custom `IDocumentSessionFactory` per tenant or a more
involved tenancy router; achievable, but a meaningful chunk of
infrastructure code that adds nothing the brief asks for.

## Decision

`ledger-core` uses **conjoined tenancy via Marten's
`TenancyStyle.Conjoined`**.

- Every event-store table and every projection table carries a
  `tenant_id` column.
- `MartenAggregateRepository` opens sessions via
  `IDocumentStore.LightweightSession(tenantId, …)`, where `tenantId`
  is taken from the request-scoped `ITenantContext`. Marten injects
  the filter on every read and the column on every write; we do not
  hand-craft `WHERE tenant_id = ?` clauses.
- `Policies.AllDocumentsAreMultiTenanted()` makes the policy global;
  forgetting to mark a new document type as tenant-scoped would
  surface at startup, not on the first cross-tenant leak.
- The Api edge resolves the tenant from the `X-Tenant-Id` header
  (PR #11) into a per-request scoped `ITenantContext`. Background
  jobs and tests use `StaticTenantContext`/`AmbientTenantContext`.

## Consequences

Positive:

- One schema, one migration story, one projection daemon. New
  tenants are a row, not a deployment step.
- Marten enforces the filter for us. Every aggregate-load and every
  projection query is tenant-scoped without each call site having to
  remember.
- Architecturally cheap to extract a tenant later: the same code
  reads from `tenant_id = X` regardless of whether `X` lives in a
  conjoined table or its own database. Migrating a single
  high-volume tenant to its own database is a `pg_dump --schema`
  away.
- Connection pooling is shared across tenants — no per-tenant
  reservation pinning RAM in idle pools.

Negative:

- **Indices have to include `tenant_id` to be selective.** Marten
  generates them correctly out of the box, but anyone hand-rolling a
  query needs to remember. Mitigated by routing every read through
  the repository abstraction.
- **A bug that drops the filter leaks across tenants.** Marten's
  policy makes that hard to do silently — sessions opened without a
  tenant id throw at the conjoined boundary. Belt-and-braces: the
  integration tests in PR #15 will assert visibility isolation
  between tenants.
- **Backup/restore is at the schema, not the tenant level.** "Restore
  customer X to a point in time" requires a conjoined-aware tool.
  Acceptable trade-off — a tenant-specific point-in-time restore is
  an edge case in this product, and the audit log lets us reconstruct
  state forward from any earlier known-good snapshot.

Risks:

- **Hot tenant noisy-neighbour.** A high-volume tenant can starve
  others of write throughput on the shared event-store stream. If we
  hit that ceiling, the extraction path is documented above; we do
  not pay for it now.

## Alternatives considered

- **Schema-per-tenant.** Strong physical isolation; hardest
  operational story. Rejected: every schema change becomes a
  scripted fan-out, projection rebuild needs to walk N schemas, and
  the brief's tenant count/team size does not justify the overhead.
  Adopt later only if a regulator requires it for a specific tenant.
- **Database-per-tenant.** Strongest isolation; worst operational
  story. Rejected for the same reasons as schema-per-tenant, plus
  per-tenant connection pools and per-tenant backups. Reasonable
  destination for a single tenant that has graduated out of the
  shared store.
- **Application-level filtering only.** Adding `tenant_id` columns
  but applying the filter in handlers, not at the session level.
  Rejected: it relies on every developer remembering the filter on
  every query, which is exactly the kind of safety net the type
  system should provide. Marten's conjoined tenancy is essentially
  the same idea wired in at the right layer.

## References

- Marten documentation, "Multi-Tenancy with Marten"
  (`TenancyStyle.Conjoined`, `AllDocumentsAreMultiTenanted`).
- The brief's "Multi-tenancy" section, which mandates header-driven
  tenant resolution and tenant-scoped sessions.
