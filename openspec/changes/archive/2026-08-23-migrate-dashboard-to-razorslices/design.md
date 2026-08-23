## Context

See `proposal.md` for motivation. The Dashboard's entire surface is 5 source files under
`src/DockYarp.Dashboard/`: `DashboardServiceCollectionExtensions.cs` (DI registration, gated on
`AdminApiOptions.Surface`), `DashboardEndpointMapping.cs` (route mapping, gated on the same option + host
scoping + the already-minimal-API certificate-download routes), `Pages/Dashboard/Index.cshtml` +
`Index.cshtml.cs` (the page itself: a GET that snapshots routing/certificate/health state, two POSTs that
mutate certificate storage), and `Pages/_ViewImports.cshtml`. Every RazorSlices API decision below was
verified against the real package (0.11.3) via `dotnet-inspect` before writing code, not assumed from
documentation — `RazorSlice<TModel>`/`RazorSlice` implement `IResult` directly and are returned via
`Microsoft.AspNetCore.Http.ResultsExtensions.RazorSlice(...)`, compiled at build time (a source generator,
like Razor Pages' own view compilation, but with an AOT/trim-safe runtime path — no `System.Reflection.Emit`
or `IViewCompiler` involved).

## Goals / Non-Goals

**Goals:**
- Replace Razor Pages with RazorSlices for the Dashboard's one page, with **zero observable behavior
  change**: same URL, same rendered content for the same state, same two POST actions with the same effect
  and the same anti-forgery protection.
- Confirm the `Microsoft.AspNetCore.Mvc`/Razor-Pages-attributed AOT warning bucket is gone.

**Non-Goals:**
- Redesigning the dashboard's look, layout, or information architecture — this is a rendering-mechanism
  swap, not a UI redesign.
- Adding authentication/authorization to the dashboard — out of scope (a separate, already-parked backlog
  item, `add-admin-dashboard-oidc-auth`).
- Migrating the certificate-download GET routes in `DashboardEndpointMapping.cs` — they are already
  minimal-API endpoints, untouched by Razor Pages, and need no change.

## Decisions

- **View model: a plain `DashboardViewModel` record, not a class hierarchy.** `IndexModel` today is a
  `PageModel` with constructor-injected services and public properties set in `OnGet`. RazorSlices templates
  don't support constructor DI the way `PageModel` does (a slice is a compiled template, not a DI-activated
  type) — the standard RazorSlices pattern is: the minimal-API handler receives services as normal minimal-API
  parameters, builds a plain model object, and passes it to the slice. `DashboardViewModel` carries exactly
  the properties `IndexModel` exposes today (`Routes`, `ClustersById`, `Certificates` — including the
  `CertificateRow` record unchanged — `AllowCertificateDownload`, `AllowCertificateConversion`,
  `AllowKeyReencryption`, `Status`, `DiscoveryStatus`, `DiscoveryBadgeClass`), computed by a
  `BuildViewModel(...)` helper that mirrors `OnGet`'s existing logic (including `ToCertificateRow`)
  unchanged.
- **Routing: 3 minimal-API endpoints replacing the one Razor Page.** `GET /dashboard` → renders the slice
  (was the page's `OnGet`). `POST /dashboard/certs/{host}/convert` → was `OnPostConvert` (Razor Pages'
  `asp-page-handler="Convert"` convention). `POST /dashboard/certs/{host}/reencrypt` → was `OnPostReencrypt`.
  Both POST routes live under the existing `/dashboard/certs/{host}/*` prefix, alongside the
  already-minimal-API download routes in the same file — consistent with the existing convention, not a new
  one.
- **Anti-forgery: manual, via `IAntiforgery`, not removed.** Razor Pages auto-injects a hidden
  `__RequestVerificationToken` field into any `<form method="post">` (via the Form tag helper) and
  auto-validates it on every page handler, converting a failed validation into `400 Bad Request`. RazorSlices
  has no such convention. Replaced with: the GET handler calls `IAntiforgery.GetAndStoreTokens(HttpContext)`
  and passes the token into the view model (one new `AntiforgeryToken` property, rendered as a hidden
  `<input>` in both forms); each POST handler calls **`IAntiforgery.IsRequestValidAsync(HttpContext)`**
  (non-throwing, returns `bool`) and returns `Results.BadRequest()` itself on failure — **not**
  `ValidateRequestAsync` (throws on failure) as originally planned here: confirmed via the existing
  `AdminObservabilityTests`' own `...WithoutAntiForgeryTokenIsRejected` tests (found live during apply, not
  anticipated in this design — asserting `400 Bad Request`) that an uncaught `ValidateRequestAsync` exception
  would have surfaced as an unhandled 500 instead, since no `app.UseAntiforgery()` middleware is registered
  to catch it (that middleware, not manual validation, is what does the exception-to-400 translation
  automatically). `builder.Services.AddAntiforgery()` is required (Razor Pages currently registers this
  transitively via `AddRazorPages()`; confirmed via grep this wasn't registered anywhere else in the
  solution).
