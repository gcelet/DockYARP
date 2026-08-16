---
id: fix-getting-started-socket-bind
capability: documentation
agent: AG-DOC
tier: C-doc
priority: high
status: backlog
nginx-proxy: (internal — doc correctness bug, no parity row)
provenance: 2026-08-16 user caught it while reviewing add-doc-nginx-proxy-migration-guide's worked examples
---

## Why
`getting-started.md`'s quick-start example directly bind-mounts the Docker socket into the DockYarp container
(`-v /var/run/docker.sock:/var/run/docker.sock`). DockYarp's runtime image is explicitly non-root (chiseled,
`APP_UID`, no shell — see the Dockerfile's own comments). On a native Linux host, `/var/run/docker.sock` is
typically owned `root:docker` mode `0660`; a non-root container process with no matching GID mapping cannot
open it. **The first example a new user runs likely fails with a permission error.** `examples.md`'s "Base
stack" already uses the correct pattern (`tecnativa/docker-socket-proxy`) but only explains it as "so the proxy
stays non-root" — phrased like a hardening preference, not the functional requirement it actually is.

## nginx-proxy behavior
N/A — internal initiative (DockYarp's own doc correctness, not a proxy feature). No `parity.md` row.

## DockYarp today
- `getting-started.md`'s "Run DockYARP" section: a bare `docker run` with a direct `docker.sock` bind mount —
  confirmed broken against the actual non-root chiseled image on a standard Linux Docker install.
- `examples.md`'s "Base stack" already does this correctly (`tecnativa/docker-socket-proxy` + a read-only
  socket mount into the proxy, not into `dockyarp` itself) but doesn't state plainly that a direct bind
  wouldn't work — a reader could reasonably think the socket-proxy is optional extra hardening.
- Confirmed by reading the Dockerfile (non-root, chiseled, `APP_UID`) before writing this stub, not assumed.

## Proposed change (sketch)
- `getting-started.md`: replace the direct-socket-mount quick-start with the socket-proxy pattern (or, at
  minimum, add an explicit, prominent note that a direct bind mount will not work against the non-root image
  and point at the socket-proxy pattern instead).
- `examples.md`: reword the "Base stack" intro so the socket-proxy is stated as a **requirement** (the
  non-root image cannot open the socket directly), not an optional hardening choice — "so the proxy stays
  non-root" reads as a nice-to-have when it is actually the reason the direct approach doesn't work at all.
- **Image-name consistency** (user-flagged, same review): the DockYarp image reference is inconsistent across
  the site — `deployment.md` uses bare `dockyarp`, `examples.md` and `docker-compose.yml` use `dockyarp:local`
  with no mention of the real published name. The user confirmed the real published name is **`gcelet/dockyarp`**
  (already the README's Docker Hub badge target) and wants it used consistently as the "official image" example
  everywhere, with `dockyarp:local` kept as the documented local-build alternative (matching the pattern already
  fixed in `add-doc-nginx-proxy-migration-guide`: `image: gcelet/dockyarp  # or dockyarp:local for a local build`).
  Sweep `deployment.md`, `examples.md`, and `docker-compose.yml` for this.

## Acceptance criteria (→ scenarios)
- **WHEN** a new user follows `getting-started.md`'s quick-start verbatim on a standard Linux Docker install
  **THEN** the example works — either via the socket-proxy pattern, or a clear warning steers them to it
  instead of a silent permission failure.
- **WHEN** a reader consults `examples.md`'s Base stack **THEN** it's clear the socket-proxy is required for
  the non-root image to reach the Docker API at all, not an optional hardening step.
- **WHEN** a reader looks at any DockYarp image reference across the site **THEN** it consistently shows
  `gcelet/dockyarp` as the published image, with `dockyarp:local` documented as the local-build alternative.

## Notes / risks / references
- Discovered while reviewing `add-doc-nginx-proxy-migration-guide`'s worked examples (which correctly used the
  socket-proxy pattern, prompting the check) — kept as its own item per the lifecycle rule rather than folded
  into that unrelated change. The image-naming fix was folded in here rather than yet another new item, since
  it touches the same files already in scope.
- Refs: `Dockerfile` (non-root/chiseled comments), `docs-site/content/en/docs/{getting-started,examples,deployment}.md`,
  `docker-compose.yml`, `README.md` (existing Docker Hub badge — the source of truth for the real name).
