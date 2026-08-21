---
id: add-session-affinity-unencrypted-cookie
capability: yarp-dynamic-config
agent: AG-RP
tier: C-spike
priority: low
status: backlog
nginx-proxy: n/a (DockYarp value-add, beyond nginx-proxy's own ceiling)
provenance: 2026-08-21, split off from `add-session-affinity` during its propose phase — the user explicitly
  asked to add YARP's built-in Cookie/CustomHeader affinity policies (beyond nginx-proxy parity), then asked
  that the two YARP built-ins deliberately left out of that change (HashCookie, ArrCookie) be tracked rather
  than silently dropped
---

## Why

`add-session-affinity` ships three affinity policies: a custom client-IP-hash policy (`ip-hash`, true
nginx-proxy `ip_hash` parity) plus YARP's built-in `Cookie` and `CustomHeader` policies (both Data
Protection-encrypted, a DockYarp value-add beyond what nginx-proxy — built on open-source nginx, which has no
cookie-based sticky-session mechanism at all — can offer). YARP ships two more built-in policies not included
in that change: `HashCookie` (YARP's own default) and `ArrCookie` (matches IIS ARR's cookie format). Both are
cookie-based but **unencrypted** — no Data Protection dependency, unlike `Cookie`/`CustomHeader`.

## Current state

- `add-session-affinity` (see its design.md's Non-Goals) deliberately excluded these two to keep that change's
  config/test surface contained — nothing in its design blocks adding them later; the same
  `SessionAffinityPolicy` enum, label parsing, and `YarpConfigMapper` mapping shape extend naturally.
- Per Microsoft's own YARP documentation: `HashCookie` uses XxHash64 for a fast, compact, obscured cookie
  value; `ArrCookie` uses SHA-256 to match IIS Application Request Routing's own affinity cookie format
  (destination host name as the input, so destination ids would need to match ARR's expectations if used
  alongside a real ARR deployment). Neither provides strong privacy protection (the destination-id space isn't
  concealed the way an encrypted `Cookie` policy's is) — this is the practical reason they weren't the
  original design's default choice, but they remain a legitimate lighter-weight option (no DP cert to manage)
  for an operator who wants *some* cookie-based stickiness without taking on Data Protection configuration.

## Proposed change (sketch)

Not designed — needs propose-time decisions if picked up. Likely shape, extending `add-session-affinity`'s
already-shipped pattern (once that change is archived, read its shipped code directly rather than re-deriving
from this stub):
- Extend `SessionAffinityPolicy` (`src/DockYarp.Core/Models/SessionAffinityPolicy.cs`) with `HashCookie` and
  `ArrCookie` members.
- Extend `LabelParser.ParseAffinityPolicy`/`ResolveAffinity` (`src/DockYarp.Docker/Labels/LabelParser.cs`) to
  recognize `DOCKYARP_AFFINITY=hash-cookie`/`arr-cookie`.
- Extend `YarpConfigMapper.BuildCluster`'s policy switch (`src/DockYarp.App/ReverseProxy/YarpConfigMapper.cs`)
  to map these two directly to YARP's own built-in policy names — no custom `ISessionAffinityPolicy`
  implementation needed (unlike `ip-hash`), matching how `Cookie`/`CustomHeader` were already mapped.
- Neither needs the Data Protection gating `Cookie`/`CustomHeader` required — no fail-fast/degradation logic to
  add for these two.

## Acceptance criteria (→ scenarios)

- **WHEN** a container sets `DOCKYARP_AFFINITY=hash-cookie` **THEN** its cluster uses YARP's `HashCookie`
  policy, with no Data Protection requirement.
- **WHEN** a container sets `DOCKYARP_AFFINITY=arr-cookie` **THEN** its cluster uses YARP's `ArrCookie` policy,
  with no Data Protection requirement.

## Notes / risks / references

- Sibling (done, or in progress): `add-session-affinity` — read its shipped code/spec first; this item is a
  narrow, additive extension of that shape, not a new design.
- Low priority / spike tier: pick up only if a real operator need for unencrypted-but-cookie-based affinity
  surfaces — `ip-hash` (nginx-proxy parity) and `cookie`/`custom-header` (encrypted) already cover the two
  most-requested shapes.
