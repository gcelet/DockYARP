## Why
nginx-proxy exposes two knobs over the ACME HTTP-01 challenge location: `ACME_HTTP_CHALLENGE_LOCATION`
(`true`/`false`/`legacy`) to enable/disable serving it, and `ACME_HTTP_CHALLENGE_ACCEPT_UNKNOWN_HOST` to answer
challenges for hosts with no matching vhost. DockYarp always serves the challenge and exposes no toggle.

## What Changes
- Add `Tls:Http01ChallengeEnabled` (default `true`). When `false`, the challenge path
  (`/.well-known/acme-challenge/{token}`) returns 404 instead of serving the token.
- **Accept-unknown-host is already the behavior**: DockYarp's `Http01ChallengeStore` is keyed by token
  (not host) and `Http01ChallengeMiddleware` serves any token in the store independently of host routing.
  The store only holds tokens DockYarp is itself provisioning, so a challenge for a not-yet-routed host is
  already answered — there is no separate toggle to add, only a requirement to state it.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: the HTTP-01 challenge is served host-agnostically and can be disabled via
  `Tls:Http01ChallengeEnabled`.

## Impact
- **Code**: `DockYarp.Tls` — `TlsOptions.Http01ChallengeEnabled`; `Http01ChallengeMiddleware` honors it
  (returns 404 on the challenge path when disabled).
- **Tests (unit)**: `Http01ChallengeMiddleware` — a token is served regardless of host; when disabled the
  challenge path returns 404 (existing token/unknown-token/other-path tests keep passing).
- **Docs**: the site configuration reference documents `Tls:Http01ChallengeEnabled`.
- **Runtime / e2e**: none (the middleware behavior is fully unit-testable; the live ACME flow is unchanged).
- **Owning agent**: AG-AT. Resolves `add-acme-challenge-options`.
