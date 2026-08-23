## Why

`investigate-aot-build` found `DockYarp.Dashboard`'s use of ASP.NET Core Razor Pages (`AddRazorPages`, one
page) is by far the single largest source of Native AOT/trim warnings — ~228 of 414 (confirmed again by
`migrate-to-docker-dotnet-enhanced`'s own spike: the `Microsoft.AspNetCore.Mvc` bucket alone accounted for
199 of 382 warnings measured there). Microsoft documents Razor Pages/Razor Components as **not supporting
trimming or Native AOT** (<https://aka.ms/aspnet/trimming>) — there is no configuration fix, only a
replacement. [RazorSlices](https://github.com/DamianEdwards/RazorSlices) is a lightweight `.cshtml`-based
templating library built for Minimal APIs with official Native AOT/trimming support, confirmed real and
actively published on NuGet (`RazorSlices` 0.11.3). Because the Dashboard's actual surface is one page (5
source files total), this closes the largest remaining AOT-prep warning bucket without a rearchitecture —
completing all 3 items `investigate-aot-build` opened
([[fix-yamldotnet-aot-trim]], [[migrate-to-docker-dotnet-enhanced]], this one).

## What Changes

- Replace `AddRazorPages()`/`MapRazorPages()` with a minimal-API GET endpoint returning a `RazorSlice`
  (`Results.Extensions.RazorSlice<TSlice, TModel>(model)`), confirmed via `dotnet-inspect` against the real
  package: `RazorSlice<TModel>` implements `IResult` directly, compiled at build time (no runtime Razor
  compilation, genuinely trim/AOT-safe).
- Convert `Pages/Dashboard/Index.cshtml` (razor markup, unchanged rendering logic) + `Index.cshtml.cs`'s
  `IndexModel` (currently a `PageModel` with constructor-injected services, an `OnGet` handler, and two
  `OnPost*` handlers) into: a plain `DashboardViewModel` record (the same properties `IndexModel` exposes
  today) built by the minimal-API GET handler from the same injected services
  (`IRouteConfigStore`/`ICertificateInventory`/`IDiscoveryHealth`/`ICertificateConverter`/`AdminApiOptions`),
  and two minimal-API POST endpoints replacing `OnPostConvert`/`OnPostReencrypt`.
- **BREAKING** (internal only, no observable change): the page's only Tag Helper usage
  (`asp-page-handler`/`asp-route-host` on the two `<form>` elements, `Microsoft.AspNetCore.Mvc.TagHelpers`)
  is removed — RazorSlices does not support Tag Helpers. Forms post to plain, explicit URLs instead
  (`/dashboard/certs/{host}/convert`, `/dashboard/certs/{host}/reencrypt`). Razor Pages' automatic
  anti-forgery token injection/validation on `<form method="post">` is replaced with an explicit
  `IAntiforgery` token embedded in the rendered form and validated in the POST handlers — same protection,
  wired manually instead of by convention.
- `DashboardServiceCollectionExtensions`/`DashboardEndpointMapping` keep their existing shape (register/map
  under the same `AdminApiOptions.Surface`/`Host` gating) — only their internals change from
  `AddRazorPages`/`MapRazorPages` to RazorSlices' minimal-API registration.
- No change to what the dashboard displays, its URL (`/dashboard`), its host-scoping behavior, its
  certificate-download routes (already minimal-API, untouched), or the two POST actions' actual effects.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — pure implementation swap, zero observable behavior change; `skip_specs: true` set in
`.openspec.yaml`)

## Impact

- **Code**: `src/DockYarp.Dashboard/` in full — `DockYarp.Dashboard.csproj`,
  `DashboardServiceCollectionExtensions.cs`, `DashboardEndpointMapping.cs`,
  `Pages/Dashboard/Index.cshtml(.cs)` → replaced with a RazorSlices template + a plain view-model +
  minimal-API endpoint handlers, `Pages/_ViewImports.cshtml` removed (RazorSlices doesn't use the Razor
  Pages view-imports model).
- **Dependencies** (`Directory.Packages.props`, `DockYarp.Dashboard.csproj`): add `RazorSlices` (CPM);
  `AddRazorSupportForMvc` removed from the csproj once `AddRazorPages`/`MapRazorPages` are gone.
- **Tests**: `DockYarp.IntegrationTests` (dashboard rendering, POST actions, anti-forgery) — verified
  end-to-end via `Microsoft.AspNetCore.Mvc.Testing`, same assertions on rendered content and redirect
  behavior, updated only where the request shape changes (explicit POST URLs, explicit anti-forgery token).
- **Manual verification**: per this session's own standing convention for UI changes, the dev server is
  started and the dashboard exercised in a real browser (golden path: view routes/clusters/certificates;
  edge cases: empty state, certificate download links, PFX-to-PEM conversion, key re-encryption) before this
  change is considered done — automated tests alone don't prove the rendered page looks right.
- **AOT**: removes the largest remaining warning source (~199-228/382-414 depending on measurement). Closes
  all 3 AOT-prep items opened by `investigate-aot-build`; Native AOT adoption itself remains a separate,
  still-open future decision (a fresh full spike, now unblocked on this machine per
  `vs-cpp-toolchain-native-aot`, is the natural next step whenever that decision is revisited — not part of
  this change's scope).
