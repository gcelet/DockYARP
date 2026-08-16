---
id: add-doc-acme-port-reachability
capability: documentation
agent: AG-DOC
tier: C-doc
priority: high
status: backlog
nginx-proxy: (internal — doc correctness gap, no parity row)
provenance: 2026-08-16 user's own real-world migration test (macvlan, no host port-remap)
---

## Why
ACME HTTP-01 (and generally, DockYARP being reachable at all under its real hostnames) requires the *external*
world to reach ports 80/443. The docs never state this as an explicit requirement, and the one worked topology
they show (`examples.md`'s base stack: bridge network + `ports: ["80:8080", "443:8443"]`) only works because
Docker's own host port-remap silently absorbs the 8080/8443→80/443 gap. **A different, equally common topology
— macvlan or any other setup with no host port-remap layer (the container gets its own LAN-routable
interface/IP) — has no such remap, and the docs give zero guidance for it.** The user hit this directly while
preparing a real migration test on their own NAS: their existing nginx-proxy stack runs the proxy container on a
macvlan with a static IP/MAC (no `ports:` mapping at all, by design), and DockYARP's non-root image defaults to
listening on 8080/8443 — which is unreachable from the LAN in that topology.

## nginx-proxy behavior
N/A — internal initiative (DockYARP's own doc gap, not a proxy feature). No `parity.md` row.

## DockYarp today
- `Server:HttpPort`/`Server:HttpsPort` (`src/DockYarp.Tls/ServerEndpointOptions.cs:12,15`) ARE already
  configurable — bound from the `Server` config section in `Program.cs:75-76`. The Dockerfile bakes in
  `ENV Server__HttpPort=8080` / `ENV Server__HttpsPort=8443` (non-root defaults), both overridable via compose
  `environment:`. **No code change needed** — this is purely an undocumented capability.
- Binding directly to 80/443 as the image's non-root user requires the `NET_BIND_SERVICE` Linux capability
  (confirmed: no `setcap` baked into the image, no code does this automatically) — `cap_add: [NET_BIND_SERVICE]`
  in compose is sufficient; no separate root image needed (rejected as unnecessary — least-privilege capability
  grant is the standard fix for "non-root process needs a privileged port", same pattern nginx's own official
  image uses via `setcap`).
- `examples.md`'s base stack (and whatever `getting-started.md` ends up documenting after
  `fix-getting-started-socket-bind`) only covers the bridge + port-publish case. Nothing documents: (a) that
  ACME needs 80/443 reachable from the internet/LAN in general, (b) the macvlan/no-remap pattern at all.

## Proposed change (sketch)
- `examples.md`: add a recipe/variant (near the base stack) for a no-host-port-remap deployment (macvlan or
  equivalent): `cap_add: [NET_BIND_SERVICE]` + `Server__HttpPort=80`/`Server__HttpsPort=443`, no `ports:` block,
  with a short note on why (no Docker-level remap in that topology).
  Real-world example verified during the user's own migration prep (2026-08-16).
- Somewhere prominent (getting-started.md and/or configuration.md's Server section): state plainly that ACME
  HTTP-01 needs port 80 reachable from the ACME CA, and port 443 reachable from clients, regardless of topology.
- **Scope overlap flagged, not resolved here**: this touches the same files (`examples.md`, possibly
  `getting-started.md`) as `fix-getting-started-socket-bind` (also queued). Decide at propose time whether to
  fold this into that item or keep separate — both are doc-correctness fixes discovered from the same review
  pass, but this one is content-additive (a missing topology) while that one is a bug fix (a broken example).

## Acceptance criteria (→ scenarios)
- **WHEN** a reader deploys DockYARP on a topology with no host port-remap (macvlan, host networking, etc.)
  **THEN** the docs show them exactly what to change (capability + port config) to make ACME/HTTP(S) work.
- **WHEN** a reader looks for "why does DockYARP need 80/443" **THEN** the reachability requirement is stated
  explicitly, not just implied by an example that happens to work.

## Notes / risks / references
- Discovered while generating real `compose.yaml`/`.env` for the user's own `10-front-door` → DockYARP migration
  test (not committed material — see the private, gitignored recap file used as grounding).
- Refs: `src/DockYarp.Tls/ServerEndpointOptions.cs`, `src/DockYarp.App/Program.cs:73-77`, `Dockerfile:29-31`,
  `docs-site/content/en/docs/examples.md` (base stack).
