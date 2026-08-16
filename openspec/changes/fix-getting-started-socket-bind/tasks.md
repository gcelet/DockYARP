## 1. Getting Started quick-start (AG-DOC)

- [x] 1.1 Replace `getting-started.md`'s direct `-v /var/run/docker.sock:/var/run/docker.sock` quick-start with
      the `tecnativa/docker-socket-proxy` pattern (matching `examples.md`'s base stack), using `gcelet/dockyarp`
      as the image.
- [x] 1.2 Re-read the rewritten section end-to-end to confirm it stands alone (a first-time reader hasn't seen
      `examples.md` yet) and mentions `dockyarp:local` as the local-build alternative. Also fixed a second latent
      bug found while rewriting: the old example published host 80/443 onto **container** 80/443, but the
      non-root image listens on 8080/8443 by default — now `ports: ["80:8080", "443:8443"]`, matching
      `examples.md`.

## 2. Examples base stack wording (AG-DOC)

- [x] 2.1 Reword `examples.md`'s base stack introduction so the socket-proxy is stated as required for the
      non-root image to reach the Docker API, not "so the proxy stays non-root".
- [x] 2.2 Confirm the base stack's `image:` line already uses `gcelet/dockyarp` (or update it) with the
      `dockyarp:local` alternative annotated, matching the migration guide's established phrasing. It was
      `dockyarp:local # or your published image` — corrected to `gcelet/dockyarp # or dockyarp:local for a
      local build`, matching `migrating-from-nginx-proxy.md`'s established phrasing exactly.

## 3. Image-name consistency sweep (AG-DOC / AG-DEP)

- [x] 3.1 `deployment.md`: **scope extended mid-implementation (user confirmed)** — replace the direct
      `docker.sock` bind mount + wrong `80:80`/`443:443` port mapping (identical bug to `getting-started.md`,
      found while fixing it) with the socket-proxy pattern and correct `80:8080`/`443:8443` mapping, and the
      bare `dockyarp` image reference with `gcelet/dockyarp` (+ `dockyarp:local` alternative note).
- [x] 3.2 Root `docker-compose.yml`: **kept `dockyarp:local` as-is** — this file has a `build:` block, so it's
      genuinely a local-build reference stack (confirmed via the header comment: "Local demo... See
      docs/deployment.md" for the production case). Added a one-line comment pointing to `gcelet/dockyarp` as
      the published alternative, without breaking the local build/dev loop.
- [x] 3.3 Grepped the full `docs-site/content/en/docs/` tree: all 5 `image:` lines now use the annotated
      `gcelet/dockyarp # or dockyarp:local for a local build` form (deployment, examples, getting-started,
      migrating-from-nginx-proxy ×2). Also checked outside docs-site: `examples/docker-compose.group-add.yml`
      (repo root) already correctly uses `dockyarp:local` with a `build:` block and correct 8080/8443 ports —
      genuinely a local-build alternative recipe, out of this change's scope, no fix needed.

## 4. Spec sync prep (AG-DOC)

- [x] 4.1 Verified the delta spec's two MODIFIED requirements ("Documentation site scaffold", "Worked
      configuration examples") match what actually shipped in sections 1-3, including the mid-implementation
      scope extension to `deployment.md` (its own scenario was added to reflect that).
