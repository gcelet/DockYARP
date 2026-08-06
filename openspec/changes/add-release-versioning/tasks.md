## 1. Tooling (AG-DEP)
- [x] 1.1 Add `.config/dotnet-tools.json` (isRoot) declaring `gitversion.tool` 6.7.0 (`dotnet-gitversion`, `rollForward:false`)
- [x] 1.2 Add root `GitVersion.yml` (GitHubFlow/v1, `next-version: 0.1.0`, `commit-message-incrementing: Disabled`, PR label `pr`)

## 2. Build wiring (AG-DEP)
- [x] 2.1 Nuke: `[GitVersion(NoFetch,NoCache)]` + a `RestoreTools` target (`DotNetToolRestore()`); add a `VersionDetails` type with a `BuildDefaultFallbackVersion()`
- [x] 2.2 `GenerateVersionDetails` target (`DependsOn RestoreTools`): precedence `Version` param → GitVersion → fallback
- [x] 2.3 `Compile`/`Publish` (`DependsOn GenerateVersionDetails`) stamp via `.SetVersion/.SetAssemblyVersion/.SetFileVersion/.SetInformationalVersion`
- [x] 2.4 `DockerImage`/`DockerPublish` (`DependsOn GenerateVersionDetails`) pass `--build-arg VERSION={VersionDetails.Version}`; `Dockerfile` `ARG VERSION` → `build.sh Publish --version "$VERSION"`

## 3. Admin API (AG-AA)
- [x] 3.1 `GET /api/version` returns the running build's informational version (+ `AdminApiModels.VersionView`)

## 4. Verify (AG-DEP)
- [x] 4.1 Local: GitVersion resolves a real version (`0.1.0-216`) and a build stamps it; the explicit `--version` path bypasses GitVersion (container case)
- [x] 4.2 Nuke `Test` gate green (357 tests, incl. `/api/version` cases); warnings-as-errors clean
