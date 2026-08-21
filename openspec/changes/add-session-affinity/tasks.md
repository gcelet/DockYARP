## 1. Core model (AG-RP)

- [x] 1.1 `src/DockYarp.Core/Models/SessionAffinityPolicy.cs` (new file, MA0048): enum `None` (default),
      `ClientIpHash`, `Cookie`, `CustomHeader`.
- [x] 1.2 `src/DockYarp.Core/Models/Cluster.cs`: new `SessionAffinityPolicy` property (default `None`),
      XML-documented — `ClientIpHash` needs no Data Protection; `Cookie`/`CustomHeader` do and degrade
      gracefully when it's absent (see design.md). Verified: `dotnet build src/DockYarp.Core` — 0 warnings/errors.

## 2. Docker label parsing (AG-DD)

- [x] 2.1 `src/DockYarp.Docker/Labels/DockerLabels.cs`: new `SessionAffinity = "DOCKYARP_AFFINITY"` constant,
      documented with its 3 recognized values (`ip-hash`/`true`, `cookie`, `custom-header`) — separate from
      `LoadBalancing` (orthogonal `ClusterConfig` property, see design.md).
- [x] 2.2 `src/DockYarp.Docker/Labels/ContainerLabelConfig.cs`: new `SessionAffinityPolicy` (`SessionAffinityPolicy?`,
      nullable so "unset" is distinguishable from `None` at the label layer, mirroring `LoadBalancingPolicy?`).
- [x] 2.3 `src/DockYarp.Docker/Labels/LabelParser.cs`: new `ParseAffinityPolicy(string? value)` (`"TRUE"`/
      `"IP-HASH"`/`"IPHASH"` → `ClientIpHash`, `"COOKIE"` → `Cookie`, `"CUSTOM-HEADER"`/`"CUSTOMHEADER"` →
      `CustomHeader`, `"FALSE"`/empty → `None`, unrecognized → `null`) and `ResolveAffinity(labels)` — native
      `DOCKYARP_AFFINITY` (via `ParseAffinityPolicy`) wins; if absent, fall back to translating the *existing*
      `DockerLabels.NginxLoadBalance` label's directive via a new `TranslateNginxAffinity` (same trim/
      first-token shape as `TranslateNginxLoadBalance`, not sharing code with it since the two switches map to
      different enums) — `ClientIpHash` when the directive is `ip_hash` or `hash` (with or without arguments),
      otherwise `null`/unset (no compat value for `cookie`/`custom-header` — nginx has none). Wired into both
      `TryParse`/`ParseCommon` call sites, mirroring `LoadBalancingPolicy = ResolveLoadBalancing(labels)`.
- [x] 2.4 New `HasUnsupportedAffinity(labels)` (mirroring `HasUnsupportedLoadBalancing`): true when
      `DOCKYARP_AFFINITY` is present but unrecognized. Wired into `AddCommonWarnings` in `ContainerMapper.cs`
      with a warning matching the existing style (`"unrecognized DOCKYARP_AFFINITY; affinity not applied."`).
- [x] 2.5 `dotnet build src/DockYarp.Docker` — 0 warnings/errors.

## 3. Static configuration (AG-DEP)

