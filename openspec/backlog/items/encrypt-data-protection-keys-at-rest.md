---
id: encrypt-data-protection-keys-at-rest
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: split from remove-unused-data-protection (obsolete premise); the persist half shipped in persist-state-on-writable-volume, 2026-07-28
---

## Why
DockYarp now **persists** its Data Protection keys to the mounted `/certs` volume (via
`persist-state-on-writable-volume`), which silenced `FileSystemXmlRepository[60]`. The keys are still written
**unencrypted at rest**, so `XmlKeyManager[35]` remains at every startup:
```
warn: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[35]
      No XML encryptor configured. Key {..} may be persisted to storage in unencrypted form.
```
Configuring an encryptor both silences the warning and protects any DP-protected payload at rest (YARP uses
Data Protection for session-affinity cookies).

## nginx-proxy behavior
N/A — internal log-hygiene / hardening finding, not a parity gap. No `parity.md` row.

## DockYarp today
- Data Protection is registered transitively (YARP session affinity). `Program.cs` calls
  `AddDataProtection().PersistKeysToFileSystem(<CertificateDirectory>/dataprotection-keys)` — persisted but
  **not** encrypted (no encryptor on Linux/chiseled; no DPAPI, no X.509 encryptor configured).
- Confirmed at the 2026-07-28 e2e run: `[60]` gone, `[35]` still emitted (`artifacts/e2e-logs/dockyarp.log`).

## Proposed change (sketch)
1. Configure a DP **XML encryptor** for the persisted key ring. Candidate approaches:
   - `ProtectKeysWithCertificate(...)` using an X.509 certificate DockYarp already manages (encrypt with the
     public key; a private key is needed to *decrypt* / read existing keys → key-availability trade-off across
     restarts must be designed carefully); or
   - a passphrase/secret-derived key provider supplied via configuration (`Tls`/a new `DataProtection` section).
2. Decide the key-rotation / decryptability story so a restart can still read previously written keys.
3. Assert on a real run that `XmlKeyManager[35]` is no longer emitted.

## Acceptance criteria (→ scenarios)
- **WHEN** DockYarp starts with its persisted key ring
- **THEN** the `XmlKeyManager[35]` "No XML encryptor configured" warning is not emitted
- **WHEN** DockYarp restarts against an existing (now encrypted) key ring
- **THEN** it reads the existing keys without error (no protected-data regression)

## Notes / risks / references
- Internal finding — no `parity.md` row.
- Touches AG-AT if a certificate-based encryptor is chosen (reuse of a managed cert / key availability).
- Sibling (done): `persist-state-on-writable-volume` (persisted the keys, silenced `[60]`).
- Confirm the encryptor's private key is available at every startup, or existing keys become unreadable.
