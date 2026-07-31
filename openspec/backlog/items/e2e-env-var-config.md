---
id: e2e-env-var-config
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: container environment variables (runtime validation)
provenance: deferred from add-env-var-config, 2026-07-31
---

## Why
`add-env-var-config` reads config from container environment variables (via a per-container inspect) and merges
them over labels (env wins), unit-tested at the merge/parse level. The **live** path — the extra Docker
`inspect` per container and an `-e VIRTUAL_HOST=…` container actually being routed — needs a Docker session.

## nginx-proxy behavior
- `docker run -e VIRTUAL_HOST=app.example.com -e VIRTUAL_PORT=8080 …` is the canonical usage; nginx-proxy reads
  the `VIRTUAL_*` family from container env vars.

## DockYarp today
- Env reading + env-over-label merge implemented and unit-tested (`add-env-var-config`). No live e2e proves the
  inspect flow or an env-configured container being routed.

## Proposed change (sketch)
- Add an Aspire e2e scenario with a backend configured **only via environment variables**
  (`VIRTUAL_HOST`/`VIRTUAL_PORT` as env, no labels) and assert it is discovered and routed through DockYarp.
- Assert that an env var overrides a same-named label on the same container.

## Acceptance criteria (→ scenarios)
- **WHEN** a backend sets `VIRTUAL_HOST`/`VIRTUAL_PORT` as env vars only **THEN** a request through DockYarp
  reaches it.
- **WHEN** a container sets the same key as both env and label **THEN** the env value is used.

## Notes / risks / references
- Requires a Docker-capable session; batch with the other e2e items. Watch the per-container inspect cost at
  scale (optimize to change-driven inspects if needed).
- Sibling (done): `add-env-var-config` (merge + parse). Related: `add-nginx-label-aliases`.
