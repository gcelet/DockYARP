## Why
nginx-proxy enables Basic Auth by mounting standard Apache htpasswd files per vhost (and per path). DockYarp
only supports Basic Auth via `DOCKYARP_AUTH_*` labels — a single credential, awkward for multiple users and
placing secrets in labels. File-based htpasswd is the idiomatic operator workflow (multiple users, hashed
passwords, no secrets in labels).

## What Changes
- **Config**: `Security:HtpasswdDirectory` — a directory of htpasswd files, loaded at startup. A file named
  `<host>` protects the whole vhost; `<host>_<sha1hex(VIRTUAL_PATH)>` protects a specific path.
- **Hash formats**: bcrypt (`$2a$`/`$2b$`/`$2y$`, via `BCrypt.Net-Next`), Apache apr1 (`$apr1$`, implemented
  from the documented MD5-crypt algorithm), and SHA1 (`{SHA}`, built-in). Unknown formats are rejected.
- **Enforcement**: the existing `BasicAuthMiddleware` consults both sources. A request is authorized if it
  matches the label credential **or** any htpasswd entry for the route; a route with neither is open. Credentials
  are never logged.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: Basic Auth credentials may come from mounted htpasswd files (per host and per path), with bcrypt,
  apr1, and SHA1 hashes, in addition to label credentials.

## Impact
- **Dependency**: adds `BCrypt.Net-Next` (CPM) to `DockYarp.Security` — .NET has no built-in bcrypt.
- **Code**: `DockYarp.Security` (`SecurityHeadersOptions`, new `HtpasswdStore`, `HtpasswdVerifier`, `Apr1`,
  `BasicAuthMiddleware` refactor, DI registration).
- **Tests**: `DockYarp.Security.Tests` — apr1 against the Apache known-answer vector, SHA1 vector, bcrypt
  round-trip, unsupported-format rejection, store parsing (per-host / per-path via a mock filesystem), and
  middleware integration (htpasswd user passes, wrong fails, path-scoped).
- **Deferred**: dynamic reload of htpasswd files (loaded at startup for now) — a follow-up backlog item.
- **Owning agent**: AG-SEC. Resolves `add-htpasswd-files`.
