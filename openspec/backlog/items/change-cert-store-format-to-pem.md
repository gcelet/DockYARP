---
id: change-cert-store-format-to-pem
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: medium
nginx-proxy: yes (acme-companion persists PEM text files per host — `<host>.crt`/`<host>.key`/
  `<host>.chain.pem` — never PKCS12; DockYarp currently diverges from that convention)
status: backlog
provenance: 2026-08-20 user feedback — "le format par défaut des certificats sauvegardés depuis l'échange ACME
  ne devrait pas être pfx, ça ne fait vraiment pas linux/docker natif"
---

## Why

`FileCertificateStore.Save()` (`src/DockYarp.Tls/FileCertificateStore.cs:39-54`) always persists a provisioned
certificate (ACME-issued or otherwise) as `<host>.pfx` — a binary PKCS12 container. This doesn't match
Linux/Docker-native conventions (no standard tool greps a `.pfx`), and diverges from the exact reference
DockYarp is meant to be a drop-in replacement for: nginx-proxy's `acme-companion` persists plain PEM text files
(`<host>.crt`/`<host>.key`) per host, never PKCS12. The user explicitly flagged this as poor fit for the
target environment.

## Current state

Confirmed by reading the code, not assumed:
- `FileCertificateStore.Save()` writes `fileSystem.File.WriteAllBytes(PathFor(host, ".pfx"), ExportChain(certificate))`
  — `ExportChain` calls `X509Certificate2Collection.Export(X509ContentType.Pfx)` with **no password** (confirmed
  in `CertesAcmeClient.BuildLoadedCertificate`'s own `bag.Export(X509ContentType.Pkcs12)!` call, same pattern —
  today's PFX is already unencrypted). **Switching to PEM is not a security regression**: the private key is
  exposed identically either way, protected only by filesystem permissions on the certs volume — same trust
  model nginx-proxy's own PEM files already rely on.
- `FileCertificateStore.Load()` (lines 79-112) **already reads both formats**: `.pfx` files first, then `.crt`
  files (paired with a same-basename `.key` via `PemCertificateLoader.TryLoad`) — a mounted PEM overrides a
  same-host PFX. This existing PEM read path was built for *operator-provided* certificates
  (`PemCertificateLoader`, see `fix-pem-cert-chain-dropped-on-load`/`fix-tls-chain-not-sent-in-handshake` for its
  chain-preservation history) — reusing it for the write side needs no new parsing logic, only a PEM *writer*.
- `LoadedCertificate` (leaf + `Additional` issuer certs) is the same in-memory shape either way; only
  `Save()`'s serialization changes.

## Proposed change (sketch)

- Add a PEM-writing path to `FileCertificateStore.Save()`: `<host>.crt` (leaf + intermediates, concatenated PEM,
  mirroring how `PemCertificateLoader`/the e2e `pem.local` fixture already build a full-chain PEM) and
  `<host>.key` (the leaf's private key, PEM-encoded, unencrypted — matching today's PFX's own no-password
  posture, not a new exposure).
- **Migration decision needed at propose time, not guessed here**: what happens to an existing `<host>.pfx` on
  an upgrade? Options include leaving it in place (harmless — `Load()` already prefers... actually loads PFX
  *before* PEM, so a stale PFX would keep winning until removed) vs. actively deleting/renaming it once the PEM
  pair is written for that host. `Load()`'s current PFX-then-PEM ordering may itself need to flip (PEM should
  probably win once it's the canonical format) — flag this precisely at design time.
- Consider whether `.pfx` reading should be kept indefinitely (for operators who still drop one in manually) or
  only kept for one deprecation window — a real product decision, not implied by "change the default".

## Acceptance criteria (→ scenarios)

- **WHEN** an ACME certificate is provisioned or renewed **THEN** it is persisted as `<host>.crt`/`<host>.key`
  PEM files, not `<host>.pfx`.
- **WHEN** DockYarp starts with only legacy `<host>.pfx` files present (no `.crt`/`.key` for that host) **THEN**
  it still loads and serves them correctly (no forced migration, no regression for existing deployments).
- **WHEN** both a legacy `.pfx` and a newly-written `.crt`/`.key` exist for the same host **THEN** the resolved
  precedence is deliberate and documented (not accidental based on enumeration order).

## Notes / risks / references

- Foundational for `add-admin-dashboard-cert-download` and `add-admin-dashboard-cert-conversion` (both new
  backlog items from the same feedback session) — a text-file canonical format is simpler for a dashboard
  endpoint to read/serve than parsing a PFX blob. Not a hard blocker for those (they could read either format),
  but sequencing this first avoids designing a download feature against a format about to change.
- `docs/labels-reference.md` and the docs-site TLS/certificate documentation may reference the `.pfx` filename
  pattern — check and update as part of this change per the standing "grep the whole docs-site tree" habit in
  `AGENTS.md`'s Change lifecycle.
- Restart-persistence e2e coverage (`RestartPersistenceTests`, see `fix-tls-chain-not-sent-in-handshake`'s
  history) already proves state survives a container recreation for the PFX format — needs the equivalent
  proof for the new PEM path, not just unit coverage.
