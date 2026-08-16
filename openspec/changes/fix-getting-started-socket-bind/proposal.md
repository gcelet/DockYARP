## Why

`getting-started.md`'s quick-start example directly bind-mounts the Docker socket into the DockYARP container.
DockYARP's runtime image is explicitly non-root (chiseled, `APP_UID`, no shell), so on a native Linux host the
socket (typically `root:docker` mode `0660`) cannot be opened directly — the first example a new user runs
likely fails with a permission error. That same example also published host ports 80/443 onto **container**
80/443, but the non-root image listens on 8080/8443 by default — a second, independent way the example never
actually worked. `examples.md`'s base stack already uses the correct `tecnativa/docker-socket-proxy` pattern but
describes it as "so the proxy stays non-root", which reads as an optional hardening choice rather than the
functional requirement it actually is. **`deployment.md`'s production Docker Compose example has the identical
socket-bind and port-mapping bugs** (found while fixing `getting-started.md` — same root cause, same fix).
Separately, the DockYARP image reference is inconsistent across the site (bare `dockyarp` in `deployment.md`,
`dockyarp:local` in `examples.md`, no mention of the real published `gcelet/dockyarp` anywhere but the README
badge).

## What Changes

- `getting-started.md`: replace the direct-socket-mount quick-start with the `tecnativa/docker-socket-proxy`
  pattern (matching `examples.md`'s base stack), so the first example a reader runs actually works.
- `examples.md`: reword the base stack's intro so the socket-proxy is stated as required for the non-root image
  to reach the Docker API at all, not phrased as optional hardening.
- `deployment.md`: replace the same direct-socket-mount + wrong host-port-mapping (80:80/443:443 onto a
  non-root image that listens on 8080/8443) with the socket-proxy pattern and the correct port mapping — the
  same fix as `getting-started.md`, in the page whose whole purpose is "how to deploy this correctly".
- Image-naming consistency: `deployment.md`, `examples.md`, and `docker-compose.yml` consistently reference
  `gcelet/dockyarp` as the published image, with `dockyarp:local` documented as the local-build alternative
  (`image: gcelet/dockyarp  # or dockyarp:local for a local build`, matching the pattern already used in the
  nginx-proxy migration guide).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `documentation`: the "Worked configuration examples" requirement's base-stack scenario states the socket-proxy
  is required (not optional) for the non-root image; the "Documentation site scaffold" requirement's Getting
  Started content must use a working quick-start (no direct socket bind) and the real published image name.

## Impact

- `docs-site/content/en/docs/getting-started.md` — quick-start example rewritten.
- `docs-site/content/en/docs/examples.md` — base stack intro reworded.
- `docs-site/content/en/docs/deployment.md` — image name corrected to `gcelet/dockyarp`.
- `docker-compose.yml` (repo root) — image name corrected/annotated.
- No application code changes (`src/`, `tests/`) — documentation-only fix.
