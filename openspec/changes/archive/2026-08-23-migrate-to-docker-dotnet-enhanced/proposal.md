## Why

`investigate-aot-build` found `Docker.DotNet` (the pinned discovery client, `3.125.15`) is the largest
remaining Native AOT/trim warning source (~138/414) via its `Newtonsoft.Json`/reflection-based model
binding, and the upstream repo has had no release or activity since 2023
([dotnet/Docker.DotNet#689](https://github.com/dotnet/Docker.DotNet/issues/689), unanswered). A follow-up
look (prompted by a user-supplied lead) found the real path forward:
**[`testcontainers/Docker.DotNet`](https://github.com/testcontainers/Docker.DotNet)**, published on NuGet
as `Docker.DotNet.Enhanced`, is an actively maintained fork (releases through `4.3.3`, commits as recent as
2026-08-17) that declares `IsAotCompatible=true` and has fully dropped `Newtonsoft.Json`. This turns the
last "wait for someone else" AOT blocker into ordinary migration work DockYarp can do on its own schedule.

## What Changes

- Replace the `Docker.DotNet` package reference with `Docker.DotNet.Enhanced` (plus the transport
  sub-packages DockYarp's endpoint support needs: `.NativeHttp`, `.Unix`, `.NPipe`, `.X509`) in
  `Directory.Packages.props` (CPM) and `src/DockYarp.Docker/DockYarp.Docker.csproj`. **BREAKING** (internal
  only): the fork is a major-version jump (3.x → 4.x) under a different package id, not a drop-in version
  bump.
- Rewrite `DockerContainerSource.CreateClient` from `DockerClientConfiguration`/`.CreateClient()` to
  `DockerClientBuilder`/`.WithEndpoint(...)`/`.Build()`.
- Rewrite `DockerTlsCredentials`'s final wiring step: the fork removes the old `Credentials` abstract class
  DockYarp's hand-rolled `ClientCertificateCredentials` subclassed (which hooked into the retired
  `ManagedHandler`/`Microsoft.Net.Http.Client` handler model). Replace it with the fork's own
  `Docker.DotNet.X509.CertificateCredentials` (`IAuthProvider`), wired via `DockerClientBuilder.WithAuthProvider(...)`.
  DockYarp's existing PEM-string-based certificate/CA-chain construction
  (`LoadClientCertificate`/`ChainsToAuthority`) is unaffected — only the final handler-wiring step changes.
- No change to `DockerContainerSource`'s actual Docker API calls: `IContainerOperations.ListContainersAsync`/
  `InspectContainerAsync`, `ISystemOperations.MonitorEventsAsync`, `ContainersListParameters.Filters`,
  `DockerApiException`, and the `IDockerClient`/`DockerClient.Containers`/`.System` shape are unchanged
  between the two packages (confirmed via `dotnet-inspect` against the real `Docker.DotNet.Enhanced` 4.3.3
  metadata, not assumed from the fork's changelog alone) — no behavior change expected.
- No change to `DockYarp.Docker.Models`-consuming code outside `DockerContainerSource.cs` and
  `DockerTlsCredentials.cs` — those two files are this migration's entire blast radius.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — pure implementation swap, zero observable behavior change; `skip_specs: true` set in
`.openspec.yaml`)

## Impact

- **Code**: `src/DockYarp.Docker/Discovery/DockerContainerSource.cs`,
  `src/DockYarp.Docker/Discovery/DockerTlsCredentials.cs`, and (found live during apply, test infra only)
  `tests/DockYarp.E2E.Tests/NonDcpHarness.cs`.
- **Dependencies** (`Directory.Packages.props`, `src/DockYarp.Docker/DockYarp.Docker.csproj`,
  `tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj`): `Docker.DotNet` → `Docker.DotNet.Enhanced` +
  `Docker.DotNet.Enhanced.{NativeHttp,Unix,NPipe,X509}` (the test project needs everything but `.X509`).
- **Tests**: existing `DockYarp.Docker.Tests` (`DockerTlsCredentialsTests` and friends) exercise the same
  PEM-string construction path; adjusted only where the final `Credentials`/`IAuthProvider` type differs.
  E2E discovery coverage (`docs/testing.md`'s coverage map) is the acceptance proof for the client itself
  against a real Docker daemon.
- **AOT**: removes the second-largest warning source found by `investigate-aot-build` (~138/414). Combined
  with the already-shipped `fix-yamldotnet-aot-trim` and the still-open `migrate-dashboard-to-razorslices`,
  this closes every warning source that spike classified as blocking — Native AOT adoption itself stays a
  separate future decision, not committed to by this change.
