# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial repository scaffolding: solution, project layout for the Ledger
  modular monolith (Domain, Application, Infrastructure, Api, Contracts,
  AppHost), test project layout (unit, integration, architecture, property),
  central package management, shared build properties, `.editorconfig`,
  `.gitignore`, `.gitattributes`, MIT license, and README skeleton.
- Continuous integration pipeline (`.github/workflows/ci.yml`): restore,
  format check (`dotnet format --verify-no-changes`), Release build, test
  with OpenCover coverage collection, test/coverage artifact upload, and
  Codecov upload. NuGet cache keyed on `Directory.Packages.props`, project
  files, and `global.json` keeps subsequent runs fast.
- Security scan workflow (`.github/workflows/security-scan.yml`): runs
  `dotnet list package --vulnerable --include-transitive` and a Trivy
  filesystem scan (CRITICAL/HIGH, ignore-unfixed) on every push, every PR,
  and a weekly schedule, uploading SARIF to GitHub code scanning.
- `codecov.yml` with per-layer coverage targets (Domain/Application 80%,
  Infrastructure 60%); thresholds are informational until Milestone 1
  lands real code to measure.

### Changed

- `Directory.Build.props`: dialled `AnalysisMode` from `AllEnabledByDefault`
  to `Recommended`. The brief mandates `TreatWarningsAsErrors` and nullable;
  the most aggressive analyzer mode pulls in CA rules that fight empty
  scaffolding without buying real safety yet. Re-evaluate as code lands.
- `Ledger.Api/Program.cs`: extracted the `WebApplicationFactory` marker
  partial class into `PublicApiMarker.cs`. The previous block-scoped
  namespace inside `Program.cs` conflicted with the file-scoped namespace
  rule that `dotnet format --verify-no-changes` now enforces.

### Documentation

- ADR-0001: modular monolith over microservices.
- ADR-0002: event sourcing for the Ledger write side.
- ADR-0003: Marten over EventStoreDB as the event store.
- `docs/adr/README.md`: index of accepted ADRs and the queue of ADRs
  scheduled to land alongside the code they document.

### Domain

- `Money` value object with currency-safe arithmetic. Backed by `decimal`
  (never `float`/`double`); operators throw `CurrencyMismatchException`
  rather than silently converting; equality, comparison, negation, and
  scalar multiply/divide are first-class.
- `Currency` value object: ISO 4217 alphabetic-code parser, canonicalised
  to upper case, with `Eur`/`Gbp`/`Usd`/`Chf` constants for tests and
  samples.
- `.editorconfig` naming rules split into static-readonly (PascalCase),
  const (PascalCase), and instance (`_camelCase`) families. The previous
  single rule forced underscore prefixes onto static readonly fields,
  which fights idiomatic .NET style.
- `Account` aggregate with the `open / freeze / unfreeze / close` event
  family plus `credit / debit` movements. State is folded from a
  sequence of `AccountEvent`s; commands raise events and update state
  in lockstep. Invariants enforced and tested: balance never goes
  negative (no overdraft policy yet), frozen accounts reject debits and
  credits, closed accounts are terminal, currency mismatches throw,
  non-positive credits/debits throw, close-with-non-zero-balance throws.
