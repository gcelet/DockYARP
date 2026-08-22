## 1. Fix the image reference everywhere (AG-DEP / AG-DOC)

- [x] 1.1 `docker-compose.yml`: `# image: gcelet/dockyarp` → `# image: ghcr.io/gcelet/dockyarp`.
- [x] 1.2 `docs-site/content/en/docs/getting-started.md`: same fix (1 occurrence).
- [x] 1.3 `docs-site/content/en/docs/deployment.md`: same fix (1 occurrence).
- [x] 1.4 `docs-site/content/en/docs/examples.md`: same fix (2 occurrences).
- [x] 1.5 `docs-site/content/en/docs/migrating-from-nginx-proxy.md`: same fix (2 occurrences).
- [x] 1.6 Grepped the whole repo (`image:\s*gcelet/dockyarp` without a preceding `ghcr.io/`) — the only
      remaining matches are this change's own OpenSpec artifacts (describing the bug) and two historical
      archived changes (a past decision record); no live doc/code occurrence missed.

## 2. Remove the dead `/config` mount, point at StaticConfig instead (AG-DEP)

- [x] 2.1 `docker-compose.yml`: removed the `- ./config:/config` line from the `dockyarp` service's `volumes:`.
- [x] 2.2 Added a comment in its place noting `StaticConfig:Path` as an alternate/no-Docker config source,
      linking `docs/features/#static-configuration` on the live docs site (no doc page currently covers
      `StaticConfig:Path` in `configuration.md` — confirmed via grep — only `features.md`'s "Static
      configuration" section, so linked there instead).
- [x] 2.3 `config/` turned out to already be listed in `.gitignore` (line 501) — a deliberate, correct exclusion
      (an operator-provided bind-mount source shouldn't be forced into the repo), not an oversight. No action
      needed: nothing references a concrete path under it anymore (the new comment doesn't name one), and being
      gitignored already means it never pollutes a fresh clone regardless of local presence.

## 3. Add a commented-out TLS/ACME example (AG-DEP)

- [x] 3.1 `docker-compose.yml`: added commented `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL` lines under the
      `whoami` service's `labels:`, with an explanatory comment on why they're inactive by default (needs a
      real public domain, matching the file's own existing "real ACME/TLS needs public DNS" framing).

## 4. Add a dashboard-enabling comment (AG-AA)

- [x] 4.1 `docker-compose.yml`: extended the existing comment near `AdminApi__Surface: "Api"` to mention
      `"ApiAndDashboard"` as the alternative that also serves `/dashboard`.

## 5. Final validation (AG-DEP)

- [x] 5.1 `docker compose config` — parses cleanly; confirmed the commented `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`
      lines are correctly excluded (not silently active) and the dead `config:` volume no longer appears.
- [x] 5.2 Ran the real reference stack (`docker compose up -d --build`) — `whoami` reachable exactly as
      README's own Quick start describes (`curl -H "Host: whoami.local" http://localhost/` → 200, whoami's own
      response body), and the admin API still answers (`curl -H "Host: localhost" -H "X-Api-Key: change-me"
      http://localhost/api/health` → `{"status":"Healthy",...}`) — confirms the `AdminApi__Surface` comment
      edit didn't disturb the actual env var value. Torn down after (`docker compose down -v`).
- [x] 5.3 `cd docs-site && npm run build` — Hugo build succeeds (16 pages, 42 static files, matching the
      favicon change's prior count — no regression), no broken-link warnings.
