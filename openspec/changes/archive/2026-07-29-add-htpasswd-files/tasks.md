## 1. Dependency + config (AG-SEC/AG-DEP)
- [x] 1.1 `Directory.Packages.props`: add `BCrypt.Net-Next`; `DockYarp.Security.csproj`: reference it (no `Version=`)
- [x] 1.2 `SecurityHeadersOptions`: add `HtpasswdDirectory`

## 2. Hash verification (AG-SEC)
- [x] 2.1 `Apr1`: implement Apache MD5-crypt `Verify` (from the documented algorithm)
- [x] 2.2 `HtpasswdVerifier.Verify(password, hash)`: dispatch bcrypt / apr1 / `{SHA}`; reject unknown

## 3. Store + enforcement (AG-SEC)
- [x] 3.1 `HtpasswdStore`: load files under the directory at startup; `Find(host, pathPrefix)` prefers the
      per-path file (`<host>_<sha1hex(path)>`) then the per-host file (`<host>`)
- [x] 3.2 `BasicAuthMiddleware`: consult label credential + htpasswd entries (union); realm prefers the label realm
- [x] 3.3 Register `HtpasswdStore` (DI); never log credentials

## 4. Tests (AG-SEC)
- [x] 4.1 `Apr1`: Apache known-answer vector verifies; a wrong password fails
- [x] 4.2 `HtpasswdVerifier`: bcrypt round-trip; `{SHA}` vector; apr1 vector; unsupported format → false
- [x] 4.3 `HtpasswdStore`: host-file parsing (skip comments/blanks) and lookup (temp directory)
- [x] 4.4 `BasicAuthMiddleware`: htpasswd user passes; wrong credential 401; path-scoped file only protects its path

## 5. Verify (AG-SEC)
- [x] 5.1 Nuke `Test` gate green
