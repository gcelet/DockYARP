# Design — add-release-versioning

## Tooling: GitVersion as a local .NET tool (not the MsBuild package)
Following the project's house convention, GitVersion is a **local tool** declared in `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "gitversion.tool": { "version": "6.7.0", "commands": ["dotnet-gitversion"], "rollForward": false }
  }
}
```

Only `gitversion.tool` — DockYarp's build/test/coverage already run through Nuke + `coverlet.collector`, so the
other tools from the reference project (husky, coveralls, resharper, regitlint, reportgenerator, dotnet-coverage)
are intentionally omitted. The manifest is a NuGet artifact, so Renovate's `nuget` manager updates the tool
version — no CPM entry (tools are not part of Central Package Management).

The tool (CLI) path — rather than the `GitVersion.MsBuild` package — is chosen deliberately: the Nuke build
computes the version **explicitly on the host** and injects it, which both matches the reference project and is the
clean solution to the Docker `.git` problem (below). No implicit per-project MSBuild stamping.

## `GitVersion.yml`
```yaml
workflow: GitHubFlow/v1
next-version: 0.1.0
commit-message-incrementing: Disabled
branches:
  main:
    regex: ^(master|main|dockyarp)$
  pull-request:
    label: pr
```
- **`GitHubFlow/v1`** (not the reference project's `GitFlow/v1`): DockYarp is single-trunk — `main` plus feature
  branches and `v*` release tags, with no `develop`/`release` branches. (If cross-project homogeneity is preferred,
  switch to `GitFlow/v1`; the rest of the design is unaffected.)
- **`branches.main.regex` includes `dockyarp`**: the trunk currently lives on `dockyarp`, whose history was rewritten
  during the project rename, so it is *orphaned* relative to the `master` backup and GitVersion would otherwise fail
  with "no base versions determined". Classifying it as a main branch makes GitVersion compute from
  `next-version` + commit height (verified: `0.1.0-<height>`). `main` is kept for after the branch is promoted; the
  entry can be trimmed to `^(master|main)$` then.
- **`commit-message-incrementing: Disabled`** mirrors the reference and fits the repo's gitmoji commits (GitVersion
  does not parse gitmoji as semver bumps); the version comes from the base + height + tags.
- `next-version: 0.1.0` sets the pre-1.0 base so untagged builds read `0.1.0-<height>`.

## Nuke wiring (idiomatic — mirrors the reference project)
- `[GitVersion(NoFetch = true, NoCache = true)] readonly GitVersion GitVersion;` (Nuke's native integration,
  `using Nuke.Common.Tools.GitVersion`), backed by the local tool + a `RestoreTools` target that runs
  `DotNetToolRestore()` before anything reads GitVersion.
- A small `VersionDetails` record (`PackageVersionPrefix`/`Suffix`, `Version`, `AssemblyVersion`, `FileVersion`,
  `InformationalVersion`) with a `BuildDefaultFallbackVersion()` (`0.1.0`) for when GitVersion can't resolve.
- A `GenerateVersionDetails` target (`DependsOn(RestoreTools)`) that resolves the version in this precedence:
  1. **`Version` parameter supplied** (the container/build-arg case) → build `VersionDetails` from it, **without**
     touching GitVersion (there is no `.git` in the image);
  2. else **read `GitVersion`** (`MajorMinorPatch`, `PreReleaseTag`, `SemVer`, `AssemblySemVer`,
     `AssemblySemFileVer`, `InformationalVersion`);
  3. else (exception) → `BuildDefaultFallbackVersion()`.
- `Compile` and `Publish` `DependsOn(GenerateVersionDetails)` and stamp via
  `.SetVersion/.SetAssemblyVersion/.SetFileVersion/.SetInformationalVersion` from `VersionDetails`.

## Version flow (host vs container)
- **Host / CI** (`.git` present): `RestoreTools` → `GenerateVersionDetails` reads GitVersion → `Compile`/`Publish`
  stamp the assemblies. `DockerImage`/`DockerPublish` (`DependsOn(GenerateVersionDetails)`) pass
  `--build-arg VERSION={VersionDetails.Version}`.
- **Container** (`.git` excluded by `.dockerignore`): the `Dockerfile` build stage takes `ARG VERSION` and runs
  `build.sh Publish --version "$VERSION"`, so `GenerateVersionDetails` takes branch (1) — the explicit version
  stamps the in-image assemblies identically, GitVersion is never run, no `.git`, no failure.

This keeps a **single build path** (Nuke) authoritative and only ever *injects* the version into Docker, never
recomputing it there.

## Admin API `/api/version`
Add `group.MapGet("/version", …)` returning the entry assembly's
`AssemblyInformationalVersionAttribute.InformationalVersion` (with `AssemblyName.Version` as a structured fallback),
behind the existing API-key filter, plus a small `AdminApiModels.VersionView` record. Unit-testable via the Admin
API test project.

## Validation
Locally verifiable now (the repo has `.git`): `dotnet tool restore` + `dotnet-gitversion` yields a real version; a
build stamps it; the `/api/version` endpoint returns it. The release/tag → GitHub Release + changelog flow is out of
scope here (`add-release-changelog`, repo-dependent).

## Out of scope
- GitHub Release creation + changelog generation on a `v*` tag (`add-release-changelog`).
- Any `GitVersion.MsBuild` per-project stamping (the tool + explicit injection is used instead).
