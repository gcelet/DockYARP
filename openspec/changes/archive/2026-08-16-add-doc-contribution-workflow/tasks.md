## 1. Submitting a contribution section (AG-DOC)

- [x] 1.1 Add a "Submitting a contribution" section to `contributing.md`, placed right after "Change lifecycle".
- [x] 1.2 Cover: fork the repository, branch off the trunk, run the same lifecycle locally (pointing back to
      "Change lifecycle" rather than repeating its steps).
- [x] 1.3 Link to `AGENTS.md`'s commit convention (via `{{< repo-file >}}`, matching the page's existing
      repo-doc link pattern) rather than restating the gitmoji/Conventional Commits format.
- [x] 1.4 State opening a pull request against the trunk as the final step.
- [x] 1.5 State the branch model in one sentence: `develop` is the trunk pre-1.0, `main` is reserved for
      releases (created at the first one) — no explanation of why, just the fact.

## 2. Validation (AG-DEP / AG-DOC)

- [x] 2.1 Built the docs site locally (`./build.ps1 Docs`) — succeeded, no Hugo errors; verified in the built
      output that the `AGENTS.md` `{{< repo-file >}}` link resolves to
      `https://github.com/gcelet/DockYARP/blob/develop/AGENTS.md`.
- [x] 2.2 Run `npx @fission-ai/openspec@latest validate add-doc-contribution-workflow --strict`.
