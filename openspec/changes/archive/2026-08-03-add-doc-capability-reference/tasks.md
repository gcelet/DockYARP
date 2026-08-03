## 1. Application-configuration reference (AG-DOC)
- [x] 1.1 `docs-site/content/en/docs/configuration.md`: replace the "Application configuration" summary with a
      per-section reference (`Server`, `Docker`, `Tls`, `Security`, `Routing`, `Proxy`, `AccessLog`, `AdminApi`,
      `Compression`, `DataProtection`, `Host`), each key with default + purpose (verified from the options types)
- [x] 1.2 Note that any key may be set via `appsettings.json` or a `Section__Key` environment variable

## 2. Follow-up (AG-DOC)
- [x] 2.1 Add backlog stub `add-doc-runtime-reference` (site audit vs specs + runtime-feature narrative)

## 3. Verify (AG-DOC)
- [x] 3.1 `openspec validate --strict` green; defaults match the options types
