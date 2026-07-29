# Design — add-htpasswd-files

## Sources and precedence
`BasicAuthMiddleware` gathers a route's protection from two sources:
1. the label credential (`route.Auth`, unchanged), and
2. htpasswd entries for the route from `HtpasswdStore`.

A request is authorized if it satisfies **either** source (union): the presented `user:password` matches the
label credential (constant-time), or it verifies against an htpasswd entry for `user`. A route with neither is
open (unchanged). This is the least surprising "complement labels" behavior and lets multiple htpasswd users all
work. The challenge realm prefers the label realm, else `DockYarp`.

## File layout and lookup
`HtpasswdStore` loads every file under `Security:HtpasswdDirectory` at startup (via `IFileSystem`, so it is unit
testable), keyed by file name. For a route with host `H` and path prefix `P`:
- if `P` is a non-root path and a file `H_<sha1hex(P)>` exists, it governs (most specific);
- else the file `H` (whole-vhost) governs, if present;
- else the route has no htpasswd protection.

`sha1hex(P)` is the lowercase hex SHA-1 of the path prefix, matching nginx-proxy's per-path file-naming scheme.

## Hash verification (`HtpasswdVerifier`)
Dispatch on the stored hash prefix:
- `$2a$` / `$2b$` / `$2y$` → `BCrypt.Net.BCrypt.Verify` (a malformed hash throws `SaltParseException`, caught → false).
- `$apr1$` → `Apr1.Verify` (below).
- `{SHA}` → constant-time compare Base64(SHA1(password)) to the stored digest.
- anything else → false (unsupported; logged once at load, never the credential).

### apr1 (`Apr1`)
Apache's MD5-crypt variant, implemented from the documented algorithm (magic `$apr1$`, 1000-iteration MD5 mix,
custom Base64 `to64` interleave). `Verify` extracts the salt from the stored hash, recomputes, and compares
constant-time. Correctness is pinned by the Apache documentation known-answer vector
(`myPassword` + salt `r31.....` → `$apr1$r31.....$HqJZimcKQFAMYayBlzkrA/`). No hashing library exists for apr1;
this is the only hand-written crypto and it is verify-only.

## Security notes
- Credentials and hashes are never logged.
- SHA1 and apr1 are weak by modern standards but required for htpasswd parity; bcrypt is the recommended format.
- Verification runs only when a route is htpasswd-protected; the per-request path is otherwise untouched.

## Out of scope (deferred)
- **Dynamic reload**: files are read at startup; changing them needs a restart (follow-up backlog item).
- Wildcard-host htpasswd files rely on the file being named exactly as the `VIRTUAL_HOST` pattern.
