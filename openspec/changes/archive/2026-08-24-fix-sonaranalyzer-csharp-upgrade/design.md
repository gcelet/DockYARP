## Context

`Directory.Packages.props` pins `SonarAnalyzer.CSharp` at `10.29.0.143774`. See `proposal.md` for the two
diagnostic rules involved and why the version bump alone cannot land.

## Goals / Non-Goals

**Goals:**
- Land the version bump and every required fix in one change, verified by a real local build (not the
  inflated ~73-site estimate from a duplicated CI log — 18 unique sites, confirmed by building the actual
  Renovate branch in an isolated worktree).

**Non-Goals:**
- Not touching any other analyzer's ruleset or `.editorconfig` severities — only fixing what the version bump
  itself newly requires.
- Not suppressing anything via `#pragma`/`[SuppressMessage]` (`AGENTS.md` guardrail: fix the root cause).

## Decisions

**S8969 (null-forgiving operator) — remove case by case, not blanket.** Verified empirically (not assumed) on
the one production-code case with repeated forgiving on the same line,
`TlsDomains.cs:25` — `new DesiredCertificate(route.Tls!.CertificateHost, route.Tls!.ContactEmail,
route.Tls!.ChallengeType)`:
- Only the **2nd and 3rd** `route.Tls!` are flagged (columns 94, 119) — **not** the first one.
- A real local build with only the 2nd/3rd `!` removed (first kept) compiles clean, 0 warnings.
- This matches real Roslyn nullable-flow semantics: `x!` narrows the compiler's own null-state for `x` for
  the rest of that flow, so after the *first* forgiving access, `route.Tls` is genuinely known non-null for
  the rest of the same lambda body — the 2nd/3rd are truly redundant, the 1st is not (nothing narrows
  `route.Tls` before it, since the `Where` clause's pattern-matched `tls` local is scoped to that lambda only
  and not visible in the following `Select`).
- **Applies the same case-by-case verification discipline to every other S8969 site**: each removal must be
  followed by a project build (not assumed from the diagnostic alone) before moving to the next.

**S8949 (explicit CancellationToken) — thread the real ambient token, verified against the actual API**:
- `Http01ChallengeMiddleware.cs:36` — `context.Response.WriteAsync(keyAuthorization)` →
  `context.Response.WriteAsync(keyAuthorization, context.RequestAborted)`. `HttpResponse.WriteAsync` has a
  `(string, CancellationToken)` overload (ASP.NET Core `HttpResponseWritingExtensions`).
- `EchoerService.cs:31` — `responseStream.WriteAsync(new EchoReply {...})` →
  `responseStream.WriteAsync(new EchoReply {...}, context.CancellationToken)`. Confirmed via `dotnet-inspect`
  against the real `Grpc.Core.Api` package: `IAsyncStreamWriter<T>` has both `WriteAsync(T)` and
  `WriteAsync(T, CancellationToken)` — the second overload is real, not assumed from the diagnostic message.

**Alternative considered — bump the version in one commit, fix sites in a follow-up**: rejected. A partial
bump leaves the build red on `develop` until the follow-up lands; landing both together is a small, bounded
diff (18 sites) with no reason to split it.

## Risks / Trade-offs

- [Risk] A different S8969 site has a genuinely different flow shape (no earlier same-line narrowing to rely
  on) and removing its `!` introduces a real CS86xx nullable warning → Mitigation: verify each site
  individually with a project-scoped build before moving to the next, per the Decisions section above — do
  not batch-strip all 16 `!` occurrences and build once at the end.
- [Risk] Renovate PR #3 does not auto-close once this change lands at the same version → Mitigation: not a
  re-fix of this item — confirm after merge and note it as a follow-up observation if it happens.
