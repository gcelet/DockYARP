## 1. Widen the cleanup with a dry-run safety step (AG-DEP)

- [x] 1.1 Change `tag-selection: tagged` to `both` and `dry-run: false` to `true` in
      `.github/workflows/ghcr-cleanup.yml`, with comments explaining why `both` is safe with `v3.1.0` and why
      `dry-run: true` is temporary. Verified YAML validity via `js-yaml`.
- [ ] 1.2 Once committed and pushed, trigger `workflow_dispatch` and review the real dry-run candidate list —
      confirm no digest still referenced by a kept tag appears.
- [ ] 1.3 Follow-up commit: flip `dry-run` back to `false` once the candidate list is reviewed and approved.
