## 1. git-cliff config (AG-DEP)

- [x] 1.1 Add `cliff.toml` at repo root with `commit_parsers` regexes matching this repo's gitmoji-prefixed
      Conventional Commits (`feat`/`fix`/`perf`/`docs` → visible sections; `chore` incl. archive commits →
      `skip = true`) per design.md Decisions. Matched against real `git log` output, not an invented
      gitmoji↔type table (see design.md's catch of the `:lock:`/`:memo:` counter-examples).
- [x] 1.2 Configure `[git] tag_pattern`/filters so git-cliff only considers `v*` tags (matches `image.yml`'s
      trigger) and `[changelog] body` template renders sections in a readable order. Used the official
      git-cliff default `[changelog] body` template verbatim (verified via the upstream repo) rather than
      inventing untested Tera syntax; only `[git]` settings and `commit_parsers` are DockYarp-specific.

## 2. Workflow (AG-DEP)

- [x] 2.1 Add `.github/workflows/release.yml`: trigger `on: push: tags: ['v*']`; checkout with
      `fetch-depth: 0` (git-cliff needs full tag history, same reason GitVersion needs it). Dropped a
      `workflow_dispatch` input considered during implementation — checked-out ref vs. an overridden tag input
      would disagree, a real bug, and it wasn't in scope; kept to the tag-push trigger only.
- [x] 2.2 Run `orhun/git-cliff-action@v4` (pinned, verified as the current major) with
      `args: -vv --latest --strip header` and `config: cliff.toml` to produce the changelog for the pushed tag.
- [x] 2.3 Create/update the GitHub Release for the tag via `softprops/action-gh-release@v3` (pinned, verified
      as the current major), using the git-cliff action's `content` output directly as `body` (simpler than
      writing an intermediate file for `body_path`) and `tag_name: ${{ github.ref_name }}`.
- [x] 2.4 Grant only the permissions needed (`contents: write`) — no broader scope than `image.yml`'s
      `packages: write` precedent.
- [x] 2.5 No changes needed to `image.yml`'s `push: tags: ['v*']` trigger — both workflows react to the same
      tag push independently.

## 3. Validation (AG-DEP)

- [x] 3.1 Validate workflow YAML structure (no `actionlint`/`git-cliff` binary on this machine per prior
      sessions — parsed `release.yml` with `yaml.safe_load` via `uvx --with pyyaml` and `cliff.toml` with
      `tomli` via `uvx --with tomli`; both parse cleanly).
- [x] 3.2 Dry-run the commit-parsing assumption for real: reimplemented `cliff.toml`'s `commit_parsers`
      first-match logic in Python and ran it over the **entire** `git log` history (254 commits, all 254
      classified — no `UNMATCHED`). Feature/fix/perf/docs/refactor commits land in their sections; every
      `chore` (including all `chore: archive … into specs` commits) and `build`/`ci`/`test`/`style` is skipped.
      Confirms the design.md correction (matching on Conventional type with the gitmoji as a wildcard, not a
      fixed gitmoji↔type table) actually holds against real data, not just the samples checked earlier.
- [x] 3.3 Run `npx @fission-ai/openspec@latest validate add-release-changelog --strict`.

## 4. Docs (AG-DEP / AG-DOC)

- [x] 4.1 `docs-site/content/en/docs/contributing.md` exists — added a **Releases** section noting the manual
      GitVersion-informed tag push, git-cliff-generated changelog, and that `main` doesn't exist until the
      first release.
