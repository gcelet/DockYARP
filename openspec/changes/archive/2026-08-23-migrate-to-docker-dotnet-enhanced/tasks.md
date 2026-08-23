## 1. Package swap (AG-DD, AG-DEP)

- [x] 1.1 Replace `Docker.DotNet` with `Docker.DotNet.Enhanced` + `.NativeHttp` + `.Unix` + `.NPipe` + `.X509`
      in `Directory.Packages.props` (CPM `PackageVersion` entries, all pinned to `4.3.3`) and
      `src/DockYarp.Docker/DockYarp.Docker.csproj` (`PackageReference`s, no `Version=` attribute), plus (see
      2b.1) `tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` (everything but `.X509`). Verified:
      `dotnet restore DockYarp.slnx` succeeds.

## 2. Client construction (AG-DD)

- [x] 2.1 Rewrote `DockerContainerSource.CreateClient` to `new DockerClientBuilder()` +
      conditional `.WithEndpoint(endpoint)` (only when `options.DockerEndpoint` is set) +
      conditional `.WithAuthProvider(tlsCredentials).WithTransportOptions(new NativeHttpTransportOptions())`
      (only when TLS credentials apply) + `.Build()`. **Extra correctness fix found during implementation**:
      `CertificateCredentials`/`IAuthProvider` owns no `IDisposable` (confirmed via `dotnet-inspect` —
      `DockerClient.Dispose()` only disposes the HTTP handler, never the auth provider or its wrapped
      `X509Certificate2`), unlike the old `Credentials` base class DockYarp's code used to subclass and
      dispose itself. Added a `DisposableCertificateCredentials : IAuthProvider, IDisposable` wrapper in
      `DockerTlsCredentials.cs` and a `credentials` field on `DockerContainerSource`, disposed alongside
      `client` in `Dispose()` — the client-certificate's private key handle would otherwise leak for the
      lifetime of the process on every `Docker:CertPath`-configured remote daemon. Verified:
      `dotnet build DockYarp.slnx` compiles clean (TreatWarningsAsErrors).
- [x] 2.2 Rewrote `DockerTlsCredentials.Create`/`BuildTlsCredentials` to return `IAuthProvider?`
      (concretely `DisposableCertificateCredentials`, wrapping `Docker.DotNet.X509.CertificateCredentials`)
      instead of the removed `Credentials` subclass: `LoadClientCertificate`/`ChainsToAuthority`/
      `BuildServerValidation` unchanged. Deleted the retired `ClientCertificateCredentials`/`ManagedHandler`
      casting code.
- [x] 2.3 Updated `DockerTlsCredentialsTests.cs` for the new `IAuthProvider`/`SocketsHttpHandler`-based
      shape (`Credentials`→`IAuthProvider`, `IsTlsCredentials()`→`TlsEnabled`, `GetHandler`→`ConfigureHandler`,
      `ManagedHandler`→`SocketsHttpHandler`, `.ClientCertificates`/`.ServerCertificateValidationCallback`
      moved under `.SslOptions`) — same inputs (PEM strings), same assertions. Verified:
      `dotnet test tests/DockYarp.Docker.Tests` — 151/151 green.

## 2b. Second consumer: e2e test harness (AG-DD)

