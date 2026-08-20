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
- [ ] 3.2 Push the `account: user` fix and trigger another real `workflow_dispatch` run. Read the reported
      deletion list. Confirm: every stable release tag (`X.Y.Z`/`X.Y`/`X`/`latest`) and `edge` are absent from
      it; only hyphenated GitVersion edge-prerelease tags appear. If anything unexpected appears, stop and
      re-investigate — do not flip to real deletion until this list looks exactly right.
- [ ] 3.3 Spot-check that a retained tag (e.g. `edge`) still has both `linux/amd64` and `linux/arm64` manifests
      reported/intact per the action's own output or a manual `docker manifest inspect`, confirming the
      multi-arch-protection claim actually holds for this repo's real package shape, not just trusted from the
      changelog.

## 4. Go live — required, after explicit user confirmation (AG-DEP)

- [ ] 4.1 With the user's explicit go-ahead (having reviewed task 3's dry-run output), flip `dry-run` to
      `false` in `ghcr-cleanup.yml`.
- [ ] 4.2 Trigger one more real `workflow_dispatch` run with real deletion enabled, confirm it completes
      successfully and the deleted list matches what the dry-run previously reported.
- [ ] 4.3 Confirm a kept tag (`edge` or the latest stable release) still pulls successfully after the real run
      (`docker pull` against the real registry).

## 5. Final validation (AG-DEP)

- [ ] 5.1 `openspec validate add-ghcr-image-retention --strict` passes (already done at propose time; re-confirm
      unchanged).
