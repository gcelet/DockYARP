## 1. Option (AG-AT)
- [x] 1.1 `TlsOptions.Http01ChallengeEnabled` (bool, default `true`)

## 2. Honor it (AG-AT)
- [x] 2.1 `Http01ChallengeMiddleware` takes `TlsOptions`; when disabled, a challenge-path request returns 404

## 3. Tests (AG-AT)
- [x] 3.1 A token is served regardless of host (host-agnostic serving; accept-unknown-host is inherent)
- [x] 3.2 When `Http01ChallengeEnabled` is false, the challenge path returns 404 (even for a stored token)
- [x] 3.3 Update the existing middleware tests' constructor calls to pass `TlsOptions`

## 4. Docs (AG-DOC)
- [x] 4.1 Site configuration reference: document `Tls:Http01ChallengeEnabled`

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker)
