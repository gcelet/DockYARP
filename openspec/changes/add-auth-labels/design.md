## Context

Basic Auth is fully implemented (`RouteRule.Auth` + `BasicAuthMiddleware`) but nothing populates it:
`docker-discovery` parses no auth labels. This wires the labels through the existing pure parser and mapper.

## Goals / Non-Goals

**Goals:** parse `DOCKYARP_AUTH_USER`/`DOCKYARP_AUTH_PASSWORD`/`DOCKYARP_AUTH_REALM` into the route's
`BasicAuthCredentials`; incomplete auth is logged and leaves the route unprotected (no crash).

**Non-Goals:** htpasswd files, per-path auth, digest/other schemes.

## Decisions

- **New label constants** in `DockerLabels`: `AuthUser`, `AuthPassword`, `AuthRealm`.
- **`ContainerLabelConfig.Auth`** (`BasicAuthCredentials?`) populated by `LabelParser`: credentials are set
  only when **both** user and password are present; otherwise `null`. Realm is optional.
- **Incomplete-auth warning** is emitted by the mapper (which owns the warnings collection): a new
  `LabelParser.HasIncompleteAuth(labels)` reports when exactly one of user/password is present, so the
  mapper logs it and leaves the route unprotected. Rationale: keep the parser pure; keep warnings where
  the other skip/validation warnings live.
- **`HostGroup.BuildRoute`** sets `Auth = first.Auth` (consistent with how it uses the first config for
  TLS/LB), so replicas of a host share the auth of the first-seen container.

## Risks / Trade-offs

- Passwords flow through labels (visible via `docker inspect`) — same trust model as nginx-proxy htpasswd
  files; documented. Credentials are never logged (the middleware already guarantees this).

## Migration Plan

Additive: new label constants + one optional config field + mapper wiring.

## Open Questions

- Per-path auth (nginx `htpasswd/<host>_<hash>`) — deferred with the static-config/extensibility backlog.
