## Context

DockYarp today ships as a regular JIT-compiled, framework-dependent-then-self-contained publish
(`build/Build.cs`'s `Publish`/`DockerImage` targets). Native AOT would remove the JIT and trim to a
single native binary, but AOT forbids runtime reflection/codegen and requires every dependency in the
graph to be trim/AOT-safe. See `proposal.md - Why` for the motivation; see the backlog item's own
assessment (`openspec/backlog/items/investigate-aot-build.md`) for the suspected blockers — YARP and
Docker.DotNet are not AOT-annotated, and reflection-based config binding, ACME (Certes), and the
OpenTelemetry exporters are all reflection-heavy.

## Goals / Non-Goals

**Goals:**
- Get a real, measured feasibility verdict for Native AOT — not the backlog item's inference — by
  actually publishing with `PublishAot=true` and trimming analyzers on, and capturing the resulting
  warning list per dependency.
- If AOT is blocked (expected), measure the pragmatic alternative (`PublishReadyToRun=true`) against
  the current JIT publish: startup time and image size.
- Record a single decision — AOT / R2R / status quo — that closes the backlog item either way.

**Non-Goals:**
- Making DockYarp AOT-compatible. If blocked, no attempt is made to work around or replace the blocking
  dependencies (that would be new, separately-scoped work, not this spike).
- Committing any AOT/trim publish profile to the repository. The AOT publish is throwaway — a local,
  uncommitted `dotnet publish` run against `src/DockYarp.App` with the relevant MSBuild properties set on
  the command line, not a checked-in project change.
- Re-running or re-validating the OCSP/DNS-01/other in-flight backlog items — this spike touches only
  publish/trim configuration.

## Decisions

- **Spike method**: run `dotnet publish src/DockYarp.App -r win-x64 -p:PublishAot=true
  -p:TrimmerSingleWarn=false` (and the linux-x64 RID, since the shipped image targets Linux) locally,
  without committing any project file changes, and capture the full IL2xxx/IL3xxx trim-analysis and
  AOT-analysis warning output.
- **Feasibility criterion**: AOT is "blocked" if the publish either fails outright or emits warnings
  attributable to a dependency DockYarp cannot alter (YARP, Docker.DotNet, Certes, OpenTelemetry
  exporters) rather than to DockYarp's own code. Warnings confined to DockYarp's own reflection-based
  config binding are not a hard blocker (already mitigable with the source-generated options binder) —
  only third-party dependency warnings settle the verdict.
- **If blocked, fallback measurement**: publish the same `win-x64`/`linux-x64` targets with
  `PublishReadyToRun=true` (no trimming) and compare cold-start time (3 runs, median) and published
  output size against the current JIT self-contained publish, using the existing `dockyarp:local` image
  build as the JIT baseline.
- **Where the verdict lives**: this design.md is amended in place with a `## Spike Result` section
  once the publish runs complete (see `tasks.md`) — the verdict is the deliverable, not new product
  code. The backlog item stub is removed on archive per the standard change lifecycle regardless of
  which way the decision falls.

## Risks / Trade-offs

- [Risk] The AOT publish may take several attempts to even complete (missing trimmer feature switches,
  RID-specific failures) before producing a clean warning list. → Budget the task as exploratory; report
  whatever warning list is obtained even if the publish never fully succeeds — a failed publish with a
  concrete error is itself a valid (negative) verdict.
- [Risk] YARP or Docker.DotNet may have improved their AOT annotations since 2026-08-14. → Re-check
  each package's current version and AOT compatibility notes at spike time rather than assuming the
  backlog item's original assessment still holds.
- [Trade-off] R2R measurement adds scope beyond a pure feasibility check, but the backlog item explicitly
  asks for "AOT / R2R / status quo" as the recorded decision, not just an AOT yes/no — so it is included
  here rather than deferred to a second change.

## Migration Plan

Not applicable — no shipped behavior changes. If R2R is the chosen outcome, wiring it into the Fallout
`Publish` target behind an explicit opt-in flag is captured as this change's implementation task; rollback
is simply not setting that flag (JIT publish remains the default).

## Spike Result (2026-08-23)

All three publish modes (`linux-x64`, self-contained, matching the shipped `mcr.microsoft.com/dotnet/sdk:10.0`
image) were built and measured inside a container matching the production `Dockerfile` base.

**Native AOT publish succeeded** (`dotnet publish -p:PublishAot=true`, exit code 0) — a working binary was
produced — but emitted **414 trim/AOT analysis warnings**. Classified by origin:

| Origin | Count | Fixable, and by whom? |
| --- | --- | --- |
| ASP.NET Core MVC / Razor Pages / Razor Components internals (pulled in transitively by `DockYarp.Dashboard`'s single page, `Pages/Dashboard/Index.cshtml`, via `AddRazorPages`) | ~228 | **Yes, DockYarp-side.** Microsoft documents Razor Pages/Components as not supporting trimming/AOT (<https://aka.ms/aspnet/trimming>). [RazorSlices](https://github.com/DamianEdwards/RazorSlices) is a confirmed AOT/trim-compatible replacement ("Full support for trimming and native AOT... with Minimal APIs"). The Dashboard's entire surface today is one page + code-behind — a bounded rewrite to a `RazorSlice` behind a minimal-API endpoint, not a rearchitecture. |
| `Newtonsoft.Json` (transitive — the currently-pinned `Docker.DotNet` **3.125.15** depends on it on both TFMs) | ~135 | **Yes, DockYarp-side, via a package swap.** `dotnet/Docker.DotNet` itself is confirmed inactive (last release 2023-05-18, last commit 2024-10-30) — its maintainers say as much on [PR #706 "Support AOT"](https://github.com/dotnet/Docker.DotNet/pull/706) (open, unmerged). That PR's own review comment reveals the real path: **[`testcontainers/Docker.DotNet`](https://github.com/testcontainers/Docker.DotNet)** is an actively maintained fork (releases up to `v4.3.3`, 2026-06-28; commits as recent as 2026-08-17) published on NuGet as **`Docker.DotNet.Enhanced`**, which already targets `net8.0`/`net9.0`/`net10.0`, has fully dropped `Newtonsoft.Json` for `System.Text.Json`, and declares `<IsAotCompatible>true</IsAotCompatible>` in its `Directory.Build.props`. Swapping the package reference is a real option today — see `migrate-to-docker-dotnet-enhanced`. |
| `YamlDotNet` (used directly by `DockYarp.Docker.Labels.MultiportParser` for label parsing — a live discovery code path) | ~35 + 1 direct call site | **Yes, DockYarp-side, today.** YamlDotNet ships an official source generator, `Vecc.YamlDotNet.Analyzers.StaticGenerator` — swap `DeserializerBuilder` for `StaticDeserializerBuilder`, add a `[YamlStaticContext]` class with `[YamlSerializable]` on every deserialized label-config type (see <https://andrewlock.net/using-the-yamldotnet-source-generator-for-native-aot/>). Requires enumerating every type reachable from label parsing, but no upstream dependency. |
| Other `Microsoft.Extensions.*` DI/reflection helpers (part of the same ASP.NET Core MVC pipeline pulled in by the Dashboard) | ~9 | Same fix as the Dashboard row — resolved once Razor Pages is dropped. |
| `Docker.DotNet`'s own code (`QueryString<T>`, query-string type conversion — used to build every Docker API filter) | ~3 | Same package-swap fix as the `Newtonsoft.Json` row — `Docker.DotNet.Enhanced` rewrote this surface as part of its `IsAotCompatible` work. |
| `Org.BouncyCastle` (pulled in via `Certes`'s ACME crypto) | 1 | Not investigated in this pass — a single warning, low priority relative to the other three. |
| DockYarp's own code (`AdminEndpoints.cs` minimal-API `Results.Json(...)` calls, `StaticConfigProvider.cs` JSON deserialization) | 14 | **Yes, DockYarp-side, today.** Mitigable with `JsonSerializerContext` source generation (standard .NET pattern, no third-party dependency). |

**Package metadata check** (via `dotnet-inspect`, 2026-08-23): `Yarp.ReverseProxy` 2.3.0 declares
`AssemblyMetadata(IsTrimmable)=True` but no `IsAotCompatible` attribute (highest TFM `net8.0`) — consistent
with it producing almost no warnings of its own in the log. The currently-pinned `Docker.DotNet` 3.125.15
declares **no** trim/AOT metadata at all (`netstandard2.1`); its actively maintained fork,
`Docker.DotNet.Enhanced`, explicitly declares `IsAotCompatible=true`. `Certes` 3.0.4 likewise declares none
(`net6.0`) and was not investigated further this pass. By contrast, the OpenTelemetry packages DockYarp
actually uses (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`) both
declare `IsAotCompatible=True` — the original backlog assessment's suspicion of the OTEL exporters does not
hold today.

**Feasibility verdict, revised twice**: the first revision (above) treated `Docker.DotNet` as the one
blocker outside DockYarp's control, based on the still-open, unanswered
[dotnet/Docker.DotNet#689](https://github.com/dotnet/Docker.DotNet/issues/689). A second look — prompted by
the user pointing at [PR #706](https://github.com/dotnet/Docker.DotNet/pull/706) and its review thread —
found that this framing undersold what's actually available: **`Docker.DotNet.Enhanced`
(`testcontainers/Docker.DotNet`) is a real, live, `IsAotCompatible=true` alternative package, published and
actively maintained today.** With this, **none of the three warning sources is a hard, unresolvable-by-
DockYarp blocker anymore** — all three (YamlDotNet, the Dashboard's Razor Pages, and Docker.DotNet's
Newtonsoft.Json/reflection surface) have a concrete, actionable path that does not depend on waiting for a
third party. What remains is *migration effort*, not *infeasibility*: `Docker.DotNet.Enhanced` uses a
different construction API (`DockerClientBuilder` vs. the current `DockerClientConfiguration`) and a
different package id, so switching needs real validation against DockYarp's actual Docker API usage (one
call site: `src/DockYarp.Docker/Discovery/DockerContainerSource.cs`) — not a drop-in version bump. The
successful AOT publish exit code (below) does not validate any of this either way: the smoke test never
exercised Docker discovery (no `docker.sock` mounted), label parsing (no labels present), or the Dashboard
UI (route never hit) — the very paths the warnings flag as risky, and the very paths a package swap would
need to be proven against.

**Measured startup/size** (self-contained `linux-x64` publish, median of 3 runs, time to the
`Now listening` log line; sizes are the raw published output directory):

| Publish mode | Median startup | Published size |
| --- | --- | --- |
| Native AOT | ~209 ms | 111 MB |
| ReadyToRun (`PublishReadyToRun=true`) | ~414 ms | 120 MB |
| JIT (current default, self-contained) | ~467 ms | 112 MB |

AOT's startup win is real and substantial (≈2.2× faster) if the trim risk were ever eliminated. R2R's win
is much smaller (~11% faster) and comes with a **larger**, not smaller, published output (pre-JITted native
code sections add size without removing anything, since R2R does no trimming) — a real cost, not a free
lunch. For a long-running reverse proxy that is not restarted per-request, an ~50 ms one-time boot saving
does not justify a second publish/test path in the Fallout build.

**Decision: status quo for this spike, but AOT is reclassified from "blocked" to "achievable, pending
three prep items."** Neither Native AOT nor ReadyToRun is adopted in this change — this spike does not
implement any of the fixes, since each is real, non-trivial migration work in its own right and belongs in
its own scoped change with its own tests. R2R's measured benefit is too marginal (~11% faster, and a
*larger* published output) to justify adopting it on its own, independent of the AOT question. This closes
the backlog item per its own acceptance criteria ("a concrete alternative is recommended... or explicitly
deferred") — the concrete alternative recommended here is: do the three prep items, then re-run this
spike's measurement method to confirm.

**No dependency is a hard, permanent blocker anymore** — this is the key correction versus the first pass:
1. `fix-yamldotnet-aot-trim` — YamlDotNet's static source generator, DockYarp-side only.
2. `migrate-dashboard-to-razorslices` — RazorSlices instead of Razor Pages, DockYarp-side only, bounded
   (one page).
3. `migrate-to-docker-dotnet-enhanced` — switch from `Docker.DotNet` 3.125.15 to the actively maintained,
   `IsAotCompatible=true` fork `Docker.DotNet.Enhanced` (`testcontainers/Docker.DotNet`). This is the item
   that most changes the picture from the first pass: it replaces "wait for an unresponsive upstream" with
   "adopt an available, live package" — real migration effort (a different construction API, a different
   package id), but no external dependency on someone else's timeline.

**Revisit trigger for actually adopting Native AOT:** once all three prep items ship, re-run this spike's
measurement (`-p:PublishAot=true`, classify remaining warnings) to confirm the warning count has actually
dropped to zero (or to a residual DockYarp can accept) before committing to AOT as the default publish
mode. Until then, the JIT publish stays the default and this decision is not itself a commitment to ship
AOT once the prep items land — that is a separate call to make once the real number is known.

**Follow-up backlog items opened** for all three DockYarp-side-actionable warning sources, so the prep work
toward a real Native AOT publish is tracked: `fix-yamldotnet-aot-trim` (YamlDotNet's static source
generator), `migrate-dashboard-to-razorslices` (replacing the Dashboard's single Razor Page), and
`migrate-to-docker-dotnet-enhanced` (switching to the actively maintained, `IsAotCompatible=true`
Docker.DotNet fork). Each item's "Notes" section cross-links the other two and states that Native AOT
adoption itself is a separate decision, made once all three land and this spike's measurement is re-run.
