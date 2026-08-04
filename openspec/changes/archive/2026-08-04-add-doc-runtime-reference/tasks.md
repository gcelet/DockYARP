## 1. Runtime features page (AG-DOC)
- [x] 1.1 `docs-site/content/en/docs/features.md`: sections for Discovery, Routing & load balancing, TLS/ACME,
      Observability (`/metrics` + access log), Admin API (the five `/api/*` endpoints + `X-Api-Key`), Static
      configuration (`StaticConfig:Path`), Custom error pages (`ErrorPages:Directory`), Graceful shutdown —
      behavior + a short example where useful, `weight: 3` (after Configuration; bumped architecture/deployment/
      contributing to 4/5/6)

## 2. Light audit / cross-links (AG-DOC)
- [x] 2.1 Cross-link Configuration ↔ Features; spot-checked getting-started/architecture/deployment — no stale
      shipped-feature claims to fix (they stay accurate; the reference gap was the runtime page, now added)

## 3. Verify (AG-DOC)
- [x] 3.1 `openspec validate --strict` green; endpoints/keys/field names match the code
