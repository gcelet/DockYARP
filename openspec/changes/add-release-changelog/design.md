## Context

See `proposal.md` - Why for the motivation, the GitFlow scope reconciliation, and the **release-please →
git-cliff tool pivot** (release-please cannot parse this repo's gitmoji-prefixed commits — confirmed against
[release-please#2385](https://github.com/googleapis/release-please/issues/2385), no config workaround). Relevant
current state:
- `GitVersion.yml` is `workflow: GitFlow/v1`; `develop` is the long-lived trunk, `main` does not exist yet
  (created only at the first release). Real `vX.Y.Z` tags are only expected once `main` exists, but this
  change's trigger (`push tags: v*`) does not itself gate on that — see Non-Goals.
- `image.yml` already publishes on `push tags: v*` — this change does not touch that trigger; it adds a
  parallel job reacting to the same trigger.
- `add-release-versioning` already stamps assemblies/image/`/api/version` from GitVersion on every build; that
  stays the build-time version source and is untouched here. Deciding/pushing the release tag stays manual.
- Commit convention (see `session-handoff` in project memory, not repo-visible) is gitmoji + Conventional type:
  `:sparkles: feat: …`, `:bug: fix: …`, `:card_file_box: chore: archive <id> into specs`.
- Existing workflows (`image.yml`, `ci.yml`) pin actions to major-version tags (`@v7`, `@v6`) and pass external
  values through `env:` rather than inline `${{ }}` interpolation in `run:` scripts, to avoid shell injection.

## Goals / Non-Goals

**Goals:**
- Generate a readable, correctly-categorized changelog from this repo's actual gitmoji-prefixed commit style,
  without renaming the convention.
- Create the GitHub Release automatically once a release tag is pushed — no manual changelog authoring.
- Keep the workflow file consistent with existing repo conventions (action pinning, `fetch-depth`, no shell
  injection from ref/PR data).

**Non-Goals:**
- Creating `main` or cutting `v0.1.0` (deferred — separate manual step, see proposal Scope note).
- Automating the version-bump/tag-cutting decision itself (that would need a semantic-release-style "what's the
  next version" tool on top of git-cliff, which does not decide versions). Deciding/pushing `vX.Y.Z` stays a
  manual, GitVersion-informed step, same as `image.yml` today.
- Persisting `CHANGELOG.md` back into the repo via a commit. The generated changelog lives in the GitHub Release
  notes only for v1 — writing it back to a tracked file would need a bot commit back to `main`/tag, which is
  extra CI-write complexity not asked for; can be added later if wanted.

## Decisions

- **Tool: `git-cliff`**, not `release-please` (pivoted during apply — see Context). Its `commit_parsers` are
  free-form regexes evaluated against the whole commit subject, so a leading gitmoji token is just part of the
  pattern to match, not something that breaks an anchored parse.
- **Trigger: `push tags: v*`** (same event `image.yml` already reacts to), not `push: branches: [main]`.
  git-cliff generates the changelog *for an already-created tag* — it does not maintain a running PR the way
  release-please did, so there is no "accumulate on push to main" phase to model. This also sidesteps needing
  `main` to exist for the workflow to be meaningful: it fires whenever a `v*` tag is pushed, exactly like the
  existing image publish.
- **`commit_parsers` regex set matches the Conventional type generically, with the gitmoji as a wildcard token
  — not a fixed gitmoji↔type table.** Checked the real commit history (`git log`) before writing this: the
  same type follows *different* gitmoji in practice (`:lock:` precedes `feat`/`fix`/`chore` depending on the
  commit; `:memo:` precedes both `docs` and a `doc` typo), so a per-gitmoji regex (`^:sparkles:\s*feat`) would
  silently miss real commits. Pattern shape instead: `^:\w+:\s*<type>(\(.*\))?!?:` — e.g.
  `^:\w+:\s*feat(\(.*\))?!?:` → group `"Features"`, `^:\w+:\s*fix(\(.*\))?!?:` → `"Fixes"`,
  `^:\w+:\s*perf(\(.*\))?!?:` → `"Performance"`, `^:\w+:\s*docs?(\(.*\))?!?:` → `"Documentation"` (the `s?`
  absorbs the `doc`/`docs` typo variance). A parser for `^:\w+:\s*chore:\s*archive\b` (checked **before** the
  general chore parser — git-cliff evaluates parsers in order, first match wins) is `skip = true` to drop
  archive commits entirely (not just hide them in a section — `skip` excludes them from git-cliff's changelog
  data model, stronger than release-please's `hidden` flag); the general `^:\w+:\s*chore(\(.*\))?!?:` (and
  `build`/`ci`/`test`/`style`) parsers are also skipped, since the DockYarp changelog should read as user-facing
  features/fixes, not repo bookkeeping. A trailing catch-all groups anything else under `"Other"` rather than
  silently dropping unrecognized commits.
- **Release creation: `softprops/action-gh-release`**, pinned to a major-version tag like the repo's other
  third-party actions, given `body_path` the git-cliff output for the tag, `tag_name: ${{ github.ref_name }}`.
- **git-cliff invocation: `orhun/git-cliff-action`** (the maintainer-published action), with `args: --latest`
  (changelog for the just-pushed tag against the previous one) and `config: cliff.toml`; checkout needs
  `fetch-depth: 0` (git-cliff walks full tag history, same reason GitVersion needs it).

## Risks / Trade-offs

- [Real `vX.Y.Z` tags are, per GitFlow convention, only expected once `main` exists] → the workflow does not
  itself enforce this (it just reacts to `push tags: v*`); acceptable, since nothing currently pushes a `v*`
  tag before a real release either. Not a hard gate, consistent with how `image.yml` already behaves.
- [git-cliff's `--latest` mode assumes the previous tag is the correct changelog boundary] → holds under this
  repo's linear tag history; would need reconsidering if a hotfix/backport tagging scheme were introduced later
  (out of scope now).
- [`chore`-skipped changelog drops legitimate repo-visible maintenance work] → acceptable: the target audience
  for a DockYarp release changelog is "what changed for someone running the proxy," and `git log` remains the
  full record.
- [Changelog not persisted to a tracked `CHANGELOG.md`] → acceptable per Non-Goals; the GitHub Release page is
  the durable record for v1.

## Migration Plan

No migration — purely additive CI config. Rollback is deleting the workflow/config files; nothing else depends
on them existing.
