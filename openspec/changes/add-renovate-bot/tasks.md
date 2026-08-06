## 1. Config (AG-DEP)
- [x] 1.1 `renovate.json`: extend `config:recommended`; enable nuget (CPM) / github-actions / docker (`pinDigests`)
      / npm; weekly schedule; dependency dashboard; gitmoji + `chore:` commit convention (upgrade/pin/downgrade)
- [x] 1.2 Grouping rules: analyzers, Aspire, OpenTelemetry, test stack, gRPC
- [x] 1.3 Disable re-pinning the .NET SDK (`dotnet-version` datasource) so the relaxed `global.json` floor stands

## 2. Validate (AG-DEP)
- [x] 2.1 `npx --yes renovate-config-validator renovate.json` passes; Renovate app install waits for the repo