- [x] 2b.1 **Scope correction found live during apply** (see design.md's Context): `tests/DockYarp.E2E.Tests/
      NonDcpHarness.cs` also referenced `Docker.DotNet` directly (`new DockerClientConfiguration().CreateClient()`,
      plus `Images`/`Networks` operations beyond `Containers`). Added the same package set to
      `tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj` (`Docker.DotNet.Enhanced` + `.NativeHttp`/`.Unix`/
      `.NPipe` — no `.X509`, this harness never uses TLS) and rewrote its client construction to
      `new DockerClientBuilder().Build()`. One more real fix needed here: `CreateContainerParameters.Env` is
      now non-nullable `IList<string>` (was nullable) — `envList` changed from `null` to `[]` for the
      no-env-vars case. Verified: `dotnet build DockYarp.slnx` compiles.

## 3. Model surface audit (AG-DD)

- [x] 3.1 Fixed the one confirmed rename: `DockerContainerSource.ResolvePorts(IList<Port>? ports)` →
      `IList<PortSummary>? ports` (`PortSummary.PrivatePort` is `ushort`, widened implicitly into the
      existing `HashSet<int>` — no logic change).
- [x] 3.2 Full-solution `dotnet build` (TreatWarningsAsErrors) surfaced every remaining break, not a manual
      changelog diff — 3 found beyond the Port rename, all fixed: (a) `Message.ID` was removed entirely
      (only `Actor.ID` remains, non-nullable) — `DockerContainerSource.TryMapEvent`'s
      `message.Actor?.ID ?? message.ID` simplified to `message.Actor.ID`; (b) a local variable named
      `credentials` shadowed the new `DockerContainerSource.credentials` field (MA0084) — renamed to
      `tlsCredentials` in `CreateClient`; (c) the `NonDcpHarness.cs` `Env` nullability fix from 2b.1.
      `ContainerListResponse`/`ContainerInspectResponse`/`ContainersListParameters`/`ContainerEventsParameters`/
      `DockerApiException`/`IDockerClient` all compiled unchanged, confirming the design.md's "spot-checked,
      not exhaustive" risk note didn't hide anything further.

## 4. Full validation (AG-DD, AG-DEP)

- [x] 4.1 `dotnet build DockYarp.slnx` + `dotnet test DockYarp.slnx --no-build` green — full solution clean,
      all 515 unit/integration tests pass unchanged in behavior (41/151/51/123/107/42 across the 6 test
      projects — the 42 in `DockYarp.E2E.Tests` at this point still ran against a **stale** `dockyarp:local`
      image built before this change, see 4.2's real result).
- [x] 4.2 `./build.ps1 E2E` green — **caught a real methodology trap live**: the first e2e run (via plain
      `dotnet test`, 14s) was a false positive per [[e2e-fast-iteration-stale-image]] — confirmed via
      `docker image inspect dockyarp:local --format '{{.Created}}'` (09:40 UTC) predating the source edits
      (~16:20-16:21 UTC same day), meaning it was still running the OLD `Docker.DotNet` 3.x discovery code
      inside the container, not this change's code at all. Reran via the real `./build.ps1 E2E` pipeline
      (which rebuilds the image): 42/42 green, full pipeline 1:41 total — within the established
      [[e2e-runtime-baseline]] healthy range, confirming real discovery/event-reconciliation/host-network/
      multi-network scenarios all work against `Docker.DotNet.Enhanced` for real, not against stale code.
- [x] 4.3 **Real environment blocker hit and resolved, not worked around**: the first attempts failed with
      `error : Platform linker not found` — confirmed via `vswhere.exe`/the VS instance's own `state.json`
      (`C:\ProgramData\Microsoft\VisualStudio\Packages\_Instances\...\state.json`, 68 selected packages, zero
      VC-related) that this machine's Visual Studio 2026 installation had genuinely never included the
      "Desktop development with C++" workload — not a regression from anything this session touched. User
      installed the component via VS Installer; two more real environment snags surfaced and were fixed
      before a clean run was possible: (a) the MSBuild-invoked link step calls `vswhere.exe` by bare name,
      not found on PATH in this shell — fixed by prepending the VS Installer directory to `PATH` for the
      publish command; (b) the very first post-install run reused stale incremental `obj/`/`bin` outputs from
      earlier failed attempts and silently reported **zero** warnings (a false negative, not a real result) —
      fixed by clearing `src/DockYarp.App`, `DockYarp.AdminApi`, `DockYarp.Dashboard`'s `obj`/`bin` before
      rerunning.
      **Real, complete result**: `dotnet publish src/DockYarp.App -r win-x64 -p:PublishAot=true
      -p:TrimmerSingleWarn=false -p:TreatWarningsAsErrors=false` succeeds (native `DockYarp.App.exe`
      produced), 382 total `warning IL*` lines. **Zero** trace back to `Docker.DotNet`. Newtonsoft.Json still
      accounts for ~136 warnings — traced via `dotnet nuget why DockYarp.App.csproj Newtonsoft.Json`
      (real command, real output, not assumed) to **`Certes`** (the ACME client library, `DockYarp.Tls` →
      `Certes` → `Newtonsoft.Json 13.0.2`), entirely unrelated to Docker.DotNet and out of this change's
      scope. Confirmed independently via `dotnet list src/DockYarp.Docker package --include-transitive`:
      zero `Newtonsoft.Json` under `DockYarp.Docker` specifically (was present pre-migration). **Correction
      to the original backlog item's estimate**: the "~138 warnings" once attributed to "Docker.DotNet's
      Newtonsoft.Json/reflection" was an overestimate that conflated Docker.DotNet's own share with Certes's
      separate, pre-existing Newtonsoft.Json dependency — the two packages both pulled in the same
      `Newtonsoft.Json` version, so trim-analyzer warnings from either looked identical without tracing the
      dependency graph explicitly, as done here. The 379→382 total is not a clean before/after (this session
      couldn't reproduce the exact conditions of the original 379 measurement — different machine state,
      possibly different incremental-build starting point), but the **acceptance criterion that actually
      matters is met**: no warning traces to `Docker.DotNet`'s own code or its own `Newtonsoft.Json`
      dependency, confirmed two independent ways.
