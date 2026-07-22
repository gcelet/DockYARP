## Why

DockYarp serves HTTPS with Kestrel defaults and a single, global HSTS policy, and it still provisions and
would serve HTTPS for hosts that asked for `nohttps`. nginx-proxy lets operators harden TLS (minimum
protocol version, cipher suites, HTTP protocols), tune HSTS per host, and genuinely keep HTTPS off a vhost.

## What Changes

- **TLS hardening options** (`TlsOptions`): minimum TLS version (default TLS 1.2), HTTP protocols (default
  HTTP/1.1+2), and an optional cipher-suite allow-list (applied on Linux/macOS; skipped where the platform
  manages ciphers). Wired into Kestrel via a pure, testable helper.
- **`nohttps` completion** (deferred from `add-https-methods`): a `nohttps` host is excluded from ACME
  provisioning, and an HTTPS request to it is refused (404).
- **HSTS**: a global `preload` flag and a **per-host HSTS** override (`HSTS` label; `off` disables it for
  the host), applied by the (now route-aware) security-headers middleware.

## Capabilities

### Modified Capabilities
- `tls-acme`: configurable TLS protocol/cipher hardening; `nohttps` hosts are not provisioned.
- `docker-discovery`: an `HSTS` label sets a per-host HSTS policy.
- `proxy-routing`: per-host TLS metadata carries an HSTS policy.
- `security`: HSTS supports preload and per-host override; HTTPS is refused for `nohttps` hosts.

## Impact

- **Code**: `src/DockYarp.Tls` (`TlsOptions`, `TlsHardening`, `KestrelTlsConfigurator`, `TlsDomains`),
  `src/DockYarp.Core` (`HostTlsMetadata`), `src/DockYarp.Docker` (`HSTS` label), `src/DockYarp.Security`
  (headers + HTTPS refusal).
- **Lower test confidence**: cipher suites, HTTP/2-3, and protocol behavior are wired as configuration but
  validated only at runtime (defaults preserve current behavior). OCSP stapling and per-socket protocol
  refusal remain out of scope.
- **Owning agent**: AG-AT / AG-SEC / AG-DD.
