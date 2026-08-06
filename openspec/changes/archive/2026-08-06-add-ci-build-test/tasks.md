## 1. Workflow (AG-DEP)
- [x] 1.1 `.github/workflows/ci.yml`: on `pull_request` + `push` to `main`; `ubuntu-latest`; checkout,
      `setup-dotnet` from `global.json`, NuGet cache, `./build.sh Test`
- [x] 1.2 Least-privilege `permissions: contents: read`; `concurrency` cancels superseded runs
- [x] 1.3 Relax `global.json` to accept any installed .NET 10 SDK (`version 10.0.100`, `rollForward latestMinor`)

## 2. Validate (AG-DEP)
- [x] 2.1 Lint locally: `actionlint .github/workflows/ci.yml` (and/or `act pull_request -j build-test`) — the
      repo/registry is not created yet, so on-push validation is deferred
