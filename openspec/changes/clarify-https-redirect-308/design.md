## Context
`HttpsRedirectionMiddleware` (`src/DockYarp.Security/HttpsRedirectionMiddleware.cs`) already issues
`Response.Redirect(target, permanent: true, preserveMethod: true)`, which is a **308**. That preserves the
method and body for every request, including non-GET.

## Goals / Non-Goals
- **Goal**: make the 308/method-preserving behavior explicit and test-locked; close the `NON_GET_REDIRECT`
  backlog item as already covered.
- **Non-Goal**: a configurable redirect status. 308 is method-preserving for all verbs and permanent for GET;
  a knob adds surface without value.

## Decisions
- Keep the existing behavior; add a regression test that a POST is redirected with 308.
- State the 308 guarantee in the `security` spec and in `docs/security-middleware.md`, noting the deliberate
  divergence from nginx-proxy's default (`301` + `NON_GET_REDIRECT`).

## Risks / Trade-offs
- None (no behavior change). Divergence from nginx-proxy is documented and strictly safer.

## Migration Plan
- None.

## Open Questions
- None.
