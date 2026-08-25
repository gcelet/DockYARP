---
id: investigate-certes-aot-alternative
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: low
nginx-proxy: n/a (internal finding — AOT/trim readiness, from migrate-dashboard-to-razorslices's own AOT spike)
provenance: 2026-08-23 migrate-dashboard-to-razorslices's real -p:PublishAot=true spike (post-migration measurement)
status: backlog
---

## Why

After all 3 AOT-prep items opened by `investigate-aot-build` landed, a real `-p:PublishAot=true` spike
measured 170 remaining warnings (down from 382). **136 of those — by far the largest remaining bucket — are
`Newtonsoft.Json.*`**, traced (via `dotnet nuget why src/DockYarp.App/DockYarp.App.csproj Newtonsoft.Json`,
confirmed live during `migrate-to-docker-dotnet-enhanced`, re-confirmed unchanged here) to a single source:
**`Certes`** (`DockYarp.Tls`'s ACME/Let's Encrypt client, `Certes 3.0.4` → `Newtonsoft.Json 13.0.2`
transitively). A further 5 warnings (`System.Linq.Expressions`/`Microsoft.CSharp.RuntimeBinder`, both BCL)
are downstream consequences of Newtonsoft's own use of `dynamic`/expression trees — not separate problems,
just more of the same root cause. Together this is ~141 of the 170 remaining warnings, i.e. the single
largest remaining blocker to a warning-free Native AOT publish, now that the Dashboard/Docker.DotNet/
YamlDotNet buckets are all closed.

**User's own initial assessment (2026-08-23, worth recording as a real prior, not just an open question):**
no maintained alternative ACME client library is known offhand — this may be a genuine, harder blocker than
the prior 3 AOT-prep items, which each had a known real fix (a fork, a different template library, a
hand-written static context). Do not assume a fix exists; this item starts as an honest investigation, not
a pre-committed migration.

## Assessment (2026-08-23) — NOT YET DONE, this is the open question this item exists to answer

Unlike the 3 completed AOT-prep items (each of which started with — or quickly found — a concrete,
maintained replacement), no such candidate has been identified yet for Certes. Real avenues to check before
concluding this is genuinely blocked:

1. **Has Certes itself added AOT/trim support, or dropped Newtonsoft.Json, in a release newer than 3.0.4?**
   Check its own changelog/repo activity — the same kind of "has the ecosystem already moved" check that
   corrected the original `investigate-aot-build` spike's own too-quick "Docker.DotNet is blocked" verdict
   (see that change's archived `design.md` — "Lesson worth repeating"). Do not assume 3.0.4 is still current
   without checking.
2. **Is there a genuinely maintained alternative ACME v2 client for .NET** (not merely one that exists, but
   one that's actively maintained and, ideally, already AOT-aware)? Candidates to actually check, not just
   name-drop: any fork analogous to `Docker.DotNet.Enhanced`'s relationship to `Docker.DotNet`; whether the
   .NET/ASP.NET Core team's own `Microsoft.AspNetCore.Certificate.*`/`LettuceEncrypt`-adjacent ecosystem has
   produced anything (note: `LettuceEncrypt` itself wraps Certes, so it would not remove the dependency,
   only hide it).
3. **Is a hand-rolled ACME v2 client realistic**, mirroring `add-acme-dns01`'s own precedent of hand-rolling
   RFC 2136 DNS UPDATE + TSIG when no suitable maintained package existed? ACME v2 (RFC 8555) is a
   real-but-bounded protocol (JWS-signed HTTP requests over a directory/order/challenge/finalize state
   machine) — scope and risk should be assessed for real, not assumed prohibitive, but this is a much larger
   surface than DNS UPDATE was, and Certes' own scope (full x509/JWS/HTTP orchestration) is non-trivial to
   replicate correctly. This is very likely the highest-effort of the three avenues.
4. **Is DockYarp.Tls's own usage of Certes narrow enough** that only specific Newtonsoft-touching code paths
   are exercised, such that a targeted `TrimmerRootAssembly`/manual annotation workaround could suppress the
   warnings without a real fix? (Lower confidence this is sound — Newtonsoft's own reflection-based model
   binding is exactly what trim/AOT warnings exist to catch; silencing without fixing risks a real runtime
   failure under trimming, not just a cosmetic warning. Only pursue if 1-3 all come back genuinely blocked.)

## Proposed change (sketch)

Not yet — this item's own propose step **is** the investigation (avenues 1-3 above), following the same
"verify before concluding blocked" discipline `investigate-aot-build` itself established after its first,
too-quick verdict was corrected. The actual change (migration, upgrade, or hand-rolled client) depends
entirely on what that investigation finds — do not pre-commit to an approach in `design.md` before the
research is done.

## Acceptance criteria (→ scenarios)

TBD — depends on which avenue (if any) the investigation confirms viable. If none pan out, the honest
outcome is: document the verdict (mirroring `investigate-aot-build`'s own `## Spike Result` pattern) and
close this item without a code change, same as how `investigate-aot-build` itself could have legitimately
landed if its user-supplied leads hadn't panned out.

## Notes / risks / references

- **This may be the item where Native AOT adoption genuinely stalls** — unlike the 3 completed items, no
  known fix exists yet. Budget accordingly; do not assume this closes as easily as the others did.
- Refs: `src/DockYarp.Tls/CertesAcmeClient.cs` (and everything else in `DockYarp.Tls` touching `Certes`),
  `migrate-to-docker-dotnet-enhanced`'s archived `tasks.md` (first traced Newtonsoft.Json → Certes there),
  `migrate-dashboard-to-razorslices`'s archived `tasks.md` (full warning breakdown, re-confirms the same
  136-line bucket unchanged), `investigate-aot-build`'s archived `design.md` ("Lesson worth repeating" — verify
  "no fix exists" as rigorously as "X is broken" before writing a closing verdict).
