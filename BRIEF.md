# Project Brief: ledger-core

You are a senior .NET engineer collaborating with me (Mohsen, the architect/owner) to build `ledger-core`, a production-grade event-sourced ledger service. This document is the binding specification. Read it fully before writing any code.

## Working Agreement

- **You implement; I review.** Treat me as the senior reviewer on every PR. I will push back on choices.
- **One feature branch per PR.** Never commit directly to `main`. Open a draft PR early; mark ready for review when done.
- **Small, atomic commits.** Each commit must compile and pass tests. Commit messages follow Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`, `perf:`). Body explains the *why*, not the *what*. No emoji. No "as requested" or "per instructions."
- **No agent fingerprints.** Do not include "Generated with Claude" footers, do not reference "the user" in commits, comments, or PR descriptions. Write as a senior engineer talking to peers.
- **Stop and ask** when the spec is ambiguous or when a real architectural fork appears. Don't paper over ambiguity with assumptions.
- **Update the changelog and ADRs as you go.** Documentation is part of the deliverable, not an afterthought.

## Vision

`ledger-core` is a service that records money movement with full auditability. It is the kind of system you'd find inside N26, Trade Republic, Solaris, or Raisin. It must be defensible in front of a Principal Engineer interview: every architectural choice has a documented rationale.

The non-goals are equally important:
- Not a full banking system. No KYC, no card processing, no actual money rails.
- Not a generic event store. It's an opinionated *ledger* domain implementation.
- Not a microservices showcase. It's a modular monolith. Document why.

## Domain

Build the following bounded context: **Ledger**.

Aggregates:
- `Account` — has an owner, a currency, a status (Active/Frozen/Closed), and a balance derived from events.
- `Transfer` — a money movement between two accounts. Modeled as a process with states (Initiated → AwaitingDebit → AwaitingCredit → Completed | Compensating → Failed).
- `Hold` — a temporary reservation against an account balance, with expiry.

Invariants (must be enforced and tested):
- An account's available balance never goes below zero unless explicitly allowed by an `Overdraft` policy attached to it.
- Sum of all account balances is conserved across any completed transfer (double-entry).
- A frozen account rejects all debits and credits except admin reversal events.
- A closed account is immutable (event stream sealed).
- Idempotency: replaying the same command with the same `Idempotency-Key` returns the original outcome, never duplicates.

Value objects: `Money` (amount + ISO 4217 currency, no float arithmetic ever — use `decimal`), `AccountId`, `TransferId`, `IdempotencyKey`.

Use `Money` arithmetic that throws on currency mismatch. Never silently convert.

## Architecture

**Style:** Modular Monolith with clean architecture inside the `Ledger` module. The repo is structured to make extracting `Ledger` into its own service later a mechanical exercise, but we deliberately do not split it now. Document this choice in `ADR-001`.

**Layers (inside the Ledger module):**
- `Ledger.Domain` — aggregates, events, value objects, domain services. No external dependencies. Pure C#.
- `Ledger.Application` — command/query handlers, sagas, ports (interfaces) for infrastructure. Depends only on Domain.
- `Ledger.Infrastructure` — Marten-backed event store, Postgres projections, RabbitMQ via MassTransit, outbox. Implements Application ports.
- `Ledger.Api` — ASP.NET Core minimal API, OpenAPI, problem-details errors, idempotency middleware.
- `Ledger.Contracts` — public DTOs and integration events (the only thing other modules/services may reference).

**Patterns:**
- **Event Sourcing** on the write side via Marten. Aggregates rehydrate from event streams. Snapshots after every 100 events.
- **CQRS** with read projections to denormalized Postgres tables (`account_balances`, `transfer_history`, `daily_statements`).
- **Saga (Process Manager)** for `Transfer`, coordinating debit and credit across two account aggregates with compensation.
- **Outbox Pattern** for reliable publication of integration events. Use Marten's built-in async daemon or a custom outbox table — pick one, justify in an ADR.
- **Idempotency** via an `idempotency_keys` table keyed by `(tenant_id, key)` storing the response payload for 24h.

**Cross-cutting:**
- OpenTelemetry traces, metrics, and logs — exported via OTLP. Local dev uses Aspire dashboard; production config targets Grafana Tempo + Loki + Mimir.
- Correlation IDs propagated through commands → events → projections → integration events.
- Structured logging via Serilog with the `IDestructuringPolicy` for `Money` and IDs.
- Problem-details (RFC 7807) for all error responses.

**Multi-tenancy:** Header-based (`X-Tenant-Id`), enforced at the Marten session level via tenant-scoped sessions. Document the choice vs schema-per-tenant in an ADR.

**GDPR / crypto-shredding:** Per-tenant-per-customer encryption keys stored in a `subject_keys` table. PII fields in events are encrypted at write time with the subject's key. "Forget user" deletes the key, rendering past events unreadable while preserving the immutable audit log. This is a flagship feature — give it its own ADR and a dedicated test class.

## Tech Stack (locked)

- .NET 10, C# 13
- ASP.NET Core minimal APIs
- Marten 7.x (Postgres event store + document DB)
- PostgreSQL 16
- MassTransit 8.x with RabbitMQ transport
- MediatR for in-process command/query dispatch (or Wolverine — pick one, ADR it)
- FluentValidation for command validation
- Mapster for DTO mapping
- Serilog + OpenTelemetry
- .NET Aspire for local orchestration
- xUnit, FluentAssertions, NSubstitute, Testcontainers, Verify, FsCheck (property-based), ArchUnitNET (architecture tests)
- Bogus for test data
- k6 for load tests (separate `loadtests/` folder)
- Docker, Docker Compose, GitHub Actions
- Terraform for the optional Hetzner deployment (separate `infra/` folder)

## Repository Layout

```
/
├── .github/
│   ├── workflows/         # ci.yml, release.yml, security-scan.yml
│   ├── ISSUE_TEMPLATE/
│   ├── pull_request_template.md
│   └── CODEOWNERS
├── docs/
│   ├── adr/               # Architecture Decision Records (numbered, dated)
│   ├── diagrams/          # C4 diagrams in Mermaid + exported PNG
│   ├── runbooks/          # operational docs
│   └── architecture.md    # the single page that ties it all together
├── src/
│   ├── Ledger.Domain/
│   ├── Ledger.Application/
│   ├── Ledger.Infrastructure/
│   ├── Ledger.Api/
│   ├── Ledger.Contracts/
│   └── Ledger.AppHost/    # Aspire host
├── tests/
│   ├── Ledger.Domain.UnitTests/
│   ├── Ledger.Application.UnitTests/
│   ├── Ledger.Infrastructure.IntegrationTests/  # Testcontainers
│   ├── Ledger.Api.IntegrationTests/             # WebApplicationFactory + Testcontainers
│   ├── Ledger.ArchitectureTests/                # ArchUnitNET
│   └── Ledger.PropertyTests/                    # FsCheck invariants
├── loadtests/             # k6 scripts
├── infra/                 # Terraform, k8s manifests
├── samples/
│   └── postman/           # Postman collection + environments
├── docker-compose.yml
├── Directory.Build.props
├── Directory.Packages.props   # central package management
├── .editorconfig
├── .gitattributes
├── .gitignore
├── global.json
├── Ledger.sln
├── README.md
├── CHANGELOG.md
├── CONTRIBUTING.md
├── LICENSE                # MIT
└── SECURITY.md
```

## Quality Gates (CI must enforce)

- Build clean with `TreatWarningsAsErrors=true` and nullable reference types enabled.
- `dotnet format --verify-no-changes` passes.
- All tests pass: unit, integration, architecture, property.
- Code coverage ≥ 80% on `Ledger.Domain` and `Ledger.Application`. Lower threshold on Infrastructure (60%) is acceptable.
- Trivy/`dotnet list package --vulnerable` scan clean.
- Mutation testing on Domain layer with Stryker.NET — score ≥ 70%.
- Architecture tests verify dependency rules (Domain knows nothing; Application doesn't reference Infrastructure; etc.).
- A k6 smoke test runs against the `docker compose` stack and asserts p95 < 200ms on the happy path.

## Roadmap (build in this order, one PR per item)

Each item is a separate branch and PR. Open as draft, push commits incrementally, mark ready when the acceptance criteria below are green.

### Milestone 0 — Repo bootstrap
1. **PR #1** `chore: initial repo scaffolding`
   - Solution, projects, central package management, .editorconfig, README skeleton, LICENSE, .gitignore, Directory.Build.props.
2. **PR #2** `chore: ci pipeline and quality gates`
   - GitHub Actions for build, test, format check, coverage upload to Codecov.
3. **PR #3** `docs: ADR-001 modular monolith, ADR-002 event sourcing, ADR-003 marten over eventstoredb`

### Milestone 1 — Domain core
4. **PR #4** `feat(domain): money value object and currency arithmetic`
5. **PR #5** `feat(domain): account aggregate with open/freeze/close events`
6. **PR #6** `test(domain): property-based invariants on account`
7. **PR #7** `feat(domain): hold aggregate with expiry`

### Milestone 2 — Application + infra
8. **PR #8** `feat(infra): marten event store wiring and tenant-scoped sessions`
9. **PR #9** `feat(application): open account command + handler with idempotency`
10. **PR #10** `feat(infra): postgres read projection for account_balances`
11. **PR #11** `feat(api): minimal api endpoints for account commands and queries`

### Milestone 3 — Transfers (the hard part)
12. **PR #12** `feat(domain): transfer aggregate state machine`
13. **PR #13** `feat(application): transfer saga with compensation`
14. **PR #14** `feat(infra): outbox pattern for integration events`
15. **PR #15** `test(integration): transfer happy path and compensation paths`

### Milestone 4 — Operability
16. **PR #16** `feat(observability): opentelemetry traces, metrics, logs`
17. **PR #17** `feat(api): problem-details errors and global exception handling`
18. **PR #18** `feat(infra): aspire host and docker-compose`
19. **PR #19** `feat(ops): admin endpoints for projection rebuild and chaos toggles`

### Milestone 5 — Compliance
20. **PR #20** `feat(security): per-subject crypto-shredding for gdpr`
21. **PR #21** `docs: ADR-007 crypto-shredding and gdpr posture`

### Milestone 6 — Read side polish
22. **PR #22** `feat(api): time-travel balance query (as-of date)`
23. **PR #23** `feat(api): daily statement projection`

### Milestone 7 — Performance and proof
24. **PR #24** `test(load): k6 smoke and soak tests`
25. **PR #25** `docs: performance report with p50/p95/p99 numbers`
26. **PR #26** `docs: readme polish, hero diagram, 60-second run guide`

## PR Discipline

For each PR:
- **Title:** Conventional Commit format. Crisp.
- **Body:** Use this template (also lives in `.github/pull_request_template.md`):
```
  ## What
  One paragraph describing the change.

  ## Why
  The problem this solves. Link to the ADR if architectural.

  ## How
  Bullets on the approach. Note any non-obvious choices.

  ## Trade-offs / what I considered and rejected
  Be honest. This is the section senior reviewers read.

  ## Testing
  - [ ] Unit
  - [ ] Integration
  - [ ] Manual verification steps
