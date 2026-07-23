## Context

`RouteConfigMerger` already ranks `Static` over `Dynamic` and validates entries, but the only producer is
Docker discovery (`Dynamic`). `DiscoveryReconciler` merges `[dynamic]` and applies to the store; when Docker
is disabled nothing applies. This change adds a static source read from a file and wires it into both paths.

## Goals / Non-Goals

**Goals:** load a JSON static config (routes + clusters) into a `Static` contribution; merge it with
discovery (static wins) when Docker is on; apply it at startup when Docker is off.

**Non-Goals (deferred):** hot-reload on file change, YAML, and advanced per-route fields (TLS/auth/transforms)
in the static file — clusters carry addresses + LB, routes carry host/path/cluster/priority.

## Decisions

- **`IStaticConfigProvider` in Core.Configuration** returning a cached `ConfigContribution` (Source =
  `Static`); a Core `EmptyStaticConfigProvider` is the neutral default (and is used by discovery tests).
- **`StaticConfigProvider` (App)** reads `StaticConfig:Path` via `System.IO.Abstractions`, deserializes a
  small JSON DTO (System.Text.Json, case-insensitive), and maps it to core routes/clusters — leniently, so
  the merger performs validation/diagnostics. A missing path or unreadable/invalid file yields an empty
  contribution (fail-open, logged via source-generated messages).
- **Discovery path**: `DiscoveryReconciler` now merges `[static, dynamic]`, so static wins and reloads on
  every reconcile.
- **Docker-off path**: a `StaticConfigService` (hosted, registered only when discovery is disabled) merges
  the static contribution and applies it once at startup. The two paths are mutually exclusive (mirroring
  the existing `Docker:Enabled` branch), so there is a single writer to the store.

## Risks / Trade-offs

- Two apply paths (reconciler vs. startup service) share the merge/apply shape; they never run together
  because registration is gated on `Docker:Enabled`, avoiding a double writer.
- Static config is loaded once at startup (cached); editing the file requires a restart until hot-reload is
  added.

## Migration Plan

Additive: a Core interface + empty default, an App provider/service, and one extra contribution in the
reconciler merge. `DiscoveryReconciler`'s constructor gains the provider (updated with its tests). Behavior
is unchanged when no static file is configured.

## Open Questions

- Hot-reload and richer per-route static fields — deferred; revisit once the file format stabilizes.
