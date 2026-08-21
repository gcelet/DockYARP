## Context

See `proposal.md` — Why, and three design corrections found live in conversation before any code was written
(kept here since they materially shaped the final shape, not silently absorbed):

1. **YARP's 4 built-in session-affinity policies are all cookie/header round-trip mechanisms**; none replicate
   nginx-proxy's stateless, first-request-effective `ip_hash`. A custom `ISessionAffinityPolicy` is needed for
   genuine parity — verified against Microsoft's own YARP documentation, not assumed.
2. **A first proposed simplification — expose only YARP's built-in `Cookie` policy — was rejected by the
   user**, who clarified the actual goal is parity with nginx-**proxy** specifically, in the same containerized,
   edge-facing deployment position DockYarp itself occupies; an initial "unreliable behind NAT" counter-argument
   didn't hold since nginx-proxy ships `ip_hash` successfully in the identical context.
3. **The user then asked, separately, to also expose cookie/header-based affinity beyond nginx-proxy parity** —
   confirmed via web search (not assumed) that open-source nginx (which nginx-proxy is built on) has *no*
   cookie-based sticky-session mechanism at all — that's an NGINX Plus-only commercial feature — so exposing
   YARP's built-in `Cookie`/`CustomHeader` policies is a genuine DockYarp value-add beyond nginx-proxy's own
   ceiling, not a redundant reimplementation of something nginx-proxy already has. This reintroduced a Data
   Protection dependency, but only for these two policies, and only conditionally: `ip-hash` still needs
   nothing. The fail-fast shape for that conditional dependency was itself corrected once more, from an initial
   "exclude the whole route" answer to "degrade only the affinity, keep the route" — see Decisions.

Current code, read directly, not re-derived:
- `LabelParser.TranslateNginxLoadBalance` (`src/DockYarp.Docker/Labels/LabelParser.cs:404-417`) already has a
  `null =>` branch for `ip_hash`/`hash $x` with a comment pointing at this backlog item — the hook point for
  the `ip-hash` compat translation, not a fresh addition to that switch. It stays untranslated for `cookie`/
  `custom-header` — nginx has no equivalent value to translate.
- `LabelParser.ResolveLoadBalancing` (`:390-402`): native `DOCKYARP_LB` wins, the nginx `loadbalance` alias is
  the fallback — the precedence pattern `DOCKYARP_AFFINITY` resolution mirrors.
- `ContainerMapper.cs`'s `AddCommonWarnings` (`:147-195`) is the established idiom for per-container
  unsupported/invalid config: log a warning, fall back to a safe default, keep the route serving
  (`HasUnsupportedLoadBalancing`, `HasUnsupportedHttpsMethod`, etc. — never exclude the container for a
  "feature request degraded to a safe default" case; only genuine "cannot route at all" cases like an
  unparseable label set or no reachable address exclude the container). This precedent is why the DP-gating
  degradation (Decisions, below) downgrades only the affinity, not the whole route — excluding the route would
  have been inconsistent with every sibling case in this same file.
- `Cluster`/`ContainerLabelConfig` are per-container/per-cluster models built in `DockYarp.Docker`, which
  depends only on `DockYarp.Core` (never `DockYarp.Security`) — so the DP-configured check cannot happen inside
  `ContainerMapper` itself; it must happen downstream, in `DockYarp.App`, which already references both.
- `YarpConfigBridge.cs` (`src/DockYarp.App/ReverseProxy/YarpConfigBridge.cs`) is a constructor-injected
  `IHostedService` that calls `YarpConfigMapper.Map(store.Current, routing.DefaultHost)` once at startup and
  again on every `IRouteConfigStore.Changed` — the natural place to also inject `DataProtectionOptions` +
  `ILogger<YarpConfigBridge>`, since it already sits at the one point in `DockYarp.App` where the routing
  snapshot (built from both Docker discovery and `StaticConfig`, already merged) is turned into YARP config.
