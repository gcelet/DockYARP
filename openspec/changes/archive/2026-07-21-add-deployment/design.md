## Context

DockYarp is functionally complete at runtime (routing, discovery, YARP, security, admin, TLS). It now
needs packaging: a small secure image, a way to run it like nginx-proxy, and CI/CD via Nuke (the build
project already exists with empty targets). Docker discovery was deferred from the YARP change and is
wired here so a deployed instance is self-configuring.

## Goals / Non-Goals

**Goals:**
- Minimal, non-root **chiseled** image with `/certs` and `/config` volumes.
- Host wiring for Docker discovery (opt-in) and graceful shutdown.
- Reference Compose stack demonstrating label-based routing.
- Nuke targets for restore/compile/test/publish/image/E2E.

**Non-Goals:**
- No real ACME/TLS in the local Compose demo (public DNS required); local demo shows HTTP proxying and the
  self-signed fallback. No multi-arch/registry push here (a CI concern).

## Decisions

- **Chiseled runtime** `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`, running as the image's
  non-root `app` user. Chiseled images ship no ICU, so the app sets **`InvariantGlobalization=true`** (it
  already uses invariant/ordinal operations).
- **Nuke owns the build, including inside the image.** The Dockerfile's build stage runs
  `bash build.sh Publish --configuration Release` — the same Nuke pipeline used on developer/CI machines —
  so there is one build definition everywhere (no ad-hoc `dotnet publish` in the Dockerfile). `build.sh`
  makes the Nuke build runnable in any .NET environment (here, the `sdk:10.0` build stage). The chiseled
  runtime stage copies `/src/artifacts/publish`. `nuke DockerImage` gates on `Test`, then `docker build`.
- **Discovery wiring is opt-in**: `Docker:Enabled` (default **false**). Tests and local `dotnet run` stay
  quiet; the Compose stack sets `Docker__Enabled=true` and mounts the Docker socket read-only. Rationale:
  keeps the existing integration tests (no daemon) clean while making the deployed stack self-configuring.
- **Graceful shutdown** via `HostOptions.ShutdownTimeout`; Kestrel drains in-flight requests and background
  services already honor the stopping token. The timeout is configurable (`Host:ShutdownTimeoutSeconds`).
- **E2E as a Nuke target**, not a `dotnet test` test: `nuke E2E` runs `docker compose up`, probes the sample
  service by `VIRTUAL_HOST`, and tears down. Rationale: keeps `dotnet test` daemon-free; the E2E requires
  Docker on PATH (prerequisite).
- **Nuke** implements the pipeline using the solution path directly (avoids `.slnx` solution-model parsing):
  Clean → Restore → Compile → Test → Publish → DockerImage, plus E2E.

## Risks / Trade-offs

- Local Compose can't obtain real certificates (no public DNS) → demo uses HTTP + the self-signed fallback;
  documented. Production TLS needs public domains + the production ACME directory.
- Mounting the Docker socket grants broad access → mounted **read-only**; documented as the discovery
  requirement, same model as nginx-proxy.

## Migration Plan

Additive: new files (Dockerfile, compose, scripts), Nuke target bodies, and host wiring. No data migration.

## Open Questions

- Registry/tagging and multi-arch builds — left to CI configuration, out of scope here.
