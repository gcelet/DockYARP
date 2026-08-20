## 1. Workflow file (AG-DEP)

- [x] 1.1 `.github/workflows/ghcr-cleanup.yml` (new): `on.schedule` weekly `cron` (`0 3 * * 0`, Sunday 03:00
      UTC) + `workflow_dispatch`. `permissions: packages: write` for the job itself.
- [x] 1.2 Single job, `snok/container-retention-policy@v3.1.0` step: `account:
      ${{ github.repository_owner }}`, `image-names: dockyarp` (the lowercased repo-name portion of
      `image.yml`'s own resolved `${repository,,}` — no `IMAGE_REPOSITORY` repo variable is set, confirmed via
      `gh variable list`, so the default `github.repository` path applies; **not independently confirmed
      against the real GHCR package listing** since the current `gh` auth lacks `read:packages` — flagged for
      task 3's dry-run to catch immediately if wrong, since a wrong package name would show zero candidates or
      an error), `image-tags: "*-*"`, `tag-selection: tagged`, `cut-off: 3d`,
      `token: ${{ secrets.GHCR_CLEANUP_TOKEN }}` (placeholder name — confirm against what the user actually
      provisions in task 2, rename here if different), `dry-run: true`.
- [x] 1.3 Validated YAML syntax (`npx js-yaml`) — OK. `actionlint` not available locally (same as
      `fix-e2e-ci-runner-timeout`'s precedent).

## 2. Required secret — user action, blocking (AG-DEP)

- [x] 2.1 **Paused, asked the user to provision a repository secret.** Corrected mid-conversation, not assumed:
      the user directly asked whether `delete:packages` alone would suffice, and it does not — GitHub's own REST
      API docs confirm listing package versions needs `read:packages`, and deleting needs **both**
      `read:packages` and `delete:packages` together. Told the user to create a classic PAT with both scopes.
      Waiting on confirmation the secret exists and its exact name before task 3 can run for real.

## 3. Real dry-run validation — required (AG-DEP)

- [x] 3.1 Secret confirmed present (`gh secret list` shows `GHCR_CLEANUP_TOKEN`), pushed, triggered a real
      `workflow_dispatch` run — https://github.com/gcelet/DockYARP/actions/runs/32414866147. **Failed**:
      `Failed to fetch packages: ... 404 ... list-packages-for-an-organization`. Root cause: `account` was set
      to `${{ github.repository_owner }}` (`gcelet`), triggering the org-scoped endpoint — `gcelet` is a
      personal account, not an org. Fixed: `account: user` (the literal string the action documents for this
      exact case).
- [x] 3.2 Pushed the `account: user` fix and triggered another real `workflow_dispatch` run —
      https://github.com/gcelet/DockYARP/actions/runs/32415763262. **Succeeded**: "Found 1 package(s) for the
      user", "Selected 2 tagged and 0 untagged package versions for deletion". Reported deletion list:
      `dockyarp:0.1.0-alpha.286` and `dockyarp:0.1.0-alpha.284` — exactly the expected shape (hyphenated
      GitVersion edge-prerelease tags). No stable release tag and no `edge` appear in the list.
- [x] 3.3 Deferred to task 4.3 (after a real, non-dry-run deletion) — a dry-run doesn't touch anything, so
      "still pullable after pruning" can't be meaningfully observed until something has actually been deleted.

## 3b. Investigating an apparent discrepancy — resolved, not a bug (AG-DEP)

- [x] 3b.1 User inspected the real GHCR package UI (screenshot) and flagged that the dry-run's 2-candidate list
      looked incomplete against a package showing "8 tagged, 32 untagged" — reasonable to question given only
      the top of a scrollable list was visible. Investigated for real rather than dismissed: bumped
      `rust-log: container_retention_policy=debug` temporarily (still `dry-run: true`, zero risk), pushed,
      re-ran (https://github.com/gcelet/DockYARP/actions/runs/32416211579), then pulled the true raw job log via
      `gh api .../logs --allow-escape-sequences` (not just `gh run view --log`, to rule out any CLI-side
      truncation).
- [x] 3b.2 Confirmed from the action's own debug trace: `Found 40 package versions for package` (exactly
      matches the screenshot's 8+32=40 — no pagination bug), `Filtered out 38/40 package versions`,
      `Selected 2 package versions`. The two selected (`0.1.0-alpha.286`, `0.1.0-alpha.284`) are the only tagged
      versions matching both filters (hyphenated tag shape AND older than the 3-day cut-off); the other visible
      tagged versions in the screenshot (`0.1.0-alpha.302`+`edge`, `0.1.0-alpha.301`, `0.0.1-timeout-fix-test`)
      were all published ~23-24h ago — correctly excluded by `cut-off: 3d`, not evidence of a bug. The
      screenshot was a partial/scrolled view, not the full list. **No fix needed** — reverted the temporary
      debug logging back to the default.
- [x] 3b.3 User's actual question clarified: not doubting alpha.286/284's existence, but why the *visible*
      screenshot entries weren't selected — confirmed it's the 3-day cut-off. Pushed the revert, re-ran
      (https://github.com/gcelet/DockYARP/actions/runs/32416645294) — identical result
      (`alpha.286`/`alpha.284` only), confirming stability across three independent runs.

## 4. Go live — required, after explicit user confirmation (AG-DEP)

- [x] 4.1 User gave explicit go-ahead ("oui on passe au réel") after reviewing three consistent dry-run
      results plus the debug-trace investigation. Flipped `dry-run` to `false` in `ghcr-cleanup.yml`.
- [ ] 4.2 Trigger one more real `workflow_dispatch` run with real deletion enabled, confirm it completes
      successfully and the deleted list matches what the dry-run previously reported
      (`0.1.0-alpha.286`, `0.1.0-alpha.284`).
- [ ] 4.3 Confirm a kept tag (`edge` or the latest stable release) still pulls successfully after the real run
      (`docker pull` against the real registry).

## 5. Final validation (AG-DEP)

- [ ] 5.1 `openspec validate add-ghcr-image-retention --strict` passes (already done at propose time; re-confirm
      unchanged).
