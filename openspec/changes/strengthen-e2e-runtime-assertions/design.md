## Context
The Aspire e2e suite (`tests/DockYarp.E2E.Tests`) boots DockYarp as a real container in front of labeled
backends. Two behaviors need the real Kestrel front-end: the `Server` header is only added by real Kestrel
(never by the in-process `TestServer`), and the 308 redirect is worth confirming nothing rewrites it on the
real path.

## Goals / Non-Goals
- **Goal**: assert the two runtime-only behaviors inside the e2e scenarios that already exercise them.
- **Non-Goal**: new e2e scenarios, or e2e for behaviors already covered in-process (`Proxy` strip, raw-IPv4).

## Decisions
- Integrate the assertions into existing scenarios — not a synthetic bundle — so each assertion stays tied to
  a real configuration flow (a discovered backend response; an HTTP→HTTPS redirect).
- `Server` absence goes on the discovery response; the 308 on the existing redirect scenario.

## Risks / Trade-offs
- Runs only under the opt-in `E2E` target (Docker). Compile-validated now; behavior validated at the next
  Docker run.

## Migration Plan
- None (tests only).

## Open Questions
- None.
