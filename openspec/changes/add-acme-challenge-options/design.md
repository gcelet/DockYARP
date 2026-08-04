# Design — add-acme-challenge-options

## Two nginx knobs, one real change for DockYarp
nginx-proxy serves the ACME HTTP-01 challenge from an nginx `location` scoped per vhost, hence two knobs:
disable the location, and accept challenges for unknown `server_name`s. DockYarp's architecture differs — it
**is** the ACME client and serves challenges from an in-memory token store:

- `Http01ChallengeStore` maps `token → keyAuthorization` (no host).
- `Http01ChallengeMiddleware` answers `/.well-known/acme-challenge/{token}` from that store for **any** host.

So `ACME_HTTP_CHALLENGE_ACCEPT_UNKNOWN_HOST` is **inherently satisfied**: a challenge for a host with no route
is still answered, because matching is by token. The store only ever holds tokens DockYarp itself set while
provisioning a `LETSENCRYPT_HOST`, so this is safe (DockYarp never answers a token it did not create). This
change only needs to **state** that behavior as a requirement — no toggle.

The one real knob is `ACME_HTTP_CHALLENGE_LOCATION` → **`Tls:Http01ChallengeEnabled`** (default `true`).

## Disable behavior
```
InvokeAsync:
  if path starts with "/.well-known/acme-challenge/":
      if !options.Http01ChallengeEnabled: 404; return     // location disabled
      token = ...; serve from store or 404; return
  await next()
```
When disabled, the challenge path returns 404 (per the acceptance criterion) rather than falling through, so
`/.well-known/acme-challenge/*` is never routed to a backend. Default (`true`) is byte-for-byte the current
behavior.

## Wiring
`Http01ChallengeMiddleware` gains a `TlsOptions` constructor parameter (already a DI singleton via
`AddDockYarpTls`); it reads `Http01ChallengeEnabled`. No pipeline/registration change.

## Out of scope
- nginx's `legacy` mode (an nginx-location detail with no DockYarp analog).
- Any change to ACME issuance/renewal or the token store contract.
