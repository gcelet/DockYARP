---
id: add-wildcard-regex-hosts
capability: proxy-routing
agent: AG-RP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: VIRTUAL_HOST (wildcard / regex forms)
provenance: this parity pass (matrix row was ⚠️ single-level only)
---

## Why
nginx-proxy matches `VIRTUAL_HOST` as a wildcard (`*.bar.com`, `foo.bar.*`) **and** as a regex
(`~^foo\.bar\..*\.nip\.io`). DockYarp only supports a single-level `*.suffix` wildcard, so multi-level
wildcards and regex hosts silently fail to route — a real parity gap for nip.io/sslip.io-style setups.

## nginx-proxy behavior
- Wildcard hostnames: `*.bar.com` and `foo.bar.*` (docs "Wildcard Hosts").
- Regex hostnames: a `VIRTUAL_HOST` starting with `~` is treated as an nginx `server_name` regex; regex hosts
  always get SHA1 upstream names. Precedence still follows nginx `server_name` specificity (exact > wildcard >
  regex).

## DockYarp today
Single-level `*.suffix` wildcard only (host matching in `src/DockYarp.Core/Routing/RouteMatcher.cs`); the
label is parsed verbatim (`src/DockYarp.Docker/Labels/LabelParser.cs`). Multi-level and regex forms are ⛔.

## Proposed change (sketch)
Extend `RouteMatcher` host matching with (a) multi-level wildcard support and (b) an optional `~`-prefixed
regex host compiled to a `Regex` (cached, `RegexOptions.Compiled | CultureInvariant`). Preserve the existing
precedence order: exact > wildcard (more specific first) > regex > priority > longest path. Map to YARP via a
host header match (YARP supports wildcard hosts natively; regex needs a custom match predicate/transform).

## Acceptance criteria (→ scenarios)
- **WHEN** a request host matches a multi-level wildcard `*.a.example.com` and no exact route exists **THEN**
  it routes to that vhost.
- **WHEN** both an exact host and a matching wildcard exist **THEN** the exact host wins.
- **WHEN** `VIRTUAL_HOST=~^app-\d+\.example\.com$` and the host matches the regex **THEN** it routes; a
  non-matching host does not.

## Notes / risks / references
- Confirm YARP's wildcard-host semantics vs nginx before implementing; regex likely needs a custom matcher.
- Keep regex compilation bounded/cached to protect the hot path (no per-request compile).
