## Context

`YarpConfigMapper.BuildCluster` uses `Endpoints.ToDictionary(e => e.Id, …)`, which throws on duplicate keys.
Duplicate endpoint ids arise from a repeated `VIRTUAL_HOST` entry (the container is added twice to a host
group, both endpoints keyed by container id) or a static-config cluster listing the same address twice
(endpoint id = address). The throw fails the whole publish rather than tolerating the bad entry.

## Goals / Non-Goals

**Goals:** never fail configuration on duplicate endpoint ids; drop a repeated host at the source.

**Non-Goals:** validating/merging endpoint *addresses* (only ids), or changing how endpoints are keyed.

## Decisions

- **Mapper de-dupe (the guarantee):** `BuildCluster` builds destinations with a dictionary indexer
  (last-wins) instead of `ToDictionary`, so any duplicate endpoint id collapses to one destination. This is
  the single choke point that covers every source (discovery and static).
- **Source de-dupe (hygiene):** `LabelParser.SplitHosts` skips a host already seen (case-insensitive), so a
  repeated `VIRTUAL_HOST` produces one host — avoiding the duplicate endpoint and duplicate-route work
  upstream.

## Risks / Trade-offs

- Last-wins on duplicate ids is arbitrary but safe; duplicates are authoring mistakes, and both entries carry
  the same intent in the reachable cases.

## Migration Plan

Pure fix: swap the destination build and add a de-dupe check in `SplitHosts`. No config or model changes.

## Open Questions

- None.