- `AccountId` strongly-typed identifier, `AccountStatus` enum,
  `DomainException` base with a stable `Code` for the future
  problem-details mapping (PR #17), and the `AccountException` family
  (already-open, not-open, frozen, not-frozen, closed,
  insufficient-funds).

### Tests

- FsCheck property-based invariants on the `Account` aggregate over
  random sequences of valid commands: replay equivalence (folding the
  pending events into a fresh aggregate reproduces the same state),
  conservation (balance equals credits minus debits), non-negative
  balance under any valid history, frozen-rejects-writes for arbitrary
  amounts, and closed-account-is-sealed against every state-changing
  command. 200 cases per property where the input space is wide,
  50 where it is narrow.

### Domain (continued)

- `Hold` aggregate: temporary reservation against an account's
  available balance with `Active → Captured | Released | Expired`
  lifecycle. Place rejects past expiry and non-positive amounts;
  Capture rejects expired holds; Release/Expire move the hold into
  terminal state; the three terminal states are mutually exclusive
  and reject further transitions. Time injected via
  `TimeProvider`/`FakeTimeProvider` so expiry behaviour is
  deterministically testable without sleeps.
- `HoldId` strongly-typed identifier, `HoldStatus` enum, the four
  hold events (`HoldPlaced`, `HoldCaptured`, `HoldReleased`,
  `HoldExpired`), and the `HoldException` family.

### Infrastructure

- Marten 8.34 wired as the event store. Single composition root
  (`AddLedgerInfrastructure`) configures the document store with
  conjoined tenancy and registers every domain event type so streams
  deserialise without runtime reflection on every load.
- `IAggregateRepository` Application port + `MartenAggregateRepository`
  implementation. Sessions are opened tenant-scoped via
  `IDocumentStore.LightweightSession(tenantId, IsolationLevel)`; loads
  go through `Events.FetchStreamAsync` and fold via the aggregate's
  own `Rehydrate`; saves use `StartStream<TAggregate>` for new streams
  and `AppendOptimistic` for existing ones, with version checks.
- `TenantId` value object (alphanumeric + `-`/`_`, 64-char cap,
  canonicalised lower-case), `ITenantContext` Application port, and
  two implementations: `StaticTenantContext` for tests/jobs and
  `AmbientTenantContext` for AsyncLocal scoping.
- Testcontainers-based integration tests cover round-tripping Account
  and Hold streams through Marten on real Postgres 16, plus a
  cross-tenant isolation test asserting that conjoined tenancy hides
  one tenant's stream from another.

### Documentation

- ADR-0005: conjoined tenancy over schema-per-tenant.
- ADR-0006: MediatR over Wolverine for in-process dispatch.

### Application

- `OpenAccountCommand` and its handler. Accepts `(Owner, Currency,
  IdempotencyKey)`, opens a fresh `Account` aggregate, persists via
  `IAggregateRepository`, and returns the new id, owner, currency,
  and a UTC-stamped `OpenedAt`.
- `IIdempotentRequest<TResponse>` marker plus
  `IdempotencyPipelineBehavior`: replays of a `(tenant, key)` return
  the cached response verbatim without re-running the handler. Hits
  the persistent `IIdempotencyStore` port; misses run the pipeline
  and write the response back with a 24h retention window.
- `ValidationPipelineBehavior` runs every registered FluentValidation
  validator and short-circuits with `CommandValidationException`
  before reaching the idempotency store, keeping cheap rejects out
  of the cache.
- `IdempotencyKey` Domain value object (alphanumeric +
  `-`/`_`/`:`/`.`, 128-char cap).
- `MartenIdempotencyStore` implements the Application port against
  a Marten document table; sessions are tenant-scoped via
  `LightweightSession(tenantId, ReadCommitted)`.
- `AddLedgerApplication` composition root: MediatR assembly scan,
  validation behavior, idempotency behavior (in that order), every
  `IValidator<>` from the assembly, and `TimeProvider.System`.

### Read side

- `account_balances` Marten projection. Single-stream, inline
  lifecycle: balance, status, and timestamps fold from
  `AccountOpened` / `Credited` / `Debited` / `Frozen` / `Unfrozen` /
  `Closed` events within the same transaction so a command handler
  reads its own writes inside one request.
- `IAccountBalanceQuery` Application port and `MartenAccountBalanceQuery`
  implementation. Tenant-scoped via Marten conjoined sessions; returns
  `AccountBalanceView` records to keep the document type private to
  Infrastructure.

[Unreleased]: https://github.com/popcom/ledger-core/commits/main
