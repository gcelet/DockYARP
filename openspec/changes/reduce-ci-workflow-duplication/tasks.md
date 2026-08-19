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

- [ ] 4.1 Push and observe an ordinary `develop` push: confirm `ci.yml` and `image.yml`'s `publish-edge` both
      still run and both still succeed (the cache addition doesn't break either workflow).
- [ ] 4.2 Trigger a `workflow_dispatch` on `image.yml` for `develop` while a `push`-triggered run is still
      in-flight (or immediately after one lands) — confirm the dispatch no longer queues behind the push-
      triggered run's concurrency group (this is the concrete symptom that motivated this change).
- [ ] 4.3 On a second push (or re-run) after task 4.1's cache-populating run, confirm the cache step reports a
      hit (`Cache restored from key: ...` in the step log) in both `ci.yml` and `image.yml` — proving the cache
      is actually shared across workflows, not just present.
