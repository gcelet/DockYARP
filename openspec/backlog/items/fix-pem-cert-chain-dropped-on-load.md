---
id: fix-pem-cert-chain-dropped-on-load
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: high
status: backlog
nginx-proxy: (internal — DockYARP TLS correctness bug, no parity row)
provenance: 2026-08-16 user's own real-world nginx-proxy → DockYARP migration test — verified against code and
  Microsoft's own X509Certificate2 documentation, not assumed
---

## Why
Loading a mounted PEM certificate whose `.crt` file contains a **full chain** (leaf + intermediate concatenated
— the standard shape produced by nginx-proxy/acme-companion, acme.sh, and virtually every real ACME client)
**silently drops the intermediate certificate**. Only the leaf is served during the TLS handshake, so any client
that doesn't already trust the issuing CA's intermediate out of band fails with "unable to get local issuer
certificate" (curl) / an unbypassable HSTS security error (Firefox, since HSTS blocks the "add exception" path).

**Scope: this is NOT specific to the nginx-proxy migration path.** It was *discovered* via a real nginx-proxy →
DockYARP migration test, but the bug lives in `PemCertificateLoader.TryLoad` — DockYARP's single, general
mechanism for loading **any** `<host>.crt`/`<host>.key` pair placed under `/certs`, regardless of where those
files came from (a migration, a manually-run `certbot`/`acme.sh`, a hand-copied cert from anywhere). Any operator
who drops a standard multi-cert `fullchain.pem`-shaped certificate into `/certs` hits this, migration or not.
**Consequence for scoping the fix**: this belongs in the core loader itself (see "Proposed change" below), not
as a migration-specific "redress the certificate format during container discovery" step — Docker container
discovery has nothing to do with certificate *files* at all (discovery maps labels to routes; certificates are
loaded from `/certs` independently by `FileCertificateStore`/`PemCertificateLoader`), so there is no
"discovery-time" hook where this would even make sense to fix. Fixing the shared loader fixes it for every
source at once, including the migration scenario that surfaced it.

This is not a corner case — it breaks TLS for **every** host whose certificate chain isn't a single
self-contained leaf, which in practice is nearly all real ACME-issued certificates. Discovered live: user copied
their real nginx-proxy certs (`<host>.crt` → `fullchain.pem`, containing step-ca's leaf + intermediate) into
DockYARP's `/certs` and every migrated service stopped working.

## nginx-proxy behavior
N/A — internal DockYARP TLS-serving bug, not a proxy feature gap. No `parity.md` row.

## DockYarp today
Root cause confirmed in code and against Microsoft's own documentation before writing this stub:
- `src/DockYarp.Tls/PemCertificateLoader.cs:23` calls
  `X509Certificate2.CreateFromPem(fileSystem.File.ReadAllText(certPath), fileSystem.File.ReadAllText(keyPath))`.
- Per Microsoft Learn (`X509Certificate2.CreateFromPem(ReadOnlySpan<char>, ReadOnlySpan<char>)` remarks): **"For
  the certificate, the first certificate with a CERTIFICATE label is loaded."** — any additional certificate
  blocks in the same PEM text (i.e. the intermediate, when `certPath` is a `fullchain.pem`-shaped file) are
  silently ignored, not an error.
- Confirmed no downstream code compensates: grepped `src/DockYarp.Tls/*.cs` for
  `SslStreamCertificateContext`/`AdditionalCertificates`/chain-building on the *server* cert path — none exists
  (`ClientCertificateValidator.cs`'s `X509Chain` usage is for validating *incoming* client certs in mTLS,
  unrelated). The bare `X509Certificate2` from `PemCertificateLoader` is exactly what Kestrel serves.
- Reproduced live: `curl -vvvv https://<host>` against the migrated stack shows exactly one `Certificate (11)`
  handshake message before an `unknown CA` alert — consistent with a leaf-only certificate being sent.
- **The fix direction is already validated elsewhere in this codebase**: `CertesAcmeClient.BuildPfx`
  (`src/DockYarp.Tls/CertesAcmeClient.cs:60-72`) builds ACME-issued certificates as a **chain-inclusive PFX**
  by default (`pfxBuilder.Build(...)` without `FullChain = false`), only falling back to leaf-only when the full
  chain build fails for the specific, understood reason of a private CA not publishing its root to Certes. Those
  PFX-loaded certificates (via `FileCertificateStore.Load()`'s `*.pfx` branch, which loads bytes directly via
  `X509CertificateLoader.LoadPkcs12` with no `CreateFromPem` involved) are already known to serve correctly —
  this is the existing, working pattern the PEM path should mirror instead of introducing a second, buggy one.

## Proposed change (sketch)
- `PemCertificateLoader.TryLoad`: replace the naive `CreateFromPem(certPem, keyPem)` call with logic that:
  1. Parses **all** certificates in `certPath` via `X509Certificate2Collection.ImportFromPem` (imports every
     `CERTIFICATE`-labeled PEM block, not just the first).
  2. Attaches the private key to the leaf entry (conventionally first in a `fullchain.pem`) via
     `CopyWithPrivateKey`, matching the key from `keyPath`.
  3. Builds a chain-inclusive `X509Certificate2Collection` (leaf-with-key + any intermediate(s)) and exports/
     reloads it as a single PKCS12 blob — the same shape `FileCertificateStore`'s existing PFX branch already
     loads correctly — rather than a bare single-cert `X509Certificate2`.
  4. Handles the single-cert case (no intermediate present) unchanged — must not regress that path.
- Verify behavior for both orderings (some tools put the intermediate before the leaf) — don't assume "first
  cert = leaf"; the correct leaf is the one the supplied private key matches.
- Test with a real multi-cert fixture (leaf + intermediate from a test CA), asserting the full chain is present
  in what Kestrel actually sends — an integration/e2e-level check, not just "loading doesn't throw".

## Acceptance criteria (→ scenarios)
- **WHEN** a mounted `<host>.crt` contains a full chain (leaf + intermediate) **THEN** the intermediate is
  preserved and served during the TLS handshake, and a client without the intermediate cached/trusted out of
  band can still build a full chain to a root it does trust.
- **WHEN** a mounted `<host>.crt` contains only a leaf certificate (no intermediate) **THEN** behavior is
  unchanged from today (no regression for the single-cert case).

## Notes / risks / references
- Discovered during the user's real REDACTED migration test (private, gitignored grounding file — never
  committed). Blocks the migration path for any host with a real ACME-issued (chain-bearing) certificate —
  effectively all of them.
- The already-archived `add-doc-nginx-proxy-migration-guide` change claimed "no format conversion is needed"
  for copying nginx-proxy certs — that claim is **wrong** given this bug and needs a correction once this is
  fixed (either fix the bug so the claim becomes true, or amend the guide to describe a required conversion
  step in the meantime — fixing the bug is preferred, don't paper over it in docs).
- Refs: `src/DockYarp.Tls/PemCertificateLoader.cs`, `src/DockYarp.Tls/FileCertificateStore.cs` (PFX branch,
  the working pattern to mirror), `src/DockYarp.Tls/CertesAcmeClient.cs:60-72` (`BuildPfx`, the existing
  full-chain-by-default precedent).