- `Program.cs:125` calls the parameterless `app.MapReverseProxy()`, which already includes
  `UseSessionAffinity()` in the default pipeline (confirmed via Microsoft's own YARP middleware doc) — no
  pipeline change needed, only a policy registration in DI.
- `DataProtectionSetup.cs`'s doc comment (`:26-31`) currently says "a future Data-Protection-consuming feature
  must instead require the certificate and fail fast — tracked on the add-loadbalance-policies backlog item
  [now add-session-affinity]." This change becomes a *conditional* DP consumer (2 of 3 policies), but not the
  hard-fail-at-startup consumer the comment anticipated — see Decisions for why that framing itself didn't
  survive contact with a dynamically-discovered, per-container opt-in setting.

## Goals / Non-Goals

**Goals:**
- Opt-in per-cluster session affinity, default-off, zero behavior change when unset.
- Genuine nginx-proxy `ip_hash` parity (the `ip-hash` policy): deterministic on client IP, no cookie, effective
  from the first request, first-3-octets-of-IPv4 grouping.
- Beyond-parity value: `cookie`/`custom-header` policies (YARP built-in), for operators who want a "real"
  session-identifier-based affinity DockYarp can offer even though nginx-proxy cannot.
- Reachable from both config sources (Docker labels — native + nginx-proxy compat for `ip-hash` only — and
  `StaticConfig` JSON), consistent with every other cluster-level setting in this project.
- A cluster requesting `cookie`/`custom-header` without Data Protection configured degrades gracefully (no
  affinity, route otherwise unaffected) rather than failing the whole route or the whole application, matching
  this project's established per-container degradation idiom — while still being loud about it (Error, not the
  usual Warning) since a security expectation went unmet.

**Non-Goals:**
- YARP's built-in `HashCookie` (its own default) and `ArrCookie` policies — cookie-based but unencrypted, no DP
  dependency. Deliberately deferred to keep this change's surface contained; tracked as its own backlog item
  (`add-session-affinity-unencrypted-cookie`) per the user's explicit request to not lose track of them, not
  silently dropped.
- A true host-startup fail-fast (crash before serving anything) for the DP requirement. Rejected: affinity is a
  per-container, dynamically-discovered opt-in setting — "at startup" cannot know about a container that
  requests `cookie` affinity for the first time ten minutes into runtime. The originating backlog stub's "fail
  fast at startup" framing assumed a statically-known precondition (like `AdminApi:Surface`/`Host`, both
  resolved once from config at boot); this setting is not statically knowable the same way, so that framing
  does not transfer as-is.
- Configurable hash granularity for `ip-hash` (e.g. full-IPv4 vs first-3-octets) — fixed to match nginx's own
  behavior; not operator-tunable, avoiding config-surface bloat for a `priority: low` item.
