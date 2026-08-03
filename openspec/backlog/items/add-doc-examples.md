---
id: add-doc-examples
capability: documentation
agent: AG-DOC
tier: C-doc
priority: low
status: backlog
nginx-proxy: (internal — worked examples / recipes)
provenance: 2026-08-03 (split from add-doc-feature-reference)
---

## Why
Users learn a reverse proxy fastest from **worked, copy-pasteable examples**. The reference documents *what*
each key does; the site still needs task-oriented recipes showing *how* to achieve common scenarios end to end.

## Scope
Recipe pages on the site, each with a real Compose/`docker run` snippet (real labels or env vars) and the
expected behavior:
- basic virtual host; multi-host; path routing + `VIRTUAL_DEST` rewrite; multi-port (`VIRTUAL_HOST_MULTIPORTS`);
- env-var-only configuration (`-e VIRTUAL_HOST=…`); Let's Encrypt TLS; provided cert + `CERT_NAME`;
- mutual TLS (`DOCKYARP_CLIENT_CERT`); per-vhost `SSL_POLICY`; Basic Auth; `NETWORK_ACCESS=internal`;
- behind a load balancer / non-standard published port (`EXTERNAL_HTTPS_PORT`, `Proxy:TrustDownstreamProxy`).

## Acceptance criteria (→ scenarios)
- **WHEN** a user wants scenario X **THEN** a copy-pasteable example exists using real labels/env vars, with the
  expected result described.

## Notes / risks / references
- Follows [[add-doc-feature-reference]] + [[add-doc-capability-reference]]. Keep examples in sync with the
  shipped behavior (they double as living documentation of the feature set).
