## Why

`FileCertificateStore.Save()` persists every provisioned certificate (ACME-issued or otherwise) as a binary
`<host>.pfx`, which doesn't match Linux/Docker-native conventions and diverges from the exact reference
DockYarp targets: nginx-proxy's `acme-companion` persists plain PEM text files (`<host>.crt`/`<host>.key`) per
host, never PKCS12. Today's PFX is already unencrypted (no export password), so switching to PEM carries no
security trade-off — it's a pure ergonomics and parity fix, requested directly by the user.

## What Changes

- `FileCertificateStore.Save()` writes `<host>.crt` (leaf + any intermediates, full-chain PEM) and `<host>.key`
  (the leaf's private key, PEM) instead of `<host>.pfx`.
- `FileCertificateStore.Load()`'s precedence flips: a `<host>.crt`/`<host>.key` pair now wins over a same-host
  legacy `<host>.pfx` still on disk (today it's the reverse) — so once a host's certificate is next
  provisioned/renewed under the new format, the PEM pair takes over without an operator needing to delete the
  stale PFX by hand.
- No forced migration: an existing deployment with only legacy `.pfx` files keeps loading and serving them
  exactly as today (`Load()` still reads `.pfx`) — they simply get replaced by PEM the next time that host's
  certificate is provisioned or renewed.
- **BREAKING for anything reading `<host>.pfx` directly off the certs volume** (not through DockYarp itself) —
  a freshly-provisioned host now produces `<host>.crt`/`<host>.key` there instead. Nothing in-product reads the
  file directly other than `FileCertificateStore` itself, which is updated; this only affects an operator's own
  external tooling, if any, pointed at the old filename pattern.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tls-acme`: the "ACME certificate acquisition" requirement's "SHALL store the resulting certificate in the
  certificate store" gains an explicit format (PEM, not PFX); the "Provided certificate loading" requirement's
  precedence between a provided PFX and a provided/ACME-persisted PEM pair for the same host needs its ordering
  stated (PEM wins), matching the new `Load()` behavior.

## Impact

- `src/DockYarp.Tls/FileCertificateStore.cs` — `Save()` (PEM write instead of PFX) and `Load()` (precedence
  reorder).
- `src/DockYarp.Tls/PemCertificateLoader.cs` / `CertificateCollectionLoader.cs` — likely reusable as-is for
  constructing the PEM write (they already handle the read side and the ACME-issued chain assembly
  respectively); check for a shared "build full-chain PEM text" helper worth extracting rather than duplicating
  string-building logic.
- `tests/DockYarp.Tls.Tests/` — unit coverage for the new write path and the flipped load precedence.
- `tests/DockYarp.E2E.Tests/` — `RestartPersistenceTests` (or equivalent) needs to prove PEM-persisted state
  survives a container recreation, the same guarantee already proven for PFX.
- `docs/labels-reference.md` and the docs-site TLS/certificate documentation — check for `.pfx` filename
  references per the standing docs-audit habit in `AGENTS.md`.
