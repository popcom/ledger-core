# Architecture diagrams

## C4 — System context

```mermaid
C4Context
title System Context — ledger-core

Person(operator, "Operator", "Triggers projection rebuilds, chaos toggles, GDPR forgets.")
Person(client, "Client App", "Calls the Ledger API on behalf of end-users.")

System(ledger, "ledger-core", "Event-sourced ledger module: accounts, transfers, holds.")
SystemDb_Ext(postgres, "Postgres 16", "Event store + projections + outbox + idempotency keys + subject keys.")
System_Ext(rabbit, "RabbitMQ", "Carrier for integration events drained from the outbox.")
System_Ext(otel, "OpenTelemetry stack", "Tempo / Loki / Prometheus / Grafana.")

Rel(client, ledger, "HTTPS, JSON, X-Tenant-Id, Idempotency-Key")
Rel(operator, ledger, "Admin and privacy endpoints")
Rel(ledger, postgres, "Conjoined-tenanted Marten sessions")
Rel(ledger, rabbit, "Drained by OutboxPublisher hosted service")
Rel(ledger, otel, "OTLP traces / metrics / logs")
```

## C4 — Container diagram

```mermaid
C4Container
title Containers — ledger-core deployment

Person(client, "Client App")

Container_Boundary(host, "ledger-core deployment") {
    Container(api, "Ledger.Api", ".NET 10, ASP.NET Core minimal API", "REST, problem-details, OpenTelemetry")
    Container(host_app, "Ledger.AppHost", "Aspire dev host", "Dev orchestration only")
}

ContainerDb(postgres, "Postgres 16", "Marten event store + projections + outbox + idempotency + subject keys")
ContainerQueue(rabbit, "RabbitMQ", "Integration event transport")
Container_Ext(otel, "OTel collector → Tempo/Loki/Prometheus/Grafana", "Observability stack")

Rel(client, api, "HTTPS")
Rel(api, postgres, "Marten")
Rel(api, rabbit, "MassTransit (when wired)")
Rel(api, otel, "OTLP")
```

## Module shape

```mermaid
flowchart LR
    subgraph Ledger
        direction LR
        Domain[Ledger.Domain<br/>aggregates / events / value objects]
        Application[Ledger.Application<br/>commands / sagas / ports / pipelines]
        Infrastructure[Ledger.Infrastructure<br/>Marten / outbox / projections / crypto]
        Api[Ledger.Api<br/>endpoints / problem-details / OTel]
        Contracts[Ledger.Contracts<br/>integration events / public DTOs]
    end

    Application --> Domain
    Application --> Contracts
    Infrastructure --> Application
    Api --> Application
    Api --> Infrastructure
    Api --> Contracts
```

## Transfer saga

```mermaid
sequenceDiagram
    participant Client
    participant Api as Ledger.Api
    participant Saga as InitiateTransferSaga
    participant Source as Account (source)
    participant Dest as Account (destination)
    participant Outbox as IOutbox

    Client->>+Api: POST /v1/transfers
    Api->>+Saga: InitiateTransferCommand
    Saga->>Source: Debit
    alt Debit fails
        Saga-->>Saga: Transfer.Fail (no compensation)
        Saga-->>-Api: Failed
    else Debit OK
        Saga->>Dest: Credit
        alt Credit fails
            Saga->>Source: Credit (compensation:*)
            Saga-->>Saga: Compensation completed
            Saga-->>-Api: Failed (compensated)
        else Credit OK
            Saga->>Outbox: TransferCompletedIntegrationEvent
            Saga-->>-Api: Completed
        end
    end
    Api-->>-Client: 201 Created / 200 OK
```