- [x] 3.1 `src/DockYarp.App/StaticConfig/StaticConfigFile.cs`: new `Affinity` field on `ClusterEntry` (`string?`,
      mirroring `LoadBalancing`'s placement and type).
- [x] 3.2 `src/DockYarp.App/StaticConfig/StaticConfigProvider.cs`: `Map` sets `SessionAffinityPolicy` via a new
      local `ParseAffinityPolicy` — `LabelParser`'s own parser is `private`, and `StaticConfigProvider` already
      has its own independent (simpler) `ParsePolicy` for load-balancing rather than reusing `LabelParser`'s, so
      this follows the same pre-existing precedent rather than introducing internals-visibility just for this.

## 4. Container mapping (AG-DD)

- [x] 4.1 `src/DockYarp.Docker/Mapping/ContainerMapper.cs`: both `BuildCluster` overloads set
      `SessionAffinityPolicy = first.SessionAffinityPolicy ?? SessionAffinityPolicy.None` (single-port) /
      `common.SessionAffinityPolicy ?? SessionAffinityPolicy.None` (multiport), mirroring the existing
      `LoadBalancingPolicy` line.
- [x] 4.2 `dotnet build src/DockYarp.Docker` — 0 warnings/errors (full-solution build deferred to after task 6,
      once the App-layer changes exist too — building Docker alone already confirms this section).

## 5. Custom client-IP-hash policy (AG-RP)

- [x] 5.1 New `src/DockYarp.App/ReverseProxy/ClientIpHashSessionAffinityPolicy.cs`: implements
      `ISessionAffinityPolicy` (`Yarp.ReverseProxy.SessionAffinity`). **Correction found live via
      `dotnet-inspect`**: this task originally assumed the *Async methods (`FindAffinitizedDestinationsAsync`/
      `AffinitizeResponseAsync`) were the ones to override — verified via `dotnet dnx dotnet-inspect member
      ISessionAffinityPolicy` that it's the reverse: the **sync** methods (`FindAffinitizedDestinations`,
      `AffinitizeResponse`) are `abstract` (mandatory to implement), the async ones are `virtual` with a default
      body that calls the sync ones. Implemented the sync methods instead. `Name` returns
      `PolicyName = "ClientIpHash"`. `FindAffinitizedDestinations` hashes `context.Connection.RemoteIpAddress`
      via a small FNV-1a hash (not `IPAddress.GetHashCode()` — not documented as stable for this purpose) —
      first 3 octets for IPv4 (matching nginx's own `ip_hash` grouping), full address for IPv6 — then picks the
      healthy destination at `hash % destinations.Count` deterministically (destinations sorted by
      `DestinationId` first for stable ordering across calls). Returns `AffinityStatus.AffinityKeyNotSet` when
      `RemoteIpAddress` is null or no destinations exist. `AffinitizeResponse` is a no-op. Verified:
      `dotnet build src/DockYarp.App` — 0 warnings/errors.

## 6. YARP cluster mapping + Data Protection gating (AG-RP)

- [x] 6.1 `src/DockYarp.App/ReverseProxy/YarpConfigMapper.cs`: `BuildCluster` sets `ClusterConfig.SessionAffinity`
      per `Cluster.SessionAffinityPolicy` via a new `BuildSessionAffinity` helper — `ClientIpHash` →
      `SessionAffinityConfig { Enabled = true, Policy = ClientIpHashSessionAffinityPolicy.PolicyName,
      FailurePolicy = SessionAffinityConstants.FailurePolicies.Redistribute, AffinityKeyName = "dockyarp-affinity"
      /* unused by this policy */ }`; `Cookie`/`CustomHeader` → the matching `SessionAffinityConstants.Policies.*`
      name + the same failure policy + the same `AffinityKeyName` (a real, meaningful one for these two), but
      only when Data Protection is configured — otherwise `null` (no affinity) plus a diagnostic string appended
      to the returned diagnostics list. Verified the exact `SessionAffinityConstants.Policies`/`FailurePolicies`/
      `DestinationState.DestinationId` member names via `dotnet dnx dotnet-inspect -y -- member ... --package
      Yarp.ReverseProxy@2.3.0` before using them.
- [x] 6.2 `Map`'s return shape gains the diagnostics list — **not** a 3-element tuple as originally planned:
      hit `AV1561` (tuples capped at 2 elements) live, fixed with a new `YarpConfigMapResult` record
      (`Routes`/`Clusters`/`Diagnostics`, property-initialized). Also hit `AV1564` (bare `bool` parameter) on the
      originally-planned `bool dataProtectionConfigured` parameter — fixed by having `Map` take
      `DataProtectionOptions dataProtection` directly instead (a named, self-documenting type doubling as the
      single source of truth `YarpConfigBridge` also reads) and computing the bool internally.
- [x] 6.3 `src/DockYarp.App/ReverseProxy/YarpConfigBridge.cs`: constructor takes `DataProtectionOptions` +
      `ILogger<YarpConfigBridge>`; passes `dataProtection` straight into `YarpConfigMapper.Map` each `Publish()`
      (no local bool cache needed now that `Map` itself takes the options object); logs each returned diagnostic
      via `LogError`. `DataProtectionOptions` is registered in DI **inside** `AddDockYarpDataProtection`
      (`DataProtectionSetup.cs`), not as a separate `Program.cs` statement — keeps `Program.cs`'s top-level
      statement count unchanged (a separate local-var-then-`AddSingleton` approach tripped `AV1500`, the
      40-statement cap, live).
- [x] 6.4 `services.AddSingleton<ISessionAffinityPolicy, ClientIpHashSessionAffinityPolicy>()` registered
      alongside `AddReverseProxy()` in `ReverseProxyServiceCollectionExtensions.cs`. Confirmed via
      `dotnet-inspect find "*SessionAffinity*"` that YARP has no dedicated `AddSessionAffinityPolicy`-style
      registration extension — plain DI registration is the correct (and only) surface, matching
      `ILoadBalancingPolicy`'s own documented extensibility pattern.
- [x] 6.5 `dotnet build DockYarp.slnx` — 0 warnings/errors (also required updating ~16 pre-existing
      `YarpConfigMapper.Map(...)` call sites in `YarpConfigMapperTests.cs` from tuple-deconstruction to
      `.Routes`/`.Clusters` property access, and `YarpConfigBridgeTests.cs`'s constructor call to pass the two
      new parameters — a full-solution build, not just `src/DockYarp.App`, is what surfaced these).

## 7. Data Protection comment correction (AG-SEC)

- [x] 7.1 `src/DockYarp.Security/DataProtectionSetup.cs`: corrected the doc comment (was lines ~26-31) that
      previously pointed at this backlog item as "a future Data-Protection-consuming feature [that] must
      instead require the certificate and fail fast." Now accurately describes session affinity as a
      *conditional* DP consumer — `ClientIpHash` needs nothing; `Cookie`/`CustomHeader` need DP but degrade
      gracefully (Error-logged, affinity dropped) rather than hard-failing, since the setting is a
      dynamically-discovered per-container opt-in, not a statically-known-at-startup precondition like
      `AdminApi:Surface`/`Host`. Comment-only change (the method itself also now registers `DataProtectionOptions`
      in DI, done as part of task 6.3 — a related but separate code change in the same file).

## 8. New backlog item for deferred policies (AG-RP)

- [x] 8.1 `openspec/backlog/items/add-session-affinity-unencrypted-cookie.md`: stub created for YARP's built-in
      `HashCookie`/`ArrCookie` policies — cookie-based, unencrypted, no Data Protection dependency —
      deliberately deferred out of this change's scope at the user's explicit request to keep them tracked for
      a future version if a real need surfaces. References this change's design.md for context.

## 9. Unit + integration tests (AG-RP)

- [x] 9.1 `tests/DockYarp.Docker.Tests/LabelParserTests.cs`: `SessionAffinityPolicyIsParsed` (6 `TestCase`s — all
      native values incl. case-insensitivity), `UnknownAffinityIsFlagged`, `NativeAffinityTakesPrecedenceOverNginxCompat`,
      `NginxLoadBalanceAliasMapsToAffinity` (both `ip_hash;`/`hash $remote_addr consistent;` → `ClientIpHash`,
      `round_robin;` → unset — no affinity meaning).
- [x] 9.2 `tests/DockYarp.Docker.Tests/ContainerMapperTests.cs`: `SessionAffinityPolicyIsCarriedOntoTheCluster`,
      `SessionAffinityDefaultsToNone`, `UnknownAffinityIsWarned`, and
      `SessionAffinityPolicyIsCarriedOntoMultiportCluster` (covering the *other* `BuildCluster` overload too).
- [x] 9.3 New `tests/DockYarp.IntegrationTests/ClientIpHashSessionAffinityPolicyTests.cs` (8 tests): same-client-IP
      determinism, same-/24-subnet IPv4 clients hash to the same destination, different subnets can hash to
      different destinations (empirically verified, not assumed), IPv6 hashes on the full address, a
      destination-list change redistributes without throwing, no remote IP / no destinations both yield
      `AffinityKeyNotSet` rather than throwing, `AffinitizeResponse` is confirmed to mutate nothing.
- [x] 9.4 `tests/DockYarp.IntegrationTests/YarpConfigMapperTests.cs` (5 new tests): `ClientIpHashAppliesWithoutDataProtection`,
      `CookieAffinityAppliesWhenDataProtectionConfigured`, `EncryptedAffinityDegradesWithoutDataProtection`
      (parameterized over `Cookie`/`CustomHeader`), `NoAffinityIsUnaffectedByDataProtection`,
      `DegradedClusterDoesNotAffectSiblingCluster` (proving isolation, not a global effect).
- [x] 9.5 `dotnet test DockYarp.slnx` (unit + integration; e2e excluded, per this change's own task 10 decision
      below) — 456/456 passing, no regressions (41 Core + 149 Docker + 50 Security + 121 Integration + 95 Tls).

## 10. End-to-end validation (AG-RP)

- [x] 10.1 Checked `docs/testing.md`: confirmed `add-loadbalance-policies` has no e2e coverage (no row, not even
      in the "not covered" list). Flagged the distinction to the user rather than assuming the same applies
      here: unlike load-balancing (100% built-in YARP policies, nothing new to wire), this change registers a
      **custom** `ISessionAffinityPolicy` in DI for the first time — the existing unit tests
      (`ClientIpHashSessionAffinityPolicyTests`) call the policy directly and cannot prove YARP's
      `SessionAffinityMiddleware` actually invokes it, that DI registration is correct, or that a real client's
      TCP-level IP flows through end to end — structurally the same class of gap `ProxyProtocolTests` exists to
      close. User confirmed: add the e2e.
- [x] 10.2 `tests/DockYarp.E2E.AppHost/BackendCatalog.cs`: two new backends (`echo-affinity-1`/`echo-affinity-2`),
      same `VIRTUAL_HOST=affinity.local`, distinct `BACKEND_ID`, both labeled `DOCKYARP_AFFINITY=ip-hash`.
      `tests/DockYarp.E2E.Tests/RoutingTests.cs`: new `Affinity_StickyClientRoutesToSameBackend` — 10 requests
      from the one test client, all must return the same backend `id`. `docs/testing.md`'s E2E coverage map
      updated with the new row, per its own "update in the same change" instruction. **Corrected an unverified
      claim made live**: initially asserted `./build.ps1 E2E` is known-broken on this machine (BEAST2022) based
      on a stale 2026-08-03 memory, without re-checking it — the user pushed back, asking for the source. Ran
      `./build.ps1 E2E` for real: it now passes cleanly (see task 12.2) — the memory was outdated, not current
      fact. Lesson: don't assert a memory's claim as present-tense fact without verifying, especially when it's
      about to gate a real decision (deferring e2e execution).

## 11. Docs (AG-DOC)

- [x] 11.1 `docs-site/content/en/docs/configuration.md`: new `DOCKYARP_AFFINITY` label row, alongside
      `DOCKYARP_LB` — all 3 values, the client-IP-hash mechanism (first 3 octets IPv4) and that it needs no
      Data Protection, and that `cookie`/`custom-header` require `DataProtection:CertificatePath` and degrade
      gracefully (Error-logged) when it's absent. Also updated the nginx-proxy compat-label table's
      `loadbalance` row to note the `ip_hash`/`hash` → `DOCKYARP_AFFINITY=ip-hash` translation.
- [x] 11.2 `docs-site/content/en/docs/migrating-from-nginx-proxy.md`: grepped for `loadbalance`/`DOCKYARP_LB`/
      `ip_hash` — no matches, this guide doesn't currently discuss the `loadbalance` label at all, so there is
      nothing to update (confirmed, not assumed).

## 12. Final validation (AG-RP)

- [x] 12.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 12.2 `dotnet test DockYarp.slnx` — 456/456 unit+integration passing, no regressions (41 Core + 149 Docker +
      50 Security + 121 Integration + 95 Tls). **Plus a real `./build.ps1 E2E` run** (Docker build, Aspire
      AppHost, full container stack) — 33/33 e2e passing, confirming `Affinity_StickyClientRoutesToSameBackend`
      (task 10.2) genuinely passes end-to-end, not just compiles. Grand total: 489/489.
- [x] 12.3 Manual smoke test, run for real against the actual `dotnet run` app (not simulated) — used a scratch
      `Tls:CertificateDirectory` under the session scratchpad per [[smoke-test-scratch-dirs]], and
      `StaticConfig:Path` (no Docker needed for this — `Docker:Enabled` defaults to `false`) pointing at two
      throwaway local HTTP listeners (`backend-1`/`backend-2` on 127.0.0.1:9101/9102) with two clusters:
      `affinity-cluster` (`Affinity: "ip-hash"`, both destinations) and `cookie-cluster` (`Affinity: "cookie"`,
      no `DataProtection:CertificatePath` configured). Confirmed for real: (1) at startup, exactly the expected
      `fail: DockYarp.App.ReverseProxy.YarpConfigBridge[0] Cluster 'cookie-cluster': DOCKYARP_AFFINITY=cookie
      requires DataProtection:CertificatePath; affinity not applied.` line was logged; (2) `cookie-cluster`'s
      route still served normally (`backend-1` returned) — degraded, not excluded; (3) 10 repeated `curl`
      requests (no cookie jar — confirms the "works from the first request, no client-side state" property) to
      `affinity-cluster` all returned the same backend (`backend-1`) — real end-to-end stickiness through the
      actual DI-registered `ClientIpHashSessionAffinityPolicy` and YARP's live middleware pipeline. Cleaned up
      afterward: app and both backend processes stopped, no temp `appsettings.Smoke.json` left in
      `src/DockYarp.App/` (confirmed via `git status`).
