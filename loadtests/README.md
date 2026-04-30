# Load tests

[k6](https://k6.io) scripts that exercise the brief's performance gates
against a running stack.

## Stack

```bash
docker compose up -d --build
```

## Smoke (CI gate)

Short, low-VU run that asserts the brief's `p95 < 200ms` happy path.

```bash
k6 run -e BASE_URL=http://localhost:8080 loadtests/smoke.js
```

## Soak

Longer mixed workload (80% reads, 20% transfers). Default 5 minutes
at 10 VUs; tune via env vars.

```bash
k6 run \
  -e BASE_URL=http://localhost:8080 \
  -e VUS=20 \
  -e DURATION=30m \
  loadtests/soak.js
```

## Thresholds

Both scripts fail the run when any threshold trips, matching what the
brief calls out for the CI smoke gate.

| Threshold                                  | Value      | Source     |
| ------------------------------------------ | ---------- | ---------- |
| `http_req_duration{name:transfer}` p95     | < 200 ms   | brief      |
| `http_req_duration{name:read}` p95 (soak)  | < 150 ms   | derived    |
| `http_req_failed` rate                     | < 1%       | derived    |
| `transfers_failed` rate (soak)             | < 5%       | derived    |
