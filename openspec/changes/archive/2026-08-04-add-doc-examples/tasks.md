## 1. Examples page (AG-DOC)
- [x] 1.1 `docs-site/content/en/docs/examples.md`: base stack shown once, then recipes (basic vhost, path
      routing + rewrite, multiple ports, env-var config, automatic HTTPS, mutual TLS, per-host TLS policy,
      Basic Auth, internal-only, behind a load balancer) — each a real Compose snippet + expected result
- [x] 1.2 Place after Features (`weight: 4`); bumped architecture/deployment/contributing to 5/6/7

## 2. Cross-links (AG-DOC)
- [x] 2.1 Getting Started "Next steps" links to Features + Examples; Configuration links to Features

## 3. Verify (AG-DOC)
- [x] 3.1 `openspec validate --strict` green; recipes use real keys (no placeholders) and match the shipped behavior
