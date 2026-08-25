## 1. Widen the cleanup with a dry-run safety step (AG-DEP)

- [x] 1.1 Change `tag-selection: tagged` to `both` and `dry-run: false` to `true` in
      `.github/workflows/ghcr-cleanup.yml`, with comments explaining why `both` is safe with `v3.1.0` and why
      `dry-run: true` is temporary. Verified YAML validity via `js-yaml`.
- [x] 1.2 Triggered `workflow_dispatch` (run 32898786364) and reviewed the real dry-run candidate list: 47
      candidates (3 aged-out tagged prereleases `0.1.0-alpha.320/.325/.316` + 44 orphaned untagged digests),
      none looked like a child of a currently-kept tag (`edge`, `latest`, release tags).
- [x] 1.3 Flipped `dry-run` back to `false`, with a comment recording the reviewed run's candidate summary.
