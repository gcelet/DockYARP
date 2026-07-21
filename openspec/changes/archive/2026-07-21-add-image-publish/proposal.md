## Why

The deployment change builds the image locally; a real workflow also needs to publish it to a registry.
This adds a Nuke `DockerPublish` target that builds (via the existing Nuke-in-Docker pipeline) and pushes
the image to a configurable registry.

## What Changes

- Parameterize the image reference: `Registry` (optional — empty targets **Docker Hub**), `ImageRepository`
  (default `dockyarp`), `ImageTag` (default `latest`). Full reference is `{registry}/{repository}:{tag}`
  (or `{repository}:{tag}` on Docker Hub).
- Add a Nuke **`DockerPublish`** target that depends on `DockerImage` (which builds the image, whose build
  stage runs `build.sh Publish` — the "inception" chain) and then `docker push`es it.
- Publishing **assumes the environment is already authenticated** to the registry (`docker login` done
  externally / by CI). No credentials are handled by the build.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `deployment`: add an image-publishing requirement (push to a configurable registry via Nuke).

## Impact

- **Code**: `build/Build.cs` (registry/repository/tag parameters, `FullImage`, `DockerPublish` target).
- **Docs**: `docs/deployment.md` (publish usage).
- **Testing**: none automated — `DockerPublish` requires Docker on PATH and registry authentication
  (run manually / in CI).
- **Owning agent**: AG-DEP.
