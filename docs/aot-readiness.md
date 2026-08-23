# Native AOT / trim readiness

DockYarp does not publish via Native AOT today (the shipped image is a JIT self-contained publish) — adopting
it is a separate, still-open future decision. This document tracks the **warning budget** on the way there,
so new code doesn't quietly regress work already landed.

**Standing rule: a change must not increase the AOT/trim warning count.** If your change touches code that a
`-p:PublishAot=true` publish would analyze (most of `src/`), re-run the spike below before presenting the
change and compare against the current baseline. A new warning attributable to your own change is a real
regression to fix, not something to wave through because AOT itself isn't adopted yet.

## Current baseline

**142 total warnings** (2026-08-23, after `fix-adminapi-json-aot-trim`). History: 414 (`investigate-aot-build`
baseline) → 379 (`fix-yamldotnet-aot-trim`) → 382 (`migrate-to-docker-dotnet-enhanced`, see note below) → 170
(`migrate-dashboard-to-razorslices`) → 142 (`fix-adminapi-json-aot-trim`).

Remaining sources, all currently tracked or accepted:

| Source | Warnings | Status |
|---|---:|---|
| `Newtonsoft.Json.*` (via **Certes**, DockYarp.Tls's ACME client) | ~136 | Open — [openspec backlog item](../openspec/backlog/items/investigate-certes-aot-alternative.md) `investigate-certes-aot-alternative`. Real risk this stays blocked; no known alternative yet. |
| `System.Linq.Expressions`/`Microsoft.CSharp.RuntimeBinder` (BCL) | ~5 | Downstream of Certes/Newtonsoft's own `dynamic` usage — folds into the item above, not separately tracked. |
| `Org.BouncyCastle.Utilities` (via `Portable.BouncyCastle`, CRL parsing in DockYarp.Tls) | 1 | Unrelated third-party, single line, not currently worth its own item. |

> The 379→382 step is not a clean regression — that measurement's own conditions (machine state, incremental
> build starting point) weren't reproducible from the prior session; see that change's archived `tasks.md`.
> Every measurement from `migrate-dashboard-to-razorslices` onward was taken with a consistent, verified
> methodology (clean `obj`/`bin`, the exact command below) and *is* directly comparable.

## Re-running the spike

```powershell
# One-time per shell: MSBuild's native-link step invokes vswhere.exe by bare name.
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH

dotnet publish src/DockYarp.App -r win-x64 -c Release -p:PublishAot=true `
  -p:TrimmerSingleWarn=false -p:TreatWarningsAsErrors=false -o "$env:TEMP\aot-spike-out"
```

- Requires the "Desktop development with C++" VS workload (native linker). If missing, `error : Platform
  linker not found` — see `openspec/changes/archive/2026-08-23-migrate-to-docker-dotnet-enhanced/` and
  `2026-08-23-migrate-dashboard-to-razorslices/`'s archived `tasks.md` for how this was diagnosed and fixed
  once already.
- **Clear `obj`/`bin`** for any project you changed before trusting the result — an incremental rerun against
  stale intermediates from an earlier *failed* attempt can silently report **fewer warnings than real**, a
  false negative caught live during this session.
- Count with `grep -cE ' warning IL[0-9]' <log>` (or equivalent); trace a specific source with
  `grep '<Namespace.Or.FilePath>' <log>`.

## Lessons that will bite again

- **A `JsonSerializerContext` registered only via DI (`ConfigureHttpJsonOptions`/`TypeInfoResolverChain`)
  does NOT silence the trim/AOT analyzer for `Results.Json(...)`/`JsonSerializer.Deserialize<T>(...)` calls.**
  The analyzer flags a call site based on which overload the C# compiler bound to **at compile time** — not
  on what `JsonSerializerOptions`/resolver chain DI happens to supply at runtime. You must pass the context
  (or a `JsonTypeInfo<T>`) **explicitly as an argument** at the call site
  (`Results.Json(value, MyContext.Default)`, `JsonSerializer.Deserialize(json, MyContext.Default.MyType)`).
  `BadRequest`/`NotFound` are the one real exception — they have no context-accepting overload at all, so DI
  registration is the *only* mechanism for them, and it does work there. First discovered in
  `fix-adminapi-json-aot-trim` — a spike after "fixing" this via DI alone showed zero warnings removed.
- **Passing a `JsonSerializerContext` directly to `Results.Json` uses *that context's own*
  `JsonSerializerOptions`, not the ambient DI-configured ones.** A source-generated context defaults to
  PascalCase; ASP.NET Core minimal APIs default to camelCase. Add
  `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]` explicitly or you
  get a silent response-shape regression — only caught, in `fix-adminapi-json-aot-trim`, by the existing
  integration tests failing, not by the AOT spike itself (the spike checks warnings, not response shape; run
  both checks, neither alone is sufficient).
- **Verify "no fix exists" as rigorously as "X is broken."** `investigate-aot-build`'s first-pass verdict on
  `Docker.DotNet` was too pessimistic and got corrected by real, checkable leads (an active fork, a
  since-added source generator). Before writing a "this dependency blocks AOT, full stop" verdict into any
  future item (including `investigate-certes-aot-alternative`), check whether the ecosystem has already moved
  — a changelog, a fork, an upstream issue — rather than concluding from the current pinned version alone.
