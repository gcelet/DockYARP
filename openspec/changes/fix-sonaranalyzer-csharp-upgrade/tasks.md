## 1. Bump the analyzer group (AG-DEP)

- [x] 1.1 Bump `SonarAnalyzer.CSharp`, `Roslynator.Analyzers`, `Meziantou.Analyzer` in
      `Directory.Packages.props` to the versions Renovate PR #3 proposes, and verify `dotnet restore
      DockYarp.slnx` succeeds.

## 2. Fix the 2 production-code sites (AG-AT)

- [x] 2.1 `src/DockYarp.Tls/TlsDomains.cs:25` — remove only the 2nd and 3rd `route.Tls!` (keep the 1st), and
      verify `dotnet build src/DockYarp.Tls/DockYarp.Tls.csproj` succeeds with 0 warnings.
- [x] 2.2 `src/DockYarp.Tls/Http01ChallengeMiddleware.cs:36` — pass `context.RequestAborted` to
      `context.Response.WriteAsync(...)`, and verify `dotnet build src/DockYarp.Tls/DockYarp.Tls.csproj`
      succeeds with 0 warnings.

## 3. Fix the E2E test-support site (AG-DEP)

- [x] 3.1 `tests/DockYarp.E2E.GrpcBackend/EchoerService.cs:31` — pass `context.CancellationToken` to
      `responseStream.WriteAsync(...)`, and verify `dotnet build
      tests/DockYarp.E2E.GrpcBackend/DockYarp.E2E.GrpcBackend.csproj` succeeds with 0 warnings.

## 4. Fix the 14 test-file sites, one file at a time (AG-DEP)

- [x] 4.1 `tests/DockYarp.Core.Tests/ClusterModelTests.cs` (2 sites, lines 80 and 96) — remove the redundant
      `!`, and verify `dotnet build tests/DockYarp.Core.Tests/DockYarp.Core.Tests.csproj` succeeds with
      0 warnings.
- [x] 4.2 `tests/DockYarp.Docker.Tests/ContainerMapperTests.cs` (line 162) — same fix and verification, on
      `DockYarp.Docker.Tests.csproj`.
- [x] 4.3 `tests/DockYarp.Docker.Tests/DockerFiltersTests.cs` (line 33) — same fix and verification, on
      `DockYarp.Docker.Tests.csproj`.
- [x] 4.4 `tests/DockYarp.Docker.Tests/DockerTlsCredentialsTests.cs` (3 sites, lines 54, 60, 64) — same fix
      and verification, on `DockYarp.Docker.Tests.csproj`.
- [x] 4.5 `tests/DockYarp.Docker.Tests/LabelParserTests.cs` (line 428) — same fix and verification, on
      `DockYarp.Docker.Tests.csproj`.
- [x] 4.6 `tests/DockYarp.E2E.Tests/RestartPersistenceTests.cs` (line 65) — same fix and verification, on
      `DockYarp.E2E.Tests.csproj`.
- [x] 4.7 `tests/DockYarp.E2E.Tests/TlsTests.cs` (5 sites, lines 44, 65, 66, 132, 149) — same fix and
      verification, on `DockYarp.E2E.Tests.csproj`.

## 4b. Fix additional sites revealed once the build graph could reach them (AG-DEP / AG-AT)

The full-solution build (task 5.1) surfaced 15 more sites the isolated single-project builds above could not
see: `DockYarp.Tls`'s own 2 sites blocked MSBuild from even attempting `DockYarp.App` and every test project
depending on it (`DockYarp.Tls.Tests`, `DockYarp.Security.Tests`, `DockYarp.IntegrationTests`) until they were
fixed, hiding those projects' own diagnostics until this point. Fixed with the same discipline (verify each
project's own build after the edit), not batched blind:

- [x] 4.8 `src/DockYarp.App/ErrorPages/ErrorPageMiddleware.cs:26` (S8949) — pass `context.RequestAborted` to
      `response.WriteAsync(...)`.
- [x] 4.9 `tests/DockYarp.Tls.Tests/CertificateStoreTests.cs` (8 sites, S8969) — remove the redundant `!` on
      `loaded`/`afterConversion` after their `Should().NotBeNull()`.
- [x] 4.10 `tests/DockYarp.Security.Tests/HtpasswdStoreTests.cs` (2 sites, S8969) and
      `tests/DockYarp.Security.Tests/DataProtectionSetupTests.cs` (1 site, S8969) — same fix.
- [x] 4.11 `tests/DockYarp.IntegrationTests/ClientIpHashSessionAffinityPolicyTests.cs:35` (1 site, S8969) —
      remove only the `first.Destinations!` forgiving operator (narrowed by `first.Destinations.Should()
      .HaveCount(1)` on the preceding line); `second.Destinations!` on the same line stays, it has no prior
      narrowing.
- [x] 4.12 `tests/DockYarp.IntegrationTests/YarpConfigMapperTests.cs` (2 sites, S8969) — same
      `NotBeNull()`-then-redundant-`!` fix on `mapped.SessionAffinity`.

Real total: **33 unique sites** (18 from the isolated-project scan + 15 revealed once the build graph could
reach every project), not the 18 `proposal.md`/`design.md` estimated before this task ran.

## 5. Verify the full gate (AG-DEP)

- [x] 5.1 Run `dotnet build DockYarp.slnx` and verify 0 warnings, 0 errors across the whole solution.
- [x] 5.2 Run `dotnet test DockYarp.slnx` and verify all unit/integration tests still pass.
- [x] 5.3 Run the full E2E suite (`./build.ps1 E2E` or `./build.sh E2E`) and verify all 42 tests still pass —
      the `EchoerService.cs`/`RestartPersistenceTests.cs`/`TlsTests.cs` changes are E2E-adjacent.
