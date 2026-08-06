## Why
The app image is built `FROM mcr.microsoft.com/dotnet/...`; between DockYarp releases the base image receives
security patches, so a published `:latest` goes stale (unpatched OS/runtime CVEs). Renovate now digest-pins the
`FROM` and opens a bump PR when the base updates (`add-renovate-bot`), but nothing rebuilds the image on merge.

## What Changes
- Add a `base-image-refresh.yml` workflow that triggers on a push to the default branch touching the `Dockerfile`
  (i.e. a merged Renovate digest bump, or any `Dockerfile` edit) and **rebuilds + republishes `:latest`** with the
  patched base — delegating the build+push to the single Nuke `DockerPublish` path (no duplicated build logic).
- Released `v*` image tags stay **immutable**: a base patch lands on `:latest`, and the next tagged release picks up
  the patched base through `image.yml`.

## Capabilities
### Modified Capabilities
- `deployment`: the published `:latest` image is rebuilt when the base image changes, keeping it patched.

## Impact
- **Code**: `.github/workflows/base-image-refresh.yml` (new). No product code.
- **Validation**: `actionlint` locally; the real trigger (a Renovate digest-bump merge) runs once the repo exists.
- **Design**: primary path only (Renovate digest pin → merge → rebuild). A scheduled cron "safety net" is
  deliberately **out of scope** — it would duplicate Renovate's digest watching and require digest-diff logic.
- **Owning agent**: AG-DEP. Depends on `add-ci-image-publish` + `add-renovate-bot`. Resolves `add-base-image-rebuild`.
