## 1. Provider & middleware (AG-AA)

- [x] 1.1 Add `ErrorPagesOptions` (`Directory`) and `ErrorPageProvider` (loads `{code}.html` via `IFileSystem`)
- [x] 1.2 Add `ErrorPageMiddleware` writing the page for a not-started error response (≥ 400) with no body

## 2. Wiring (AG-AA)

- [x] 2.1 Register options/provider/middleware; add `UseMiddleware<ErrorPageMiddleware>()` after access logging

## 3. Tests & docs

- [x] 3.1 Provider tests (MockFileSystem): `{code}.html` loaded; missing directory → none
- [x] 3.2 Middleware tests: configured page written for a bodiless 404; no page → unchanged; started response → unchanged
- [x] 3.3 Document error pages in `docs/routing-model.md` (or admin-api)
- [x] 3.4 Build + full test suite green via the Nuke CLI
