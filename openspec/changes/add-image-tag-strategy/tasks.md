## 1. Nuke build (AG-DEP)

- [x] 1.1 Add `[Parameter] readonly string PublishTag = "";` (explicit-tag override) and
      `[Parameter] readonly bool Edge;` to `build/Build.cs`.
- [x] 1.2 Add a private `ImageRef(string tag)` helper (`{Registry}/{ImageRepository}:{tag}` or
      `{ImageRepository}:{tag}` when `Registry` is empty), reused by both the existing `FullImage` and the new
      tag-list computation. Remove the now-unused `LatestImage` property.
- [x] 1.3 Rewrite `DockerPublish`'s tag computation (`PublishTags()`): if `PublishTag` is set, publish exactly
      that one tag; otherwise build the list from `VersionDetails` — always the exact version; if
      `PackageVersionSuffix` is empty, also `Major.Minor`, `Major`, `latest`; if `Edge`, also `edge`.
- [x] 1.4 Left `DockerImage`/`ImageTag`/`FullImage` behavior unchanged (local `--load` build, unrelated to the
      publish scheme).

## 2. Workflows (AG-DEP)

- [x] 2.1 `image.yml`: added `branches: [develop]` to the `push` trigger.
- [x] 2.2 `image.yml`: scoped the existing job (renamed `publish-release`) to the release path
      (`if: github.event_name == 'workflow_dispatch' || startsWith(github.ref, 'refs/tags/')`); changed its
      Nuke call from `--image-tag "$VERSION"` to `--version "$VERSION"`.
- [x] 2.3 `image.yml`: added a second job `publish-edge`
      (`if: github.event_name == 'push' && github.ref == 'refs/heads/develop'`) that resolves
      registry/repository the same way, then calls
      `./build.sh DockerPublish --registry "$REGISTRY" --image-repository "$REPOSITORY" --edge --platforms linux/amd64,linux/arm64`
      — no `--version` override, letting GitVersion resolve the `develop` prerelease version.
- [x] 2.4 `base-image-refresh.yml`: changed `--image-tag latest` to `--publish-tag latest` (rename only, no
      behavior change).

## 3. Validation (AG-DEP)

- [x] 3.1 Compiled the Nuke build (`./build.ps1 --help`, which builds `_build.csproj` first) — "Build succeeded,
      0 Error(s)", `DockerPublish -> GenerateVersionDetails` listed. (`--help` itself then triggered an
      unrelated Nuke execution quirk on this Nuke version — not a code issue, not chased further.)
- [x] 3.2 Reasoned through `PublishTags()` for the four representative inputs (reimplemented the logic in a
      throwaway Python script rather than trusting a manual trace): stable `0.1.0` → `[0.1.0, 0.1, 0, latest]`;
      prerelease `0.1.0-rc.1` → `[0.1.0-rc.1]` only; edge `0.1.0-alpha.223` → `[0.1.0-alpha.223, edge]`;
      `--publish-tag latest` (the base-image-refresh case) → `[latest]` only. All four match the design exactly.
      No live registry push is possible from this machine without credentials.
- [x] 3.3 Validated both edited workflow YAML files parse (`yaml.safe_load` via `uvx --with pyyaml`) — both OK;
      `image.yml` shows the two expected jobs (`publish-release`, `publish-edge`).
- [x] 3.4 Run `npx @fission-ai/openspec@latest validate add-image-tag-strategy --strict`.
