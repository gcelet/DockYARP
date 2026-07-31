## 1. Model + merge (AG-DD)
- [x] 1.1 `ContainerInfo`: add `Env` (`IReadOnlyDictionary<string,string>`, default empty)
- [x] 1.2 New pure `LabelParser.EffectiveConfig(ContainerInfo)`: labels overlaid by env vars (env wins),
      ordinal keys

## 2. Route config reads through the merge (AG-DD)
- [x] 2.1 `LabelParser.TryParse`: read from `EffectiveConfig(container)` (ports still from `ExposedPorts`);
      public signature unchanged
- [x] 2.2 `ContainerMapper`: compute the effective config once per container; use it for multiports detection,
      `ParseCommon`, and every `Has*` diagnostic (was `container.Labels`)

## 3. Discovery wiring (AG-DD)
- [x] 3.1 `DockerContainerSource`: inspect each container (`InspectContainerAsync` → `Config.Env`), parse the
      `KEY=VALUE` list via the pure `ContainerEnvParser.Parse` helper, populate `ContainerInfo.Env` (empty on
      inspect failure)
- [x] 3.2 New backlog item `e2e-env-var-config` (live inspect + `-e VIRTUAL_HOST` round-trip)

## 4. Tests (AG-DD)
- [x] 4.1 `LabelParser.EffectiveConfig`: env-only key; label-only key; env wins over a same-named label; empty
      env returns the labels unchanged
- [x] 4.2 Parsing/mapping from env: a container with `VIRTUAL_HOST`/`VIRTUAL_PORT` in `Env` (no labels) is
      parsed and routed
- [x] 4.3 `ContainerEnvParser.Parse`: `KEY=VALUE` parsing (value with `=`, entries with no key skipped,
      null/empty → empty)

## 5. Verify (AG-DD)
- [x] 5.1 Nuke `Test` gate green
