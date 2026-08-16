## 1. Page scaffolding (AG-DOC)

- [x] 1.1 Create `docs-site/content/en/docs/migrating-from-nginx-proxy.md` with front matter `title`, `weight: 5`,
      a one-line `description`.
- [x] 1.2 Bump `weight` by one on `architecture.md` (5→6), `deployment.md` (6→7), `contributing.md` (7→8), and
      `releasing.md` (8→9) — no other content changes to those files.

## 2. Basic migration path (AG-DOC)

- [x] 2.1 Write the basic-path section: a typical nginx-proxy + `acme-companion` (public ACME) compose stack →
      the DockYarp equivalent, pointing at `examples.md`'s "Base stack" for the DockYarp-side compose shape
      rather than duplicating it.
- [x] 2.2 Table or list mapping the common nginx-proxy labels/env vars used in a basic setup to their DockYarp
      equivalents, cross-referencing `configuration.md` rather than restating its full reference.

## 3. Advanced migration path (AG-DOC)

- [x] 3.1 Write the advanced-path section: the `nginx-proxy`/`docker-gen`/`acme-companion` trio,
      environment-variable-based backend configuration (not labels), multiple Docker networks — generalized
      patterns, no literal hostnames/domains/IPs from the source material.
- [x] 3.2 State plainly that migration is iso for every backend stack — only the front-door (nginx-proxy) stack
      is replaced.

## 4. Certificates and rollback (AG-DOC)

- [x] 4.1 Write the shared certificates section: copy (never move) `nginx/certs/<host>.crt`+`<host>.key` into
      DockYarp's `Tls:CertificateDirectory` before first start; state plainly that no format conversion is
      needed (verified this session against `PemCertificateLoader`) and that DockYarp auto-detects and reuses
      them, skipping ACME provisioning unless near expiry.
- [x] 4.2 State the rollback story explicitly: because the copy is non-destructive, the original nginx-proxy
      installation's files are untouched and the old stack can be restarted at any point during migration.

## 5. Private ACME CA (advanced path) (AG-DOC)

- [x] 5.1 Document `Tls:AcmeDirectoryUri` for pointing DockYarp's ACME client at a private CA.
- [x] 5.2 Document mounting a combined CA bundle (system CAs + the private root) and setting `SSL_CERT_FILE` on
      the DockYarp container so its own ACME/HTTP calls trust that CA — state this was verified with an
      isolated live test (throwaway CA, DockYarp's exact base image, no ambient trust), not assumed from docs.
- [x] 5.3 Confirm/state the HTTP-01 vs DNS-01 distinction: `ACME_HTTP_CHALLENGE_LOCATION` in acme-companion
      configs controls which component writes the nginx challenge-location config, not the challenge type;
      absence of DNS-provider credentials in a real config implies HTTP-01, which DockYarp already supports.

## 5a. Worked examples (AG-DOC, added after user review of the first draft)

- [x] 5a.1 Basic migration: a before/after compose pair (nginx-proxy+acme-companion+one labeled backend →
      the DockYARP base stack, backend copied unchanged).
- [x] 5a.2 Advanced migration: a before/after compose set (the full trio + two env-var-configured backend
      stacks + a private CA → the DockYARP base stack with the private-CA settings, backends copied unchanged).
      Caught and fixed a mistake in my own first draft: two backend stacks were shown as one invalid YAML block
      with a duplicated `services:` key — split into two separate fenced blocks, one per stack's own file.
- [x] 5a.3 Re-verified: grepped the expanded page for every real identifier again (zero matches), parsed all 6
      fenced YAML blocks with a YAML parser (all valid), rebuilt the docs site (succeeded).
- [x] 5a.4 User asked to verify both examples against nginx-proxy's own docs (README/wiki), not memory —
      fetched `nginx-proxy/nginx-proxy`'s README + wiki Docker-Compose-Example page and
      `nginx-proxy/acme-companion`'s `docs/Docker-Compose.md` (the exact two- and three-container examples).
      Found and fixed real inaccuracies in the first draft: (1) official examples use `VIRTUAL_HOST` as a list-
      style **environment variable**, not a label — every official example checked used env vars, so the
      basic-path example (and its "labels on each backend container" framing) was corrected to match; (2) the
      basic example invented `volumes_from: [nginx-proxy]` — the real docs use explicit named shared volumes
      (`certs`/`html`/`acme`), no `volumes_from`; (3) both examples were missing the
      `com.github.nginx-proxy.nginx`/`com.github.nginx-proxy.docker-gen` labels acme-companion/docker-gen use
      to identify their sibling containers — a real functional detail, not cosmetic; (4) the advanced example's
      `conf.d` mount ro/rw split and the docker-gen `command`/template-mount shape now match the verified
      three-container example exactly. Removed an invented `networks: webservices external: true` block that
      wasn't part of any verified source. Rebuilt the docs site again after the corrections (succeeded).
- [x] 5a.5 User clarified the real motivation for the three-container split: using the **official** `nginx`
      image directly, decoupled from `nginxproxy/nginx-proxy`'s own release cadence for security patches —
      added that rationale to the Advanced migration intro (it previously only described *what* the trio is,
      not *why* someone would choose it). Also fixed the intro/basic-path step-3 wording left over from the
      original "labels" framing to match the corrected examples. Rebuilt the docs site again (succeeded).
- [x] 5a.6 User extended the argument symmetrically: DockYARP itself must keep pace with its own base image's
      (.NET + Linux) security patches, the same concern the nginx-image split addresses on the nginx-proxy
      side. Verified before writing (not stated from memory): confirmed `Dockerfile`'s `FROM
      mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`, `renovate.json`'s `pinDigests: true` on the Docker
      base image, and `.github/workflows/base-image-refresh.yml`'s `push: branches: [main], paths: [Dockerfile]`
      trigger are all real and current — added a paragraph stating this mirrored concern and how it's addressed.
      Rebuilt the docs site again (succeeded).
- [x] 5a.7 User flagged an image-name inconsistency: `dockyarp:local` was used here without the real published
      name. Confirmed `gcelet/dockyarp` against `README.md`'s existing Docker Hub badge, updated both worked
      examples to `image: gcelet/dockyarp  # or dockyarp:local for a local build`. The same inconsistency exists
      in already-shipped `deployment.md`/`examples.md`/`docker-compose.yml` — folded into
      `fix-getting-started-socket-bind` (already touching those files) rather than a new item. Rebuilt the docs
      site again (succeeded).

## 6. Validation (AG-DEP / AG-DOC)

- [x] 6.1 Built the docs site locally (`./build.ps1 Docs`) — succeeded; verified in the built output that the
      nav order is Examples → **Migrating from nginx-proxy** → Architecture, and the page itself was generated.
- [x] 6.2 Grepped the finished page for every real identifier from the private recap file (the real domain
      suffix, NAS name, CA name, internal IP ranges, MAC prefix) — zero matches. No conversion needed beyond
      this check since the page was written from generalized patterns, not copy-pasted, but verified rather
      than assumed.
- [x] 6.3 Run `npx @fission-ai/openspec@latest validate add-doc-nginx-proxy-migration-guide --strict`.
