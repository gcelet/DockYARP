---
id: e2e-proxy-protocol
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: ENABLE_PROXY_PROTOCOL (real client IP behind an L4 balancer)
provenance: 2026-08-10 e2e coverage audit — the one non-DCP-blocked runtime gap
---

## Why
`add-proxy-protocol` (archived) accepts a PROXY v1/v2 header on the edge listeners and sets the connection's remote
endpoint to the real client, feeding `X-Forwarded-For` / `X-Real-IP`. The **parser** and the **connection
middleware** are unit-tested, but the **live path** — a real PROXY header preceding a request through the running
proxy — is not proven end-to-end. This is the one runtime behavior in the shipped feature that unit/integration
tests cannot cover (it depends on the actual Kestrel edge listener wiring). See the e2e coverage audit in
`docs/testing.md`.

## Current state
- `ProxyProtocolParser` + `ProxyProtocolConnectionMiddleware` unit-tested; `Server:EnableProxyProtocol` wired in
  `KestrelTlsConfigurator` (applied before `UseHttps`). No e2e.

## Proposed change (sketch)
- Enable `Server:EnableProxyProtocol=true` on the e2e proxy (AppHost env for the DockYarp resource).
- A test opens a **raw TCP socket** to the proxy's **HTTP edge** (plaintext — simplest, no TLS over the PROXY
  header), sends a PROXY **v1** line (e.g. `PROXY TCP4 203.0.113.7 10.0.0.1 12345 8080\r\n`) followed by an
  HTTP/1.1 request to an echo host, and asserts the backend sees `203.0.113.7` in `X-Forwarded-For` / `X-Real-IP`.
- Optionally repeat with a PROXY **v2** (binary) header to cover both encodings.

## Acceptance criteria (→ scenarios)
- **WHEN** a PROXY v1 (and v2) header precedes an HTTP request to the edge **THEN** the backend receives the spoofed
  client IP in `X-Forwarded-For` / `X-Real-IP` (not the L4 balancer's address).

## Notes / risks / references
- **e2e verification item** → single commit, **no archive** (verifies an already-archived spec). Needs Docker.
- Test the HTTP edge (plaintext) to avoid layering the PROXY header under TLS. Refs: `ProxyProtocolParser`,
  `ProxyProtocolConnectionMiddleware`, `KestrelTlsConfigurator.ApplyProxyProtocol`, `Server:EnableProxyProtocol`.
- Update `docs/testing.md` (the e2e coverage map) when this lands.
