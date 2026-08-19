## 1. Narrow the concurrency group (AG-DEP)

- [x] 1.1 `.github/workflows/image.yml`: change `concurrency.group` from `image-${{ github.ref }}` to
      `image-${{ github.ref }}-${{ github.event_name }}`, keeping `cancel-in-progress: false` unchanged.

## 2. Share the NuGet restore cache (AG-DEP)

- [x] 2.1 `.github/workflows/image.yml`: add an `actions/cache@v6` step to `publish-release`, right after
      "Set up .NET (for the Nuke build)" and before "Set up QEMU" — same `path`/`key`/`restore-keys` as
      `ci.yml`'s existing cache step, verbatim, so the cache entry is shared across workflows.
- [x] 2.2 `.github/workflows/image.yml`: add the identical cache step to `publish-edge`, same placement.

## 3. Local sanity check (AG-DEP)

- [x] 3.1 Validated both workflow files parse as syntactically correct YAML (`npx js-yaml`, matching the
      validation approach already used for `image.yml` in `fix-e2e-ci-runner-timeout`) — a syntax check only,
      not a behavioral validation.

## 4. Real CI validation — required (AG-DEP)

- [x] 4.1 Pushed (commit visible as run `32301395537` for `ci.yml`, `32301395509` for `image.yml`). Both
      workflows ran and succeeded: `CI` in 1m24s, `Publish image` (edge) in 14m41s (the `release` job correctly
      skipped, 0s, since this was a branch push not a tag). The cache addition doesn't break either workflow.
- [ ] 4.2 **Deferred, not done** — confirm a `workflow_dispatch` on `image.yml` no longer queues behind a
      concurrent `push`-triggered run (the concrete symptom that motivated this change). Not exercised this
      session; user will validate later with a real concurrent trigger.
- [x] 4.3 Confirmed directly from run `32301395537`/`32301395509`'s own logs (no second push needed): both
      jobs, running in parallel, restored from the **exact same** cache key
      (`nuget-Linux-2efbb5d79418ffda6934d51637d769b111cbc13ea765e682adcd73fa3ff87ea1`) — `Cache restored from
      key: ...` in both `ci.yml`'s and `image.yml`'s "Cache NuGet packages" step, proving the cache is actually
      shared across workflows, not just present in each independently.
