# GitHub rulesets (import into `gcelet/DockYARP`)

Server-side rules matching the GitFlow workflow. They enforce even against `git push --no-verify`
(unlike the local `.git/hooks/pre-push` guard, which is per-clone and bypassable).

## How to import
GitHub UI → **Settings → Rules → Rulesets → New ruleset ▾ → Import a ruleset** → upload one JSON file.
Repeat for each file (one ruleset per file).

## The branch model (GitFlow)
- **`develop`** — long-lived trunk; direct pushes allowed (the nightly evening-projected push). Default branch.
- **`main`** — releases only; created at the first release, updated via **pull request** with CI green.
- **`feature/*`**, **`release/*`**, **`hotfix/*`**, **`bugfix/*`** — short-lived workflow branches.
- Anything else (e.g. `develop-by-day`, ad-hoc names) is **rejected on the remote** by the naming ruleset.

## The files
| File | Ruleset | What it does |
|------|---------|--------------|
| `block-daytime-branches.json` | `*-by-day` | **Recommended for the daytime-branch block.** Targets only `*-by-day` and checks **Restrict creations** (+ updates) — a standard, clearly-visible rule. No bypass → `develop-by-day` can't be created/pushed on the remote. |
| `branch-naming.json` | Applies to **all** branches | ⚠️ **Org/Enterprise repos only.** Enforces GitFlow names via the **Restrict branch names** metadata rule. This rule is **NOT available on personal repos** (`gcelet/DockYARP`) — GitHub drops it on import, so the ruleset imports with no rules. **Don't use it here**; use `block-daytime-branches.json` instead. |
| `protect-main.json` | `main` | No deletion, no force-push, changes only via **PR** (0 approvals — solo), and CI must pass. |
| `protect-develop.json` | `develop` | No deletion, **linear history**, **no force-push** (anti-accident). Direct push allowed (no PR/CI gate). Requires the **incremental fast-forward** evening projection (`project_daytime_onto_develop.py`) — never a whole-branch rewrite. |

### Why `branch-naming.json` imports with no rules
Its rule is **Restrict branch names** (`branch_name_pattern`), a metadata rule available only on **organization /
Enterprise** repos. On a **personal** repo the ruleset UI has no such option, so GitHub silently drops the rule on
import and the ruleset ends up empty. Use **`block-daytime-branches.json`** (standard *Restrict creations* rule)
to block `*-by-day` — that is fully supported here. There is no way to positively enforce the full GitFlow naming
convention via rulesets on a personal repo; the local `pre-push` hook remains the convention guard for other names.

## ⚠️ The required status check name
`protect-main.json` requires a check named **`Build & test`** (`integration_id: 15368` = GitHub Actions) — this is
the job `name:` in `ci.yml`, which is the exact string GitHub reports the check under (NOT the job id `build-test`).
A required check must match the reported name **exactly**, or PRs to `main` wait forever for a check that never
reports. Safest path: since CI hasn't run yet, **import `protect-main.json` with "Require status checks to pass"
unchecked**, then add the check via the UI **after the first CI run** (the picker lists the real name — select
`Build & test`). `main` does not exist until the first release anyway, so nothing is blocked in the meantime.

## Notes
- **Linear history** is enforced on `main` and `develop` (`required_linear_history`) — the project prefers a linear
  history, so PR merges must be **squash or rebase**, not merge commits. Set it repo-wide too:
  Settings → General → Pull Requests → **uncheck "Allow merge commits"**, keep squash and/or rebase. (This is a
  deliberate deviation from classic GitFlow's `--no-ff` merges.)
- **Force-push is blocked** on `develop` and `main`. The evening history projection must therefore be an
  **incremental fast-forward** (append the new daytime commits, shifted into 20:00–23:59, onto `develop`'s tip) —
  never a whole-branch rewrite. Use `scratchpad/project_daytime_onto_develop.py`.
- `bypass_actors` are empty (full discipline). To give yourself an escape hatch, add
  `{ "actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always" }` (Repository admin) to a ruleset.
- Tags (`v*`) are untouched here; add a tag ruleset later if you want to protect release tags.
