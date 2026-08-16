## Context

See `proposal.md` - Why. Purely a documentation-content change — `Server:HttpPort`/`Server:HttpsPort` are
already fully configurable (`src/DockYarp.Tls/ServerEndpointOptions.cs`, bound from the `Server` config section
in `Program.cs`); the Dockerfile's `EXPOSE 8080`/`EXPOSE 8443` and `ENV Server__HttpPort=8080`/
`ENV Server__HttpsPort=8443` are the non-root defaults, both overridable via compose `environment:`. No code
change is needed or in scope.

## Goals / Non-Goals

**Goals:**
- Document the macvlan/no-host-port-remap topology as its own recipe, distinct from the base stack's
  bridge+port-publish default.
- State the port-80/443 reachability requirement explicitly, not just implied by an example.

**Non-Goals:**
- Not touching `getting-started.md` or `deployment.md` — both already show the bridge+port-publish pattern
  correctly (fixed in `fix-getting-started-socket-bind`); this change adds a *second*, alternative recipe for a
  different topology, it doesn't change those pages' existing content.
- Not modeling every possible network topology (host networking, `--network container:`, etc.) — macvlan is the
  concrete case that surfaced this gap; the recipe's explanation (no Docker-level port remap) generalizes to any
  topology sharing that property, without needing a recipe per topology name.

## Decisions

- **`cap_add: [NET_BIND_SERVICE]`, not a root image.** Already settled in the backlog stub (and independently
  during the REDACTED real-deployment work earlier this session): the Linux capability is the standard
  least-privilege mechanism for "non-root process needs to bind a privileged port" — the same pattern nginx's
  own official image uses via `setcap`. A second root-variant image was considered and rejected as unnecessary
  complexity for a problem one capability grant already solves.
- **A new recipe, not a rewrite of the base stack.** The base stack's bridge+port-publish shape is the more
  common case and should stay the default reader sees first; the no-remap topology is presented as an
  alternative recipe alongside the others (Basic virtual host, Path routing, etc.), not a replacement.

## Risks / Trade-offs

- [Risk] A reader on a standard bridge-network setup could mistakenly apply the `NET_BIND_SERVICE` + 80/443
  recipe and end up with a *working but unnecessarily different* config (no functional harm, just needless
  divergence from the simpler default) → [Mitigation] the recipe explicitly states which topology it's for and
  why the default doesn't apply there.
