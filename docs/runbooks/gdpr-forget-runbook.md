# Runbook: Right-to-be-forgotten (GDPR Article 17)

## Scope

A subject (natural person) has requested the deletion of their personal
data. `ledger-core` honours the request through **crypto-shredding**:
the per-subject key is deleted, rendering past PII fields unreadable
while preserving the immutable audit log.

This runbook is for the operator executing the request, not the
end-user. It assumes you have admin credentials for the target tenant
and access to the production Postgres + backup catalogue.

## Pre-flight

1. **Verify the request is authentic** through the customer-facing
   identity flow. Right-to-be-forgotten is irrevocable.
2. **Identify the `subjectId`** the Ledger uses for the customer.
   This is the same id the API embedded in PII fields when the
   account was opened.
3. **Confirm there is no legal hold** on the subject's data (active
   investigation, regulatory order). A hold supersedes the GDPR
   request and must be resolved first.

## Forget the subject

```bash
curl -X POST \
  -H "X-Tenant-Id: <tenant>" \
  https://api.ledger.popcom.dev/v1/privacy/forget/<subjectId>
```

Expected response: `202 Accepted`, body `{ "subjectId": "...", "status": "forgotten" }`.

The endpoint deletes the `subject_keys` row for the subject. From
this point forward `IPiiCrypto.DecryptAsync(subject, ...)` returns
`null` for any field encrypted with the key.

## Verify

1. **Read an account associated with the subject.** Decrypted PII
   fields should be `null` or a redaction marker.
2. **Pull a recent event from the stream** and confirm the encrypted
   blob is intact (the row still exists; the cipher is just no
   longer reversible).
3. **Check the audit log** for the forget event; the operator's
   identity and the wall-clock timestamp must be recorded.

## Replay across backups

Crypto-shredding is byte-level on the live database. A backup taken
**before** the forget operation still contains the key. If that
backup is restored to recover an unrelated incident, the subject's
PII becomes readable again.

After every forget operation:

1. List Postgres backups taken since the subject's last activity.
2. For each backup that is in active retention, **schedule a
   replay** of the forget endpoint after the backup is restored,
   or rotate the backup catalogue so the affected backups are
   purged after the retention window expires.

## What this does NOT do

- Does **not** rewrite past events. The structure of the event log
  is intentionally immutable; only the cipher state changes.
- Does **not** purge the subject's id from indexes or projections.
  The `subjectId` is itself a pseudonym; if it carries PII (an
  email, a national id), that is a misconfiguration and should be
  fixed at the API edge before any forget operation runs.
- Does **not** undo. Once the key is deleted, the only way to
  recover is to restore from a backup taken before the forget
  operation, which would re-expose the data — and that is the
  exact path GDPR forbids.
