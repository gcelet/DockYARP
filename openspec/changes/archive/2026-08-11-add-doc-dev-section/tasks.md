## 1. Enrich the Contributing page (AG-DOC)
- [x] 1.1 Add a **Testing** section to `contributing.md`: the pyramid (unit / integration / e2e, when each applies) + link the coverage map `docs/testing.md` via `{{< repo-file … >}}`
- [x] 1.2 List the Nuke targets: `./build.ps1 Test` (unit+integration), `E2E` (Docker), `Docs` (site)
- [x] 1.3 Add an **Architecture** pointer → `docs/architecture.md` (via the `repo-file` shortcode)
- [x] 1.4 All in-repo links use the `repo-file` shortcode (centralized `github_branch`), no hardcoded branch

## 2. Verify (AG-DOC)
- [x] 2.1 Site builds; the new links resolve to the configured branch (`blob/develop/docs/testing.md` + `architecture.md`)
