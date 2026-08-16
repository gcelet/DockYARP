---
id: add-doc-nginx-proxy-migration-guide
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal initiative — adoption/migration content, no parity row of its own)
provenance: 2026-08-16 user idea (session close); unblocked 2026-08-16 (new session) once the user's private
  installation recap existed — grounded and verified before writing this update, see Notes
---

## Why
DockYarp positions itself as an nginx-proxy equivalent (`AGENTS.md`: "a dynamic reverse proxy for Docker
containers, an nginx-proxy equivalent"), and `openspec/backlog/parity.md` already tracks feature-by-feature
compatibility in depth — but that matrix is an **engineering tracking doc**, not something written for an
nginx-proxy operator deciding whether/how to switch over. Nothing on the doc site walks a reader through an
actual migration.

## nginx-proxy behavior
N/A — internal initiative (a migration guide for adopting DockYarp, not a proxy feature). No `parity.md` row.

## DockYarp today
- `openspec/backlog/parity.md` — the definitive feature matrix; this guide should **link to it**, not duplicate
  it, for the exhaustive comparison.
- `docs-site/content/en/docs/configuration.md` already documents the nginx-proxy-compatible label set
  (`VIRTUAL_*`, `LETSENCRYPT_*`, `CERT_NAME`, `SSL_POLICY`, `HTTPS_METHOD`, `HSTS`, `NETWORK_ACCESS`,
  `SERVER_TOKENS`, `EXTERNAL_HTTPS_PORT`) and the `DOCKYARP_*` extensions — most of the label-level mapping work
  is already written, just not framed as "coming from nginx-proxy."
- `getting-started.md` / `examples.md` cover a fresh DockYarp install and recipes, not a migration path from an
  existing nginx-proxy deployment.

## Proposed change (sketch) — two paths
1. **Basic path**: a typical nginx-proxy + `acme-companion` docker-compose setup (public Let's Encrypt) → the
   equivalent DockYarp compose stack. Mostly a direct label swap (most nginx-proxy labels already work unchanged
   per `configuration.md`) plus the compose-service-level changes (image, socket-proxy recommendation, etc.).
2. **Advanced path**: grounded in the user's own production nginx-proxy installation — the classic
   `nginxproxy/nginx-proxy` + `nginxproxy/docker-gen` + `nginxproxy/acme-companion` trio, `VIRTUAL_HOST`/
   `LETSENCRYPT_HOST`-style env vars on every backend stack (not labels), a **private ACME CA** (not public
   Let's Encrypt) reached over HTTP-01, and two Docker networks (one macvlan for LAN-direct services, one bridge
   for reverse-proxied ones). Migration must be **iso for every backend stack** — only the front-door
   (nginx-proxy) stack itself is replaced; no other stack's config changes. Must also preserve a clean rollback
   path to nginx-proxy (existing files, especially certificates, untouched).

## Acceptance criteria (→ scenarios)
- **WHEN** an operator running a basic nginx-proxy + acme-companion stack reads the guide **THEN** they get a
  direct, copy-pasteable compose/label translation to the DockYarp equivalent.
- **WHEN** an operator running the advanced pattern (private ACME CA, `docker-gen`/`acme-companion` trio, env-var
  backend config) reads the guide **THEN** they find that exact pattern covered, not just the basic case.
- **WHEN** the guide covers certificates **THEN** it states plainly: copy (never move) the existing
  `nginx/certs/<host>.crt`+`<host>.key` files into DockYarp's certificate directory before first start — no
  conversion needed (verified, see Notes) — so nginx-proxy's own files are untouched and rollback stays trivial.
- **WHEN** the guide covers a private ACME CA **THEN** it documents `Tls:AcmeDirectoryUri` (already supports a
  custom directory) plus mounting a combined CA bundle and setting `SSL_CERT_FILE` so DockYarp's own ACME calls
  trust that CA (verified empirically, see Notes) — no DockYarp code change needed for either.
- **WHEN** the guide needs the exhaustive feature-by-feature comparison **THEN** it links to
  `openspec/backlog/parity.md` rather than restating it.

## Notes / risks / references
- **Everything below was verified before writing this update, not assumed** — the user specifically flagged that
  this dev machine already trusts their real private CA, so anything "confirmed" here had to be tested without
  relying on that ambient trust:
  - **Cert file compatibility**: confirmed by reading `PemCertificateLoader`/`FileCertificateStore` —
    `X509Certificate2.CreateFromPem` accepts a full chain in `.crt` and RSA **or** EC keys in `.key`
    (acme-companion's exact output shape); `FileCertificateStore.Load()` auto-detects and reuses a pre-existing
    `{host}.crt`+`{host}.key` pair at startup, skipping ACME provisioning unless within `RenewBeforeExpiry`
    (default 30 days) of expiry. No code change; no conversion step.
  - **Private-CA ACME trust**: confirmed with a real, isolated test — generated a throwaway self-signed root +
    leaf cert (unrelated to the user's real CA), served it from a container on its own Docker network, and ran
    DockYarp's exact base image (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`) against it: **without**
    `SSL_CERT_FILE` set, the connection failed (`PartialChain` — proving no ambient trust leaked into the test);
    **with** `SSL_CERT_FILE` pointing at the throwaway root, it succeeded. .NET on Linux delegates to OpenSSL and
    honors this standard env var. So: mount a combined bundle (system CAs + the private root) and set
    `SSL_CERT_FILE` on the DockYarp container — no DockYarp code change.
  - **HTTP-01 vs DNS-01**: `acme-companion`'s `ACME_HTTP_CHALLENGE_LOCATION` only controls *which component*
    writes the nginx challenge-location config (acme-companion itself vs. nginx-proxy's built-in support) — it
    is not a DNS-01 toggle. No DNS-provider credentials anywhere in a real advanced setup imply HTTP-01, which
    DockYarp already supports.
  - **No new code-feature backlog item needed** — the user's original suggestion (copy/move/convert certs on
    first startup) turned out to need none of that beyond "copy the files"; both points above resolve entirely
    in documentation.
- Decide at propose-time: one doc-site page with two sections, or two separate pages (basic vs. advanced) —
  likely one page, mirroring how `examples.md` already groups multiple recipes on one page.
- Refs: `openspec/backlog/parity.md`, `docs-site/content/en/docs/configuration.md`,
  `docs-site/content/en/docs/getting-started.md`, `src/DockYarp.Tls/{PemCertificateLoader,FileCertificateStore,
  CertesAcmeClient}.cs`.