- **Tag Helpers: removed from the app's own markup, but the framework's own built-in ones need explicit
  disabling — a real correction found during apply, not anticipated here originally.** The page's only
  Tag Helper usage of ours was `asp-page-handler`/`asp-route-host` on the two `<form>` tags — both become
  plain `<form method="post" action="/dashboard/certs/@(...)/convert">`. That alone was not sufficient:
  `AddRazorSupportForMvc=true` (kept, see below) bakes `HeadTagHelper`/`BodyTagHelper` (from
  `Microsoft.AspNetCore.Mvc.Razor.TagHelpers`, targeting the literal `<head>`/`<body>` tags) into the
  "mvc.1.0.view" Razor document configuration **unconditionally** — independent of any `@addTagHelper` in
  the project, and RazorSlices marks the methods these tag helpers need as `[Obsolete]`/uncompilable
  ("Tag Helpers are not supported in Razor Slices"). Confirmed by fetching RazorSlices' own official Getting
  Started guide from its GitHub repo (`gh api repos/DamianEdwards/RazorSlices/contents/README.md`) rather
  than guessing: it documents a required `Slices/_ViewImports.cshtml` with
  `@removeTagHelper *, Microsoft.AspNetCore.Mvc.Razor` (the assembly is named `Microsoft.AspNetCore.Mvc.Razor`,
  *not* `...Mvc.Razor.TagHelpers` — an initial wrong guess that silently no-opped, since `@removeTagHelper`
  only removes helpers matching an assembly it can find, and a wrong name matches nothing) plus a
  `@tagHelperPrefix __disable_tagHelpers__:` second-layer safeguard. `Slices/_ViewImports.cshtml` now carries
  this (adapted from the official example), and `Pages/_ViewImports.cshtml` is deleted along with the rest
  of `Pages/`.
- **Project file: `AddRazorSupportForMvc` is kept, not removed — the original plan here was wrong.**
  RazorSlices' own build target (`RazorSlices.targets`'s `_CheckRazorSlicesDeps`) hard-errors if
  `AddRazorSupportForMvc != true` — confirmed by the actual build error, not documentation. (A brief detour
  tried switching the SDK to `Microsoft.NET.Sdk.Web` to match RazorSlices' own sample project, since that
  sample sets no `AddRazorSupportForMvc` property at all — `Sdk.Web` apparently defaults it internally. That
  didn't fix the tag-helper issue either, and additionally broke the build with "no Main method" since
  `Sdk.Web` implies an executable output for what is a referenced class library here — reverted.) The real
  fix was the `_ViewImports.cshtml`-based tag-helper removal above, orthogonal to which SDK sets the
  property.

## Risks / Trade-offs

- [Risk] RazorSlices is a smaller, single-maintainer library compared to ASP.NET MVC/Razor Pages (already
  flagged in the backlog item). → Mitigation: confirmed actively published (0.11.3 on NuGet, real download
  count), and the Dashboard's usage is narrow enough (one page, no Tag Helpers, no partials, no view
  components) that switching away later — to a different template engine, or to hand-written HTML string
  building — would be a small, contained change if RazorSlices' maintenance ever lapses.
- [Risk] Manual anti-forgery wiring is easier to get subtly wrong than the Razor Pages convention (e.g.
  forgetting the check on a new POST handler added later, or — as happened here — picking the throwing
  `ValidateRequestAsync` API and getting a 500 instead of the expected 400). → Mitigation: both current POST
  handlers get an explicit, visible `if (!await antiforgery.IsRequestValidAsync(context)) return
  Results.BadRequest();` at the top, matching the existing code's own pattern of explicit `Allow*` checks "as
  defense in depth" — the same explicit-check style already used in this file, not a new idiom, and now
  verified against the real expected status code by the existing integration tests, not just assumed.
- [Trade-off] Loses Razor Pages' automatic model binding (`OnPostConvert(string host)` today binds `host`
  from the route automatically). Minimal API route parameters (`(string host, ...) => ...`) provide the same
  binding with no extra code — not a real cost.

## Migration Plan

Single-PR swap, no phased rollout (internal rendering-mechanism change, no external contract change beyond
the internal-only form-URL/Tag-Helper removal noted in `proposal.md`). Manual verification against the real
dev server (real HTTP requests through the actual production pipeline) is part of this change's own
acceptance, per this session's standing UI-change convention — automated tests alone don't prove the
rendered page is correct; no browser-automation tool was available in this session, so the interactive POST
flows are covered by the real `WebApplicationFactory<Program>`-based integration tests instead (see
`tasks.md` 4.2 for the honest scope of what was and wasn't literally clicked in a browser). Rollback is a
plain revert (no persisted state depends on the rendering mechanism).
