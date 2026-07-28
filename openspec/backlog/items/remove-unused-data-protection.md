---
id: remove-unused-data-protection
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal finding — not an nginx-proxy parity gap)
provenance: e2e diagnostics log review, 2026-07-27; split from quiet-startup-warnings (the HTTP/2 half shipped in quiet-http2-plaintext-warning)
---

## Why
DockYarp emits two Data Protection warnings at startup (keys stored ephemerally + unencrypted) even though it
does **not** use Data Protection (no cookies, antiforgery, auth). Cosmetic but noisy; a clean startup is more
trustworthy.

## Observed behavior (e2e log — exact categories/event IDs)
```
warn: Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository[60]
      Storing keys in a directory '/home/app/.aspnet/DataProtection-Keys' that may not be persisted outside of the container...
warn: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[35]
      No XML encryptor configured. Key {..} may be persisted to storage in unencrypted form.
```

## DockYarp today
No explicit `AddDataProtection` anywhere in the codebase; Data Protection is registered **transitively** and its
key ring is initialized at startup (something resolves `IDataProtectionProvider` during startup). Keys default
to the ephemeral `/home/app/.aspnet/DataProtection-Keys`, unencrypted (no encryptor on Linux).

## Proposed change (sketch)
1. Find what pulls in / initializes Data Protection at startup (YARP? a framework default? HSTS?).
2. Then either:
   - **(preferred)** avoid/remove the Data Protection dependency if DockYarp genuinely never uses protected
     payloads (it has no cookies/antiforgery/session/auth-cookie); or
   - persist keys to the mounted `/certs` volume **and** configure a key encryptor (e.g. certificate-based) so
     **both** warnings are silenced. Persisting alone silences only `[60]`; the `[35]` "unencrypted" warning
     stays on Linux without an encryptor.
- The chiseled non-root image needs a **writable** target (mounted `/certs` works; `/home/app/.aspnet` is
  ephemeral).

## Acceptance criteria (→ scenarios)
- **WHEN** DockYarp starts
- **THEN** neither Data Protection warning (`FileSystemXmlRepository[60]` / `XmlKeyManager[35]`) is emitted
- **WHEN** DockYarp restarts
- **THEN** no protected-data functionality regresses (DockYarp uses none today)

## Notes / risks / references
- Internal log-hygiene finding — no `parity.md` row.
- The crux is locating the DP trigger; confirm DockYarp truly needs no DP before removing it.
- Sibling (done): the HTTP/2-without-TLS warning was fixed in `quiet-http2-plaintext-warning`.
