## 1. Workflow (AG-DEP)
- [x] 1.1 `.github/workflows/base-image-refresh.yml`: `push` to the default branch `paths: [Dockerfile]` + `workflow_dispatch`; `permissions: contents:read, packages:write`
- [x] 1.2 Checkout `fetch-depth: 0` (GitVersion); resolve registry/repository (same as `image.yml`, `env:`-passed); setup-dotnet + QEMU + Buildx + login
- [x] 1.3 Delegate to `./build.sh DockerPublish --registry --image-repository --image-tag latest --platforms linux/amd64,linux/arm64` (single Nuke path, `:latest` only)

## 2. Verify (AG-DEP)
- [x] 2.1 YAML syntax-checked; structure mirrors the actionlint-clean `image.yml`. `actionlint` on a capable machine + the real trigger (Renovate digest-bump merge) deferred to the repo