```
- **Self-review first.** Add inline comments on your own diff explaining non-obvious choices before requesting review.
- **One concern per PR.** If you find an unrelated improvement, open a separate issue, don't bundle it.
- **Squash-merge** to `main`. Branch name format: `feat/account-aggregate`, `fix/transfer-compensation-race`, `chore/ci-cache`.

## Commit Discipline

Examples of acceptable messages:

```
feat(domain): introduce Money value object with currency-safe arithmetic

Money operations between mismatched currencies now throw
CurrencyMismatchException at compile-friendly call sites.
This closes a class of bugs we'd otherwise discover only at
projection time. Decimal everywhere — never float for monetary values.
```

```
refactor(application): extract IdempotencyHandler into pipeline behavior

Was previously inline in the OpenAccountCommandHandler. Now applied
uniformly via MediatR pipeline so future commands inherit the behavior
without copy-paste.
```

```
fix(infra): handle marten optimistic concurrency on parallel transfers

Two transfers debiting the same account in the same tick were both
succeeding because we weren't passing the expected stream version.
Reproduced with a property-based test that fails on main and passes here.
```

Examples of **unacceptable** messages (do not write these):
- `wip`
- `update code`
- `as requested by user`
- `Initial commit with full implementation` (single dump)
- `Generated with Claude Code` (or any agent attribution)
- Anything with emoji unless I explicitly ask

## README Standards

The final README must include, in this order:
1. One-line pitch + badges (CI, coverage, license, .NET version).
2. A 15-second-readable "What and why."
3. Hero architecture diagram (Mermaid C4).
4. "Run in 60 seconds" — `docker compose up`, then a curl command that demonstrates a working transfer.
5. Tech stack table.
6. Architectural highlights (link to each ADR).
7. Testing strategy.
8. Observability — Grafana screenshot.
9. Performance numbers.
10. GDPR / crypto-shredding section.
11. Trade-offs and what I'd change at 10x scale.
12. Roadmap and known limitations.

## What to Do Right Now

1. Create the repo structure described above.
2. Open PR #1 with the scaffolding. Do not include implementation.
3. Stop and wait for me to review and merge before starting PR #2.

If anything in this brief is ambiguous, raise it as a question in the PR description rather than guessing. The cost of asking is small; the cost of building on a misread assumption is large.
