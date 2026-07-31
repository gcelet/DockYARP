---
id: add-env-var-config
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: high
status: backlog
nginx-proxy: container environment variables (VIRTUAL_HOST & the VIRTUAL_* family are env-only in nginx.tmpl)
provenance: 2026-07-31 parity re-analysis (env-vs-label config source)
---

## Why
nginx-proxy's **canonical** configuration channel is the **container's environment variables** — the classic
usage is `docker run -e VIRTUAL_HOST=app.example.com -e VIRTUAL_PORT=8080 …`. DockYarp reads **labels only**, so
that primary usage does not work today, and the config *source* — not just the feature set — diverges from
nginx-proxy.

## nginx-proxy behavior
- docker-gen exposes both a container's `.Env` and `.Labels` as separate maps with **no built-in precedence**;
  precedence is the template's choice.
- The real nginx-proxy template reads the **`VIRTUAL_*` family from container env vars only** (`VIRTUAL_HOST`,
  `VIRTUAL_PORT`, `VIRTUAL_PROTO`, `VIRTUAL_PATH`, `VIRTUAL_DEST`, `VIRTUAL_ROOT`, `VIRTUAL_HOST_MULTIPORTS`),
  along with `CERT_NAME`, `NETWORK_ACCESS`, `SERVER_TOKENS`, `EXTERNAL_HTTP_PORT`, `EXTERNAL_HTTPS_PORT`, and
  the per-vhost env overrides `HTTPS_METHOD`/`HSTS`/`SSL_POLICY`/`ENABLE_HTTP_ON_MISSING_CERT`. A **separate,
  namespaced label set** (`com.github.nginx-proxy.nginx-proxy.*`) covers `loadbalance`, `keepalive`,
  `ssl_verify_client`, `http2.enable`, `http3.enable`, `non-get-redirect`, `trust-default-cert`,
  `debug-endpoint`.

## DockYarp today
- `LabelParser` reads `container.Labels` only; `ContainerInfo` has no `Env`; `DockerContainerSource` copies
  `response.Labels` and never reads `Config.Env`. The Docker **`ListContainers`** response does not include env
  vars, so env support needs a per-container **`ContainersInspect`** (already how docker-gen gets `.Env`).

## Proposed change (sketch)
- Add `ContainerInfo.Env` and populate it in `DockerContainerSource` via an inspect per discovered container
  (or fold into an existing inspect). Feed a **merged** key/value view (env + labels) to the existing
  `LabelParser`, with **environment variables taking precedence** over the same-named label (per the decision:
  env is canonical, the existing label is the fallback). Keep all current DOCKYARP_* labels working.
- Consider batching / caching inspects and only re-inspecting on change (events) to bound the extra Docker
  calls.

## Acceptance criteria (→ scenarios)
- **WHEN** a container sets `VIRTUAL_HOST`/`VIRTUAL_PORT` as **environment variables** **THEN** it is routed
  (the canonical nginx-proxy usage works).
- **WHEN** a value is set as both an env var and a label **THEN** the **env var wins**.
- **WHEN** a value is set only as a label **THEN** it still applies (backward compatible).

## Notes / risks / references
- Extra Docker API cost (inspect per container) — mitigate with change-driven inspects and the existing
  reconcile flow. Tier B-runtime (Docker-heavy; validate live).
- Decide whether to also recognize the real `com.github.nginx-proxy.nginx-proxy.*` label namespace for the
  label-only knobs, or keep the DockYarp `DOCKYARP_*` namespace (document the mapping either way).
- Cross-cutting: this is the headline finding of the 2026-07-31 parity re-analysis; see the new
  "Configuration source" section in `parity.md`.