- Configurable affinity failure policy (`Redistribute` vs `Return503Error`) — fixed to `Redistribute` (the YARP
  default, closest to nginx-proxy's own graceful failover), not exposed.

## Decisions

**A custom `ISessionAffinityPolicy` (`ClientIpHashSessionAffinityPolicy`) for `ip-hash`, not one of YARP's
built-in policies.**

Rationale: covered in Context (correction #1/#2). `FindAffinitizedDestinationsAsync` computes the hash of
`HttpContext.Connection.RemoteIpAddress` (masked to the first 3 octets for IPv4, full address for IPv6)
directly against the current healthy destination list and returns the matching one — no stored/decoded key, so
no "key present but undecodable" failure mode. `AffinitizeResponseAsync` is a no-op: nothing to attach to the
response, since the client's own IP is always available on every subsequent request without a round-trip. This
also means the mechanism has an effect from the client's very first request — true parity with nginx's
`ip_hash`, which a cookie-based policy cannot offer (no effect until the client echoes a cookie back on its
*second* request).

**`cookie` and `custom-header` map directly to YARP's built-in `Cookie`/`CustomHeader` policies — no custom
implementation needed for these two.**

Rationale: unlike `ip-hash`, these two exist precisely to provide what DockYarp wants here (an encrypted,
session-identifier-style affinity) and are already correct off the shelf; reimplementing them would be pure
duplication with no parity or behavioral justification, unlike `ip-hash` where the built-ins genuinely don't
cover nginx-proxy's actual mechanism.

**DP-gating happens in `YarpConfigBridge`/`YarpConfigMapper` (`DockYarp.App`), not in `ContainerMapper`
(`DockYarp.Docker`) — an architectural constraint, not a style preference.**

Rationale: `DockYarp.Docker` depends only on `DockYarp.Core`, never `DockYarp.Security` (see AGENTS.md's
dependency graph) — `ContainerMapper` cannot see `DataProtectionOptions` even if it wanted to. `YarpConfigMapper.Map`
gains a `bool dataProtectionConfigured` parameter (computed once by `YarpConfigBridge` from
`DataProtectionOptions.CertificatePath is { Length: > 0 }`, passed in rather than re-read per call) and returns
an additional diagnostics list alongside routes/clusters; `BuildCluster` downgrades `Cookie`/`CustomHeader` to
no affinity when the flag is false and adds a diagnostic. `YarpConfigBridge.Publish()` logs each returned
diagnostic via `ILogger<YarpConfigBridge>.LogError`. This keeps `YarpConfigMapper` a pure, still-mostly-static
mapping layer (matching its existing shape) while placing the one piece of cross-cutting knowledge (is DP
configured) at the single call site that already has access to both routing and security config.

**Degradation, not exclusion, on missing Data Protection — corrected mid-design from an initial "exclude the
whole route" answer.**

Rationale: covered in Context — `ContainerMapper.cs`'s existing precedent for every sibling "unsupported
per-container value" case is graceful fallback, not exclusion; excluding a route over a missing *bonus*
encryption property (the proxy still functions correctly without affinity) would be inconsistent with that
precedent and a disproportionate blast radius for a nice-to-have. The security concern that motivated
"exclude" in the first draft — never silently ship an unprotected payload the operator explicitly asked to be
protected — is fully addressed by simply not applying the affinity at all (no cookie/header is ever set,
encrypted or not) rather than needing to take the whole route down; **Error** severity (not the codebase's
usual Warning for this class of fallback) keeps it appropriately loud without the disproportionate blast
radius.

**A fixed, unused `AffinityKeyName` is still set on `SessionAffinityConfig` for the `ip-hash` policy.**

Rationale: YARP's built-in policies require `AffinityKeyName` to be explicitly set (the cookie/header name);
our custom policy reads/writes neither, so the value is functionally inert for it — but if YARP's own config
validation enforces this property unconditionally (not confirmed either way from documentation alone), leaving
it null risks a runtime validation exception discovered only once the feature is exercised. Setting a fixed
constant (documented as unused) costs nothing and removes that risk; `cookie`/`custom-header` set a real,
meaningful `AffinityKeyName` as YARP expects.

**`DOCKYARP_AFFINITY` is a separate label from `DOCKYARP_LB`, not a new value of it.**

Rationale: `LoadBalancingPolicy` and session affinity are orthogonal YARP `ClusterConfig` properties — a
cluster can have both. Folding affinity into `DOCKYARP_LB` would misrepresent it as mutually exclusive with the
other five values, which it is not on the YARP side (unlike real nginx, where `ip_hash` genuinely replaces
round-robin as the sole directive).

## Risks / Trade-offs

- [Risk] Client IP as seen by DockYarp may not be the real client IP if DockYarp itself sits behind another
  proxy/load balancer. → Accepted, matches nginx-proxy's own same limitation in the same topology; DockYarp's
  documented deployment model is to *be* the edge proxy.
- [Risk] `AffinityKeyName` may turn out to be required-non-null by YARP's own config validation even for a
  custom policy that ignores it, discovered only when the feature is actually exercised. → Mitigation: set a
  fixed placeholder value proactively (see Decisions); the apply-phase integration test will surface whether
  this assumption holds.
- [Risk] A destination set change (container added/removed) shifts which destination a given IP hashes to for
  `ip-hash`, same as nginx's own `ip_hash` under a changing upstream pool. → Accepted, matches nginx-proxy's
  own behavior exactly; not solvable here, consistent hashing is beyond this `priority: low` item's scope.
- [Risk] An operator relying on `cookie`/`custom-header` affinity who later removes their DP certificate path
  gets silently-degraded (not broken) affinity, discoverable only via an Error log they may not be watching. →
  Accepted: matches the same trade-off already accepted for every other per-container config-degradation case
  in this codebase; the admin dashboard's existing health/status surfaces are the operator's tool for noticing
  this class of issue, not a new mechanism this change should invent.
