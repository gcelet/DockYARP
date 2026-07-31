# Design — add-env-var-config

## Context
`LabelParser.TryParse` and the `Has*`/`ParseCommon`/multiports reads in `ContainerMapper` all read
`container.Labels`. `ContainerInfo` carries `Labels` only; `DockerContainerSource` populates it from the
Docker **`ListContainers`** response, which does **not** include environment variables. nginx-proxy reads the
`VIRTUAL_*` family from container env vars (env-only), so DockYarp is incompatible with `-e VIRTUAL_HOST=…`.

## Decisions

### 1. Read env via inspect; carry it on `ContainerInfo`
`ListContainers` omits env, so `DockerContainerSource` calls `InspectContainerAsync(id)` per discovered
container and reads `Config.Env` (a `KEY=VALUE` list). `ContainerInfo` gains `Env`
(`IReadOnlyDictionary<string,string>`, default empty). This is the Docker-heavy part (one inspect per
container) — acceptable for now; a change-driven / batched inspect is a later optimization.

### 2. Effective config = env ∪ labels, env-priority (a pure merge)
Add `LabelParser.EffectiveConfig(ContainerInfo)`: start from the labels, then overlay the env vars so an
**env var overrides a same-named label** (env is nginx-proxy's canonical channel; the label is the fallback).
Keys are matched with an ordinal comparer (Docker env/label names are case-sensitive). This pure function is
the headline, fully unit-tested.

### 3. Route all config reads through the merged view
`LabelParser.TryParse(container)` reads from `EffectiveConfig(container)` (port inference still uses
`container.ExposedPorts`). `ContainerMapper` computes the effective config **once** per container and passes it
to the multiports detection, `ParseCommon`, and every `Has*` diagnostic (previously `container.Labels`). So an
env-set `VIRTUAL_HOST`, `VIRTUAL_HOST_MULTIPORTS`, `DOCKYARP_LB`, etc. all take effect. `TryParse`'s public
signature is unchanged (it merges internally), keeping existing tests intact.

### 4. Backward compatible
When a container sets no env vars, `EffectiveConfig` returns the labels unchanged — current behavior. All
`DOCKYARP_*` and nginx-proxy-named labels keep working.

## Verification
- **Unit**: `EffectiveConfig` (env-only, label-only, env-wins, empty), and `TryParse`/`ContainerMapper` reading
  config from `Env` (host+port from env → routed).
- **Deferred (e2e)**: the live per-container inspect and `-e VIRTUAL_HOST` round-trip need a Docker session →
  new item `e2e-env-var-config`. `DockerContainerSource` wiring is covered by inspection (it wraps the daemon).

## Risks
- One extra Docker API call per container (inspect). Bounded by the existing reconcile cadence; optimize later
  (inspect on change only). Env parsing splits on the first `=` (values may contain `=`).
