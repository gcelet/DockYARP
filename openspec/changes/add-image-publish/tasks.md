## 1. Parameterize the image reference (AG-DEP)

- [x] 1.1 Add Nuke parameters `Registry` (optional, empty = Docker Hub), `ImageRepository` (default `dockyarp`), `ImageTag` (default `latest`)
- [x] 1.2 Compute `FullImage` = `{repository}:{tag}` on Docker Hub, else `{registry}/{repository}:{tag}`
- [x] 1.3 Update `DockerImage` to build `-t {FullImage}`

## 2. Publish target (AG-DEP)

- [x] 2.1 Add a `DockerPublish` target depending on `DockerImage` that runs `docker push {FullImage}`
- [x] 2.2 No `docker login` in the build (assume the environment is already authenticated)

## 3. Docs & verification (AG-DEP)

- [x] 3.1 Document `DockerPublish`, the parameters, and the authentication prerequisite in `docs/deployment.md`
- [x] 3.2 Verify the Nuke build still compiles (`./build.ps1` — DockerPublish itself needs Docker on PATH)
