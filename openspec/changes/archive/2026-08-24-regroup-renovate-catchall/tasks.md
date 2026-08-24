## 1. Add the logical catch-all groups (AG-DEP)

- [x] 1.1 Add the `nuget dependencies` catch-all `packageRule` to `renovate.json`, placed before the 5
      existing named groups.
- [x] 1.2 Add the `build tooling` `packageRule` (`matchFileNames`: `build/_build.csproj`,
      `.config/dotnet-tools.json`), placed after 1.1 so it overrides the nuget catch-all for those files.
- [x] 1.3 Add the `npm dependencies` and `github-actions dependencies` catch-alls.

## 2. Verify (AG-DEP)

- [x] 2.1 Validate `renovate.json` with the real `renovate-config-validator`
      (`npx --yes -p renovate renovate-config-validator renovate.json`) and confirm it reports success, not
      just JSON-syntax validity.
