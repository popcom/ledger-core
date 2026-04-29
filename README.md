# ledger-core

A production-grade, event-sourced ledger service for recording money movement
with full auditability. Modular monolith built on .NET 10, Marten, and
PostgreSQL, with read projections, sagas, and crypto-shredding for GDPR.

> Status: scaffolding. The roadmap below is delivered one PR at a time.

## What and why

`ledger-core` models the kind of double-entry ledger you would find inside a
neobank or broker. It is opinionated about correctness: monetary arithmetic is
`decimal`-only, transfers are sagas with explicit compensation, and every
state change is an immutable event. It is not a full banking system and not a
generic event store.

## Tech stack

| Concern             | Choice                                              |
| ------------------- | --------------------------------------------------- |
| Runtime             | .NET 10, C# 13                                      |
| Web                 | ASP.NET Core minimal APIs                           |
| Event store / DB    | Marten 7.x on PostgreSQL 16                         |
| Messaging           | MassTransit 8.x on RabbitMQ                         |
| Dispatch            | MediatR (decision pending an ADR)                   |
| Validation          | FluentValidation                                    |
| Mapping             | Mapster                                             |
| Observability       | OpenTelemetry + Serilog                             |
| Local orchestration | .NET Aspire, Docker Compose                         |
| Tests               | xUnit, FluentAssertions, NSubstitute, Testcontainers, Verify, FsCheck, ArchUnitNET |

## Repository layout

```
src/
  Ledger.Domain          - aggregates, events, value objects
  Ledger.Application     - command/query handlers, sagas, ports
  Ledger.Infrastructure  - Marten, Postgres projections, MassTransit, outbox
  Ledger.Api             - ASP.NET Core minimal API
  Ledger.Contracts       - public DTOs and integration events
  Ledger.AppHost         - .NET Aspire host
tests/
  Ledger.Domain.UnitTests
  Ledger.Application.UnitTests
  Ledger.Infrastructure.IntegrationTests
  Ledger.Api.IntegrationTests
  Ledger.ArchitectureTests
  Ledger.PropertyTests
docs/                    - ADRs, C4 diagrams, runbooks
loadtests/               - k6 scripts
infra/                   - Terraform, Kubernetes manifests
samples/                 - Postman collections
```

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

A reproducible toolchain is pinned via [`global.json`](./global.json) and
package versions are managed centrally via
[`Directory.Packages.props`](./Directory.Packages.props).

## Roadmap

See [`BRIEF.md`](./BRIEF.md) for the full milestone plan. The README will be
expanded with a hero diagram, a 60-second run guide, observability
screenshots, performance numbers, and the GDPR / crypto-shredding section as
those features land.

## License

MIT - see [`LICENSE`](./LICENSE).
