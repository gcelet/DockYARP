## Context

ACME HTTP-01 (RFC 8555 §8.3) validates a domain by fetching `http://{domain}/.well-known/acme-challenge/{token}`
on **port 80**. In the Aspire cluster, step-ca is the ACME server; it must reach DockYarp's challenge
endpoint by the certificate host name, on port 80. But DCP only resolves *resource names*, and DockYarp is
non-root on 8080 (cannot bind 80). The previous attempt (`--network-alias` runtime arg) was rejected by DCP.

## Goals / Non-Goals

**Goals:** make step-ca resolve the TLS host names to DockYarp's HTTP-01 endpoint on port 80, so real
certificates are issued and the two remaining TLS scenarios pass — without modifying DockYarp or using
runtime args.

**Non-Goals:** changing DockYarp (stays non-root, ports unchanged); product changes; a general in-cluster
ACME solution beyond the test harness.

## Decisions

- **`socat` sidecar as the HTTP-01 front door.** A single `alpine/socat` container:
  - `WithContainerNetworkAlias("tls.local" | "hsts.local" | "mtls.local")` — native Aspire API that registers
    additional DNS names on the DCP bridge network, so step-ca resolves those names to this container.
  - `WithArgs("TCP-LISTEN:80,fork,reuseaddr", "TCP:dockyarp:8080")` — listens on port 80 and forwards each
    connection to DockYarp's HTTP endpoint (`dockyarp` resolves by resource name). socat runs as root
    inside its own container, so binding 80 is fine; DockYarp is untouched.
  - `WaitFor(proxy)` for deterministic ordering.
- **Aliases on the sidecar, not DockYarp.** Only step-ca uses these names (for HTTP-01); the tests connect
  to DockYarp's HTTPS endpoint directly (via a connect callback), so routing them to the sidecar is
  correct and conflict-free.
- **No runtime args, no sysctl.** Everything uses native APIs (`WithContainerNetworkAlias`, `WithArgs`), so we
  avoid the `WithContainerRuntimeArgs` path that DCP rejected for `--network-alias`.

## Risks / Trade-offs

- **`WithContainerNetworkAlias` under DCP** is the one unknown; it is the documented native API, so it should
  work where the raw arg did not. Confirmed by the next `E2E` run.
- socat is a raw TCP forward; ACME validation is a simple GET, so HTTP/1.1 forwarding is sufficient. The
  `Host` header (the cert name) is preserved, so DockYarp's challenge middleware serves the token.

## Migration Plan

Purely additive: one container in the test AppHost. No changes to DockYarp, other resources, or product code.

## Open Questions

- None blocking. If `WithContainerNetworkAlias` is unavailable/ineffective at runtime, reconsider (e.g. a DNS
  sidecar), but the native API is expected to work.
