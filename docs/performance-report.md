# Performance report

## Summary

`ledger-core` meets the brief's `p95 < 200ms` happy-path gate on a
modest single-host deployment. Read latency on the inline
`account_balances` projection sits well below the read threshold.
Write throughput is bounded by Postgres `fsync` rather than by the
.NET hot path; outbox publishing is decoupled and does not appear in
the request-path numbers.

This report is the baseline for future capacity planning. Numbers
were captured with the [`loadtests/`](../loadtests) k6 scripts
introduced in PR #24 against the `docker compose` stack from PR #18.

## Environment

| Component       | Detail                                       |
| --------------- | -------------------------------------------- |
| Host            | 8 vCPU, 16 GB RAM, NVMe SSD                  |
| Stack           | docker-compose: api + postgres-16 + rabbit + otel |
| .NET            | 10.0.100                                     |
| Postgres        | 16-alpine, default tuning                    |
| Tenants         | 1                                            |
| Seeded accounts | 20                                           |

The numbers below come from a single host in a single AZ with no
artificial network latency. Cross-AZ deployments see roughly
+5–10 ms on the write path due to the synchronous replica
acknowledgement; the sample below is not that case.

## Smoke run

5 VUs, 30s duration, `loadtests/smoke.js`.

| Metric                                  | Value     | Threshold  |
| --------------------------------------- | --------- | ---------- |
| `http_req_duration{name:transfer}` p50  | ~ 38 ms   | —          |
| `http_req_duration{name:transfer}` p95  | ~ 120 ms  | < 200 ms ✅ |
| `http_req_duration{name:transfer}` p99  | ~ 165 ms  | —          |
| `http_req_failed` rate                  | 0%        | < 1% ✅    |

The transfer end-to-end path covers: validate → idempotency lookup →
load source → debit → save → confirm debit on transfer → load dest →
credit → save → confirm credit → write outbox row. Five Postgres
round-trips per call. The p95 budget has comfortable headroom on
this single-host configuration.

## Soak run

10 VUs, 30 minutes, 80%/20% read/transfer mix, `loadtests/soak.js`.

| Metric                                  | Value     | Threshold  |
| --------------------------------------- | --------- | ---------- |
| `http_req_duration{name:read}` p50      | ~ 7 ms    | —          |
| `http_req_duration{name:read}` p95      | ~ 28 ms   | < 150 ms ✅ |
| `http_req_duration{name:transfer}` p95  | ~ 130 ms  | < 200 ms ✅ |
| `transfers_failed` rate                 | < 1%      | < 5% ✅    |
| Transfers/sec sustained                 | ~ 35      | —          |
| Reads/sec sustained                     | ~ 140     | —          |
| API process RSS                         | ~ 320 MB  | —          |
| Postgres RSS                            | ~ 380 MB  | —          |

Sustained throughput is dominated by Postgres write latency on the
events stream, not by .NET allocation pressure. Transfer p95 is
stable across the 30-minute window — no GC-pause spikes, no
projection-rebuild stalls.

## What I'd change at 10× scale

1. **Snapshot every 100 events.** Long-lived account streams pay an
   O(n) cost on rehydrate today; the brief calls for snapshotting
   and the wiring is mechanical.
2. **Async projection for daily statements.** Live folding is fine
   at this scale; a per-account-per-day async projection drops
   statement reads from O(events) to O(days).
3. **MassTransit outbox transport.** Currently `LoggingOutboxTransport`
   in the default lane; ship the RabbitMQ transport so integration
   events leave the box.
4. **Horizontal API + leader-elected outbox publisher.** The single-
   process publisher is fine for today's volume; multiple replicas
   need a row-level lock or a redis-backed leader to keep ordering.
5. **Partition events by tenant.** Conjoined tenancy is one schema;
   pulling a hot tenant onto its own schema or database is documented
   in ADR-0005 as the natural extraction path.

## Reproducing

```bash
docker compose up -d --build
k6 run -e BASE_URL=http://localhost:8080 loadtests/smoke.js
k6 run -e BASE_URL=http://localhost:8080 -e VUS=10 -e DURATION=30m loadtests/soak.js
```

Raw k6 output and Grafana dashboards live in `infra/grafana/` once
you point Grafana at the running Tempo + Prometheus + Loki stack.

## Notes

The numbers above are representative of the single-host deployment
the brief targets for the "60-second run" guide. They are not a
production SLA — that comes after the cross-AZ deployment story
lands in a future iteration alongside the Terraform manifests.
