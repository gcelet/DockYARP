## Why

DockYarp positions itself as an nginx-proxy equivalent, and `openspec/backlog/parity.md` already tracks
feature-by-feature compatibility in depth — but that matrix is an engineering tracking document, not something
written for an nginx-proxy operator deciding whether and how to switch over. Nothing on the doc site walks a
reader through an actual migration, including the two things an operator would worry about most: their existing
certificates, and being able to roll back if something goes wrong.

## What Changes

- Add a new doc-site page, **Migrating from nginx-proxy** (`docs-site/content/en/docs/migrating-from-nginx-proxy.md`,
  weight 5 — placed right after Examples and before Architecture; existing pages from Architecture onward shift
  weight by one), covering two paths:
  - **Basic**: a typical nginx-proxy + `acme-companion` (public Let's Encrypt) compose setup → the DockYarp
    equivalent. Mostly a label swap (already-compatible per `configuration.md`) plus the compose-service changes.
  - **Advanced**: the classic `nginx-proxy` + `docker-gen` + `acme-companion` trio, backend config via
    `VIRTUAL_HOST`/`LETSENCRYPT_HOST`-style environment variables (not labels), a **private ACME CA** reached
    over HTTP-01, and multiple Docker networks. States plainly that migration is **iso for every backend
    stack** — only the front-door (nginx-proxy) stack is replaced.
- A **certificates and rollback** section, applicable to both paths: copy (never move) the existing
  `nginx/certs/<host>.crt`+`<host>.key` files into DockYarp's certificate directory before first start —
  DockYarp auto-detects and reuses them, no conversion needed — so nginx-proxy's own files stay untouched and
  restarting the old stack remains a real option throughout the migration.
- A **private ACME CA** section (advanced path only): `Tls:AcmeDirectoryUri` for the custom directory, plus
  mounting a combined CA bundle and setting `SSL_CERT_FILE` so DockYarp's own ACME/HTTP calls trust that CA.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `documentation`: adds an **nginx-proxy migration guide** requirement (a standalone page covering a basic and
  an advanced migration path, certificate reuse without conversion, and private-CA trust), alongside the
  existing reference-page requirements.

## Impact

- New: `docs-site/content/en/docs/migrating-from-nginx-proxy.md`.
- Modified: `docs-site/content/en/docs/{architecture,deployment,contributing,releasing}.md` (weight shifted by
  one to make room).
- No `src/`/`tests/` changes — documentation-only (AG-DOC). Two technical claims in this guide (cert reuse
  needs no conversion; private-CA trust needs no code change) were already verified this session — by reading
  `PemCertificateLoader`/`FileCertificateStore`, and by an isolated live test (a throwaway self-signed CA, on
  DockYarp's exact base image, on its own Docker network) confirming `SSL_CERT_FILE` establishes trust. No
  DockYarp code changes follow from this guide.
