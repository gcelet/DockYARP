## 1. Site configuration reference (AG-DOC)
- [x] 1.1 `docs-site/content/en/docs/configuration.md`: lead note (label **or** env var; env wins) + complete the
      routing/TLS/access-control tables with every recognized key + a namespaced-alias note

## 2. In-repo reference (AG-DOC)
- [x] 2.1 `docs/labels-reference.md`: same env-or-label note + add the missing rows (`CERT_NAME`, `SSL_POLICY`,
      `SERVER_TOKENS`, `NETWORK_ACCESS`, `EXTERNAL_HTTPS_PORT`, `ENABLE_HTTP_ON_MISSING_CERT`,
      `TRUST_DEFAULT_CERT`, namespaced aliases)

## 3. Follow-ups (AG-DOC)
- [x] 3.1 Add backlog stubs `add-doc-capability-reference` (app-config + runtime, audit the site) and
      `add-doc-examples` (recipes)

## 4. Verify (AG-DOC)
- [x] 4.1 `openspec validate --strict` green; examples use real key names (no placeholders)
