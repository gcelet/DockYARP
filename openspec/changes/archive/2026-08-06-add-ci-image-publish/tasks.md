## 1. Single Nuke build path (AG-DEP)
- [x] 1.1 `build/Build.cs`: `--platforms` parameter (default `linux/amd64`) + `LatestImage`; `DockerImage` →
      `buildx build --load`, `DockerPublish` → `buildx build --push` (multi-arch, tags `{FullImage}` + `:latest`)

## 2. Workflow delegating to Nuke (AG-DEP)
- [x] 2.1 `.github/workflows/image.yml`: trigger on `v*` tag + `workflow_dispatch`; `permissions: packages: write`
- [x] 2.2 Resolve registry/repository/version from `vars.*` + tag via `env:` (injection-safe), default `ghcr.io`
- [x] 2.3 `docker/login-action` with `secrets.REGISTRY_USERNAME`/`REGISTRY_PASSWORD` (fallback `github.actor`/`GITHUB_TOKEN`)
- [x] 2.4 setup-qemu + setup-buildx, then `./build.sh DockerPublish --registry … --image-repository … --image-tag {version} --platforms linux/amd64,linux/arm64`

## 3. Validate (AG-DEP)
- [x] 3.1 Nuke `Test` gate green (Build.cs compiles); `actionlint`/YAML check; real push waits for the repo +
      registry credentials (dry-run via `act` / `docker buildx build --push=false` meanwhile)
