## Context

See `proposal.md` for motivation. DockYarp's own production Docker API surface goes through two files:
`src/DockYarp.Docker/Discovery/DockerContainerSource.cs` (client construction, container listing/inspection,
event monitoring, response mapping) and `DockerTlsCredentials.cs` (remote-daemon TLS client-certificate
wiring, exercised only when `Docker:CertPath` is configured). **Correction found live during apply**: a
third file, `tests/DockYarp.E2E.Tests/NonDcpHarness.cs` (test infra, not production code — creates
containers/networks outside Aspire/DCP's management for host-network/multi-network e2e scenarios), also
references `Docker.DotNet` directly with its own `new DockerClientConfiguration().CreateClient()` and a
wider API surface (`Images`, `Networks` operations in addition to `Containers`) — the original backlog
item's "entire Docker API surface goes through one file" undercounted this. No new design decision needed:
same client-construction swap as `DockerContainerSource`, no TLS/X509 involved (local daemon only), and the
`Images`/`Networks` operation shapes get the same "confirm via build, not assumption" treatment tasks.md
already applies to the `Containers` shapes. Every design decision below was verified
against the real `Docker.DotNet.Enhanced` 4.3.3 package metadata via `dotnet-inspect` (constructors,
signatures, decompiled source where relevant) — not assumed from the fork's README/changelog alone, per the
backlog item's own explicit caution that this item carries the most real breaking-change risk of the
AOT-prep trio.

## Goals / Non-Goals

**Goals:**
- Replace `Docker.DotNet` with `Docker.DotNet.Enhanced`, preserving identical discovery behavior (same
  containers found, same addresses resolved, same events observed).
- Confirm every actual API call `DockerContainerSource`/`DockerTlsCredentials` make still compiles and
  behaves the same against the fork, fixing only what the fork genuinely renamed or restructured.

**Non-Goals:**
- Adopting any new capability the fork offers beyond what DockYarp already uses (e.g., its `WithContext`/
  Docker CLI-context support, Swarm/plugin operations) — out of scope, no current need.
- Enabling Native AOT publish itself — this change only removes one of the warning sources; AOT adoption is
  a separate future decision (see `investigate-aot-build`'s archived design.md).

## Decisions

- **Client construction: `DockerClientBuilder().WithEndpoint(uri).Build()`.** Confirmed via the builder's
  decompiled `Build()` source that `ResolveTransportFactory(scheme)` auto-selects a transport by URI scheme
  (`npipe`→NPipe, `unix`→Unix, `tcp`/`http`/`https`→LegacyHttp or NativeHttp per a `NativeHttpEnabled`
  toggle) when `WithTransportOptions` is never called — so the common case (no TLS, no proxy networks
  override) needs no transport configuration at all, matching today's zero-config `DockerClientConfiguration()`
  path.
- **HTTP transport: NativeHttp, not LegacyHttp, for the `tcp://` (remote daemon) case.**
  `NativeHttpTransportOptions.ConfigureHandler` is `Action<SocketsHttpHandler>` — a real BCL type with
  `SslOptions.ClientCertificates`/`RemoteCertificateValidationCallback` — versus
  `LegacyHttpTransportOptions.ConfigureHandler`, which is `Action<ManagedHandler>`, the fork's own
  bespoke handler type (a straight carry-over from the original `Microsoft.Net.Http.Client.ManagedHandler`
  DockYarp's current code depends on). NativeHttp matches AGENTS.md's own preference for real BCL types over
  a project-specific handler, and drops the dependency on `Docker.DotNet.Enhanced.LegacyHttp` entirely — only
  `.NativeHttp`, `.Unix`, `.NPipe` (for socket/named-pipe endpoints) and `.X509` (TLS credentials, below) are
  referenced.
- **TLS client-certificate wiring: `Docker.DotNet.X509.CertificateCredentials`, not a hand-rolled `Credentials`
  subclass.** The fork removed the old `Credentials` abstract class DockYarp's `ClientCertificateCredentials`
  subclassed (`DockerTlsCredentials.cs`'s current `GetHandler(HttpMessageHandler innerHandler)` override,
  cast to `ManagedHandler`). Its replacement, `Docker.DotNet.Enhanced.X509`'s `CertificateCredentials`,
  implements `IAuthProvider` (constructor: `CertificateCredentials(X509Certificate2? certificate)`, plus a
  settable `ServerCertificateValidationCallback` property) and plugs into
  `DockerClientBuilder.WithAuthProvider(IAuthProvider?)`. DockYarp keeps its own PEM-string-based
  `LoadClientCertificate`/`ChainsToAuthority` logic unchanged (already unit-tested, no filesystem
  dependency, and the fork's own `DockerTlsCertificates` convenience helpers only load from file paths —
  a worse fit than what DockYarp already has) — only the final "hand the built `X509Certificate2` and
  validation callback to the client" step changes, from a custom `Credentials.GetHandler` override to
  `new CertificateCredentials(clientCertificate) { ServerCertificateValidationCallback = ... }` passed to
  `WithAuthProvider`.
- **Model surface: confirmed stable except one rename.** `IContainerOperations.ListContainersAsync`/
  `InspectContainerAsync`, `ISystemOperations.MonitorEventsAsync(ContainerEventsParameters, IProgress<Message>,
  CancellationToken)`, `ContainersListParameters.Filters` (still
  `IDictionary<string, IDictionary<string, bool>>?`), `DockerApiException`, and `IDockerClient`/
  `DockerClient.Containers`/`.System` are all identical in shape to what DockYarp already calls — confirmed
  member-by-member, not inferred. The one real rename found: `ContainerListResponse.Ports` is now
  `IList<PortSummary>` (was `IList<Port>`); `PortSummary.PrivatePort` is `ushort` (was a wider integer type),
  which widens implicitly into `DockerContainerSource.ResolvePorts`'s existing `HashSet<int>` with no logic
  change — just the type name in the method signature. `ContainerInspectResponse.Config`/`.NetworkSettings`
  and `ContainerListResponse.NetworkSettings`/`.Labels`/`.Names`/`.ID`/`.Status` all keep their existing
  names and shapes.

## Risks / Trade-offs

- [Risk] The full `Docker.DotNet.Models.*` surface was spot-checked against exactly what
  `DockerContainerSource.cs`/`DockerTlsCredentials.cs` read today, not exhaustively diffed against every
  type in the package. → Mitigation: `tasks.md` keeps an explicit audit task before touching code, and the
  existing unit/integration/e2e test suite (which exercises real response mapping, not just compilation) is
  the actual acceptance gate — a missed rename fails loudly (compile error) or is caught by the discovery
  tests, not silently.
- [Trade-off] Adds 4 package references (`Docker.DotNet.Enhanced.{NativeHttp,Unix,NPipe,X509}`) where 1
  sufficed before, since the fork split transports into separate packages. Accepted — this is the fork's own
  documented shape (confirmed via NuGet search, not a DockYarp-side complication), and each package is
  small/focused, matching CPM's existing multi-package patterns elsewhere in the solution (e.g. the gRPC
  fixture packages).
- [Risk] `Docker.DotNet.Enhanced` is a smaller community fork, not an official Microsoft/dotnet-foundation
  package (already flagged in the backlog item). → Mitigation: activity confirmed live at propose time
  (commits through 2026-08-17, releases through 2026-06-28); no different from DockYarp's existing reliance
  on `YamlDotNet`/`Certes`/`Portable.BouncyCastle`, all third-party.

## Migration Plan

Single-PR swap, no phased rollout needed (internal dependency, no external contract changes): update
packages → rewrite the two files → fix compile errors from the model surface audit → run the full test
suite (unit/integration/e2e) → throwaway `PublishAot=true` spike to confirm the warning source is gone.
Rollback is a plain revert (no data/schema migration, no persisted state depends on the client type).
