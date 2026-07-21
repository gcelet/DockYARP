## Context

`add-deployment` produces the image via a multi-stage Dockerfile whose build stage runs the Nuke pipeline
(`build.sh Publish`). This adds publishing to a registry, keeping the same Nuke-driven build.

## Goals / Non-Goals

**Goals:**
- A configurable image reference (registry optional → Docker Hub; repository; tag).
- A `DockerPublish` Nuke target that builds and pushes.

**Non-Goals:**
- No credential handling (assume `docker login` already done). No multi-arch/registry-specific logic.

## Decisions

- **Image reference** computed from parameters: `FullImage = Registry` empty ? `{ImageRepository}:{ImageTag}`
  : `{Registry}/{ImageRepository}:{ImageTag}`. Defaults: registry empty (Docker Hub), repository
  `dockyarp`, tag `latest`. `DockerImage` builds `-t {FullImage}`.
- **"Inception" chain**: `nuke DockerPublish` → depends on `DockerImage` → `docker build` (Nuke target) →
  the Dockerfile build stage runs `bash build.sh Publish` (inner Nuke) → `DockerPublish` then
  `docker push {FullImage}`. One build definition (Nuke) at every level.
- **No authentication in the build**: `DockerPublish` does not `docker login`; the environment/CI is
  expected to be authenticated. Rationale: keeps secrets out of the build; matches the chosen workflow.

## Risks / Trade-offs

- Push fails if not authenticated → documented prerequisite; the push exit code surfaces the error.

## Migration Plan

Additive: parameters + one Nuke target. No runtime/code changes to the app.

## Open Questions

- Adding `docker login` with secret parameters could be a later option if unauthenticated CI needs it.
