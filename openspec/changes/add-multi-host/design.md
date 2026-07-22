## Context

nginx-proxy accepts a comma-separated `VIRTUAL_HOST`, exposing one container under several hostnames.
DockYarp's `LabelParser` treats the whole value as one host pattern, so `app.local,www.app.local`
yields a single (broken) route. The split point is the pure parser/mapper pipeline already used for every
other label, so this is a focused, additive change with no runtime plumbing.

## Goals / Non-Goals

**Goals:** split `VIRTUAL_HOST` on commas (trim whitespace, drop empties) and fan the container out to one
route per host, each sharing the container's port, path, TLS, and auth; a value with no valid host is
skipped and logged (unchanged behavior).

**Non-Goals:** comma-separated `LETSENCRYPT_HOST` (per-host cert selection stays in the TLS backlog),
`VIRTUAL_HOST_MULTIPORTS`, wildcard/regex host patterns.

## Decisions

- **`ContainerLabelConfig.Host` (string) → `Hosts` (`ImmutableArray<string>`).** The config already is the
  single carrier of parsed intent; making it multi-host there keeps the parser pure and the mapper simple.
- **`LabelParser` splits and validates.** A new private `SplitHosts` trims each comma segment and drops
  empties; `TryParse` fails with the existing "required" error when no valid host remains (covers both a
  missing label and a value like `", ,"`). All other fields are unchanged.
- **`ContainerMapper` fans out.** `GroupByHost` iterates `config.Hosts` and adds the container's endpoint to
  each host's `HostGroup`. Cross-container aggregation per host is unchanged (still keyed by host,
  case-insensitive), so replicas across hosts keep aggregating correctly. Route/cluster building is
  untouched — each host still builds from its group's first config.

## Risks / Trade-offs

- A container listing the same host twice would add its endpoint twice to that cluster. Docker labels can't
  hold duplicate keys and the value would be an authoring mistake; not de-duplicating keeps the parser
  trivial and matches nginx-proxy (which also does not dedupe). Left as-is; can revisit if it bites.

## Migration Plan

Additive shape change to an internal record: `Host` → `Hosts`. Only the Docker module and its tests read the
field; updated in the same change. No config, no persisted state.

## Open Questions

- Comma-separated `LETSENCRYPT_HOST` and per-host certificate selection — deferred to the TLS backlog
  (`add-provided-certificates` / `add-tls-hardening`).
