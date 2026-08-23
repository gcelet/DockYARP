---
id: migrate-dashboard-to-razorslices
capability: admin-api
agent: AG-AA
tier: A-structural
priority: low
nginx-proxy: n/a (internal finding — AOT/trim readiness, from investigate-aot-build)
provenance: 2026-08-23 investigate-aot-build spike (Native AOT feasibility investigation)
status: backlog
---

## Why

The `investigate-aot-build` spike found that `DockYarp.Dashboard`'s use of ASP.NET Core Razor Pages
(`AddRazorPages`, one page: `Pages/Dashboard/Index.cshtml` + code-behind) is the single largest source of
trim/AOT warnings in a Native AOT publish — ~228 of 414, plus ~9 more from the same MVC pipeline's DI
helpers. Microsoft documents Razor Pages and Razor Components as **not supporting trimming or Native AOT**
(<https://aka.ms/aspnet/trimming>), so there is no configuration fix here — only a replacement. Because the
Dashboard's actual surface is a single page, not a full MVC app, this is a bounded rewrite rather than a
rearchitecture, and closing it removes by far the largest warning bucket blocking a future Native AOT
publish (the remaining blocker after this and [[fix-yamldotnet-aot-trim]] would be `Docker.DotNet` alone —
see Notes).

## Assessment (2026-08-23)

[RazorSlices](https://github.com/DamianEdwards/RazorSlices) is a lightweight `.cshtml`-based templating
library built specifically for Minimal APIs and middleware, with official Native AOT and trimming support
("Full support for trimming and native AOT when used in conjunction with ASP.NET Core Minimal APIs"). It is
not a 1:1 Razor Pages replacement — no Tag Helpers, no View Components, no MVC page-model binding — but
supports layouts via `RazorLayoutSlice`, which covers the Dashboard's current needs. Given the Dashboard is
one page today, migration means: convert the page into a `RazorSlice`, replace the page handler
(`Index.cshtml.cs`) with a minimal-API endpoint (`DashboardEndpointMapping.cs` already maps endpoints
outside the page-handler model, so this is largely additive), and handle any query/form binding manually.

## Proposed change (sketch)

1. Add the `RazorSlices` package (CPM) to `DockYarp.Dashboard`.
2. Convert `Pages/Dashboard/Index.cshtml` into a `RazorSlice`-based template (adjust `_ViewImports.cshtml`
   usage accordingly — RazorSlices does not use the same view-imports model as Razor Pages).
3. Move `Index.cshtml.cs`'s logic into a minimal-API handler registered via
   `DashboardEndpointMapping`/`DashboardServiceCollectionExtensions`, replacing `AddRazorPages()`.
4. Remove the `AddRazorPages`/`AddRazorComponents` service registrations once nothing depends on them.
5. Re-run a throwaway `-p:PublishAot=true` publish (same approach as `investigate-aot-build`) and confirm
   the ASP.NET Core MVC/Razor-Pages-attributed warnings are gone.

## Acceptance criteria (→ scenarios)

- **WHEN** the admin dashboard is requested **THEN** it renders the same information it does today (routes,
  clusters, certificates, health) via the RazorSlices-based endpoint, verified by existing dashboard
  integration tests (updated as needed) still passing.
- **WHEN** a Native AOT publish is attempted **THEN** no IL2xxx/IL3xxx warning traces back to
  `Microsoft.AspNetCore.Mvc.*`/Razor Pages/Razor Components internals.

## Notes / risks / references

- This item alone does **not** unblock Native AOT — it removes one of three warning sources. The other two
  are [[fix-yamldotnet-aot-trim]] (YamlDotNet's reflection-based label parsing) and
  [[migrate-to-docker-dotnet-enhanced]] (Docker.DotNet's own `Newtonsoft.Json`/reflection surface — no
  longer believed to be an unresolvable upstream blocker; see that item). Native AOT adoption itself is a
  separate decision to make once all three land.
- RazorSlices is a smaller, less mainstream library than ASP.NET MVC — verify its maintenance status and
  API stability at propose time before committing to the migration.
- Refs: `src/DockYarp.Dashboard/` (all files), `investigate-aot-build`'s `design.md` (`## Spike Result`,
  archived under `openspec/changes/archive/`).
