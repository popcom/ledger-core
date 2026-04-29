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

[Unreleased]: https://github.com/popcom/ledger-core/commits/main
