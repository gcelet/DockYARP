## Why
nginx-proxy's canonical configuration channel is the **container's environment variables** — the classic usage
is `docker run -e VIRTUAL_HOST=app.example.com -e VIRTUAL_PORT=8080 …`, and the whole `VIRTUAL_*` family is
**env-only** in nginx-proxy. DockYarp reads **labels only**, so that primary usage does not work today.

## What Changes
- Discovery reads each container's **environment variables** (via a Docker inspect) in addition to its labels.
- Configuration is parsed from a **merged** key/value view where an **environment variable takes precedence**
  over a same-named label (env is the canonical channel; the existing label remains a valid fallback).
- All existing labels keep working; nothing changes when a container sets no relevant env vars.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: configuration is read from container environment variables and labels (env wins).

## Impact
- **Code**: `DockYarp.Docker` — `ContainerInfo.Env`; `LabelParser.EffectiveConfig` (pure env∪labels merge,
  env-priority) consumed by `TryParse` and the `Has*`/`ParseCommon`/multiports reads in `ContainerMapper`;
  `DockerContainerSource` inspects each container to populate `Env`.
- **Tests (unit)**: `LabelParser.EffectiveConfig` (env-only / label-only / env-wins / empty), and parsing +
  mapping from env (`VIRTUAL_HOST`/`VIRTUAL_PORT` as env vars route the container).
- **Runtime / e2e (deferred)**: the per-container **inspect** adds a Docker call per container; validating the
  live inspect flow + `-e VIRTUAL_HOST` end to end needs a Docker-capable session — batch with the other e2e
  items (a follow-up `e2e-env-var-config`). The merge + parsing are fully unit-tested here.
- **Out of scope (split)**: recognizing the `com.github.nginx-proxy.nginx-proxy.*` **label namespace** →
  `add-nginx-label-aliases`.
- **Owning agent**: AG-DD. Resolves `add-env-var-config`.
