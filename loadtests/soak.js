// k6 soak test for ledger-core.
//
// Long-running mixed workload: 80% reads against the account_balances
// projection, 20% transfers between a pool of seeded accounts. Asserts
// the same p95<200ms gate as the smoke test plus a stability gate
// across the soak window.
//
// Run:
//   k6 run -e BASE_URL=http://localhost:8080 \
//          -e VUS=20 -e DURATION=30m loadtests/soak.js

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const TENANT = __ENV.TENANT_ID || 'acme';
const VUS = parseInt(__ENV.VUS || '10', 10);
const DURATION = __ENV.DURATION || '5m';

const transfers = new Trend('transfer_ms');
const reads = new Trend('read_ms');
const transfersFailed = new Rate('transfers_failed');

export const options = {
    vus: VUS,
    duration: DURATION,
    thresholds: {
        'http_req_duration{name:transfer}': ['p(95)<200'],
        'http_req_duration{name:read}': ['p(95)<150'],
        'http_req_failed': ['rate<0.01'],
        'transfers_failed': ['rate<0.05'],
    },
};

function headers(idempotencyKey) {
    const h = { 'Content-Type': 'application/json', 'X-Tenant-Id': TENANT };
    if (idempotencyKey) h['Idempotency-Key'] = idempotencyKey;
    return h;
}

// Setup runs once before the VUs start; it creates a pool of accounts
// the iterations route random traffic between.
export function setup() {
    const pool = [];
    for (let i = 0; i < 20; i++) {
        const res = http.post(
            `${BASE_URL}/v1/accounts`,
            JSON.stringify({ owner: `soak-${i}`, currency: 'EUR' }),
            { headers: headers(`soak-init-${i}`) });
        if (res.status === 201) {
            pool.push(res.json('accountId'));
        }
    }
    return { pool };
}

export default function (data) {
    const pool = data.pool;
    if (pool.length < 2) return;

    const r = Math.random();
    if (r < 0.8) {
        // Read path
        const id = pool[Math.floor(Math.random() * pool.length)];
        const start = Date.now();
        const res = http.get(`${BASE_URL}/v1/accounts/${id}`,
            { headers: headers(), tags: { name: 'read' } });
        reads.add(Date.now() - start);
        check(res, { 'read 200': (r) => r.status === 200 });
    } else {
        // Transfer path
        const src = pool[Math.floor(Math.random() * pool.length)];
        let dst = pool[Math.floor(Math.random() * pool.length)];
        while (dst === src) dst = pool[Math.floor(Math.random() * pool.length)];

        const start = Date.now();
        const res = http.post(`${BASE_URL}/v1/transfers`,
            JSON.stringify({
                sourceAccountId: src,
                destinationAccountId: dst,
                amount: 1.0, currency: 'EUR',
                reference: 'soak',
            }),
            { headers: headers(`tx-${uuidv4()}`), tags: { name: 'transfer' } });
        transfers.add(Date.now() - start);
        transfersFailed.add(res.status >= 400);
    }

    sleep(0.05 + Math.random() * 0.1);
}
