## Why

ACME HTTP-01 needs port 80 reachable from the certificate authority, and clients need port 443 reachable, for
DockYARP to actually work — but the docs never state this as a requirement, only imply it through the one
worked topology they show (`examples.md`'s base stack: a bridge network with `ports: ["80:8080", "443:8443"]`,
where Docker's own host port-remap silently absorbs the 8080/8443→80/443 gap). A different, equally common
topology — macvlan, host networking, or any setup with no host port-remap layer (the container gets its own
LAN-routable interface) — has no such remap and zero documented guidance. The user hit this directly while
migrating their own real nginx-proxy installation (which runs on exactly this kind of macvlan setup) to
DockYARP: the non-root image's 8080/8443 defaults were unreachable from the LAN, and nothing in the docs said
what to change.

## What Changes

- New recipe in `examples.md`, near the base stack: a no-host-port-remap deployment (macvlan or equivalent) —
  `cap_add: [NET_BIND_SERVICE]` (lets the non-root process bind privileged ports; no separate root image needed,
  same least-privilege pattern nginx's own official image uses via `setcap`) + `Server__HttpPort: "80"` /
  `Server__HttpsPort: "443"`, no `ports:` block, with a short explanation of why (no Docker-level remap in that
  topology).
- `configuration.md`'s `Server` section gains an explicit statement: ACME HTTP-01 needs port 80 reachable from
  the certificate authority, and clients need port 443 reachable, regardless of topology — not just implied by
  an example that happens to work.
- No application code changes — `Server:HttpPort`/`Server:HttpsPort` are already fully configurable
  (`src/DockYarp.Tls/ServerEndpointOptions.cs`, bound in `Program.cs`); this is purely an undocumented
  capability and a missing topology in the existing recipes.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `documentation`: "Worked configuration examples" requirement gains a scenario for the no-host-port-remap
  topology; "Application configuration reference" requirement's `Server` section documentation gains the
  explicit port-reachability statement.

## Impact

- `docs-site/content/en/docs/examples.md` — new recipe.
- `docs-site/content/en/docs/configuration.md` — `Server` section statement.
- No `src/`/`tests/` changes.
