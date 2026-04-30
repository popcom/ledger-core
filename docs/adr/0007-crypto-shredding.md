# ADR-0007: Per-subject crypto-shredding for GDPR

- Status: Accepted
- Date: 2026-04-30
- Deciders: Mohsen (architect), engineering team
- Tags: gdpr, security, crypto, ledger, audit

## Context

GDPR Article 17 requires data controllers to delete a subject's
personal data on request. The Ledger has two competing properties:

1. The event log is **immutable**. Tampering with past events would
   destroy the audit guarantee the brief sells the system on; rewriting
   stream history breaks every projection rebuild and every
   point-in-time query the brief lists.
2. PII fields **must disappear** when a subject exercises their right
   to be forgotten. "Pseudonymisation" alone does not satisfy modern
   regulators — the data must be unrecoverable.

The two ways to square this are:

1. **Tombstoning**: rewrite past events to redact PII fields. Preserves
   structure, breaks immutability, breaks any cryptographic chain that
   ties events together.
2. **Crypto-shredding**: encrypt PII fields at write time with a
   per-subject key, and *delete the key* on a forget request. Past
   events stay byte-for-byte identical; the encrypted payload becomes
   unrecoverable mathematics rather than recoverable plaintext.

The brief calls crypto-shredding the flagship privacy feature; this
ADR records the cryptographic choices.

## Decision

`ledger-core` uses **per-tenant-per-subject AES-GCM-256
crypto-shredding** for PII fields stored alongside events.

- **Key store**. `ISubjectKeyStore` (Application port) returns the raw
  key for a `SubjectId`. The Marten implementation persists a
  `SubjectKeyDocument` keyed on the subject id, scoped per tenant via
  Marten conjoined tenancy. Forgetting a subject is a single document
  delete.
- **Cipher**. AES-GCM-256 with a 96-bit nonce, 128-bit tag, version
  byte. Each encryption call generates a fresh random nonce; the
  envelope packs `(version | nonce | ciphertext | tag)` and is stored
  base64-encoded. Storing the nonce inline keeps key rotation safe
  (the same key encrypts many fields without nonce reuse) and gives
  us room to migrate algorithms via a version bump.
- **Boundary**. `IPiiCrypto` is the only path that touches plaintext
  PII. Domain events accept ciphertext strings; aggregate code never
  sees the underlying bytes. The API edge encrypts inbound and
  decrypts outbound through the same port.
- **Forget**. `POST /v1/privacy/forget/{subjectId}` deletes the
  subject's key. Past events keep their rows; their PII fields
  become unreadable and `IPiiCrypto.DecryptAsync` returns `null`
  rather than throwing — the audit log remains queryable but the
  PII is gone.
- **Multi-tenancy**. Subject keys are conjoined-tenanted, same as
  every other Ledger document. One tenant cannot reach into
  another's key space; one tenant's forget operation does not
  affect another.

## Consequences

Positive:

- Right-to-be-forgotten satisfied without rewriting history. The
  immutable audit log stays immutable; only the key changes.
- Algorithm migration is one version-byte switch and a re-encrypt
  pass on live keys. Past events with the old version stay readable
  via the legacy path; past events whose subject has been forgotten
  remain unrecoverable regardless of the algorithm.
- The boundary is small (one port, two methods, one storage type),
  so the audit story is concentrated and reviewable.

Negative:

- Key compromise is an "everyone forgotten by mistake" risk.
  Mitigated by storing keys in the same tenant-scoped store the
  rest of the system uses (so a compromise of that store
  compromises everything else too — losing the keys is no worse
  than losing the events) and by rotating keys on a schedule the
  brief defines later.
- Once a key is deleted, that subject's PII is gone forever, even
  for legitimate post-deletion access (e.g. discovery in a legal
  hold). Operators must distinguish "forgotten" from "soft-suspend";
  the brief is explicit that GDPR forget is irrevocable.
- AES-GCM forbids nonce reuse with the same key. The 96-bit random
  nonce is safe for ~ 2³² messages per key; well above any single
  subject's lifetime PII volume.

Risks:

- **Tag truncation, AAD misuse, or implementation bug.** Mitigated
  by using the BCL `AesGcm` class (FIPS-validated where the
  platform provides it) and a tight envelope format with explicit
  unit tests.
- **Forget-then-restore-from-backup.** If a backup taken before the
  forget operation is restored, the key reappears and the PII
  becomes readable again. Operational practice (the runbook in
  `docs/runbooks/`) documents that forgets must be replayed across
  recovered backups.

## Alternatives considered

- **Tombstone events**. Rejected: rewriting an immutable log breaks
  the audit story.
- **Encrypt the entire event payload with a tenant key**. Coarser;
  forgetting one subject would require either rewriting events or
  re-keying the tenant. Per-subject keys keep the blast radius of a
  forget exactly one subject.
- **Application-layer encryption with a shared HSM-held key**.
  Strong key custody, but a single forget event becomes "rotate the
  HSM key and re-encrypt every subject's events", which scales
  poorly. Per-subject keys make forget O(1).

## References

- ENISA, "Pseudonymisation Techniques and Best Practices" (2019).
- NIST SP 800-38D (AES-GCM).
- The brief's GDPR / crypto-shredding section.
