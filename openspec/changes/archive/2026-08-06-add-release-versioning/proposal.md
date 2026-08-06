## Why
Builds are unversioned: `build/Build.cs` carries a manual `Version = ""`, assemblies are unstamped, and the
Admin API exposes no version. The image publish (`add-ci-image-publish`) and any support/diagnostics need a real,
git-derived version. This change wires **git-derived versioning** end to end; the repo-dependent GitHub
Release + changelog is split into `add-release-changelog`.

## What Changes
- Adopt **GitVersion** as a local .NET tool: declare `gitversion.tool` in a `.config/dotnet-tools.json` manifest
  and add a root `GitVersion.yml` (GitHubFlow, `commit-message-incrementing: Disabled`) so the version derives
  from the base version + git height + `v*` tags.
- Compute the version **once on the host** in the Nuke build (`dotnet tool restore` → `dotnet-gitversion`), default
  the `Version` parameter to it, and stamp the assemblies (`-p:Version`/`InformationalVersion`) and the local image
  tag from it (the release workflow keeps passing the tag explicitly).
- Expose the running build's version through the **Admin API** (`GET /api/version`).
- **Stamp the image identically despite `.git` being excluded from the Docker context**: the host-computed version
  is passed to the image build as a `--build-arg`, which the `Dockerfile` forwards to the Nuke publish — so the
  app inside the image reports the same version without shipping `.git` (GitVersion never runs in the container).

## Capabilities
### Modified Capabilities
- `deployment`: builds and the Admin API report a git-derived version; the image is stamped with it via a build arg.

## Impact
- **Code**: `.config/dotnet-tools.json` (new — `gitversion.tool`), `GitVersion.yml` (new), `build/Build.cs`
  (compute + default `Version`/`ImageTag`, forward the build arg), `Dockerfile` (`ARG VERSION` → forwarded to the
  publish), `src/DockYarp.AdminApi/AdminEndpoints.cs` (+ a model) for `/api/version`. Unit-testable + a local build
  check (we have `.git`, so GitVersion resolves a real version locally). **No CPM / `Directory.Build.props` change**
  (a tool manifest is not CPM); Renovate's `nuget` manager keeps `gitversion.tool` updated.
- **Deferred**: the GitHub Release + changelog on a `v*` tag → `add-release-changelog` (needs the repo).
- **Owning agent**: AG-DEP. Resolves `add-release-versioning`; feeds `add-ci-image-publish` / `add-release-changelog`.
