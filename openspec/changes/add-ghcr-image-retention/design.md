## Context

See `proposal.md` — Why. Confirmed action inputs (fetched from the action's own README, not guessed):

- `account` (required): org/user — `gcelet` for this repo, resolved dynamically from `github.repository_owner`
  rather than hardcoded, matching the existing dynamic-resolution pattern in `image.yml`'s "Resolve target" step.
- `token` (required): needs **both** `read:packages` (to list versions at all) **and** `delete:packages` (to
  delete them) — confirmed against GitHub's own REST API docs, not the action's summarized README alone; the
  default `GITHUB_TOKEN` does not carry delete rights for packages, only write. **A user-provisioned secret,
  not something this change can create itself.**
- `image-names` (required): the package name — `dockyarp` (lowercased repository name, matching
  `image.yml`'s own `${repository,,}` lowercasing).
- `image-tags` (optional, glob `*`/`!`): the tag filter — `"*-*"` (see Decisions).
- `cut-off` (required): minimum age before a matched tag becomes eligible, e.g. `3d`.
- `tag-selection` (optional, default `both`): whether to consider tagged, untagged, or both versions.
- `dry-run` (optional, default `false`).
- Multi-arch protection is automatic since `v3.1.0` — no separate configuration needed, confirmed from the
  action's own documentation.

## Goals / Non-Goals

**Goals:**
- Only ever delete GitVersion-resolved edge-history prerelease tags, never a release tag or `edge` itself,
  using a filter that doesn't need to enumerate or maintain a keep-list.
- Never break a retained tag's multi-arch pull.

**Non-Goals:**
- Provisioning the required token/secret — a user action (creating a PAT with `packages:delete`, adding it as
  a repository secret), explicitly called out as a blocking prerequisite in `tasks.md`, not something implied
  to happen automatically.
- Cleaning up untagged/dangling manifests — scoped to `tag-selection: tagged` deliberately (see Decisions);
  broader untagged cleanup is a different, unrequested concern.
- Any change to what `image.yml` tags or publishes — this change only prunes after the fact.

## Decisions

**`image-tags: "*-*"` (a positive include filter for "contains a hyphen"), not an enumerated exclusion list
like `!latest !edge !1 !1.* `.**

Rationale: every DockYarp release tag shape (`X`, `X.Y`, `X.Y.Z`, `latest`) and `edge` itself never contain a
hyphen (confirmed against `image.yml`'s actual tag-generation logic — `version="${IN_TAG:-${REF_NAME#v}}"`
strips the `v` prefix, producing bare `1.2.3`-shaped tags, and the rolling/latest tags are plain words). Every
GitVersion-resolved edge prerelease always does (semver's own `-` prerelease separator, e.g.
`0.1.0-alpha.223`). A positive filter targeting the one shape that should ever be deleted is more robust than
an exclusion list: it doesn't need updating if the release tag scheme changes, and it can't accidentally
protect (or sweep) a tag whose shape wasn't anticipated when the exclusion list was written. Considered and
rejected: enumerating `!latest !edge` plus a version-matching exclusion — more fragile, more to maintain, and
solves the same problem less directly.

**`tag-selection: tagged` (not the default `both`).**

Rationale: scope this change to exactly the stated problem (accumulating *tagged* edge-history) without also
reaching into untagged/dangling manifest cleanup, which is a different concern the user didn't raise and which
interacts with multi-arch child manifests in ways worth reasoning about separately if it's ever wanted.

**`cut-off: 3d`.**

Rationale: a few days of buffer after a push before its edge-prerelease tag becomes eligible — avoids any risk
of deleting a tag while a manifest is still propagating or someone is mid-investigation of a very recent edge
build, while still being short enough that the package doesn't meaningfully re-accumulate between weekly runs.
Not a precisely-derived number; a reasonable, easily-adjusted default.

**Weekly schedule (`cron`), Sunday early UTC.**

Rationale: matches the low urgency of this item and DockYarp's own evening-only commit cadence
([[git-history-evening-hours]]-equivalent project convention) — no need for a tighter cadence; the package
only meaningfully grows on active development days.

## Risks / Trade-offs

- [Risk] The required `packages:delete` token doesn't exist yet — this change cannot validate end-to-end
  (a real deletion) without the user first creating and adding the secret. → Mitigation: `dry-run: true`
  first, validated for real without deleting anything, is the achievable validation without the secret; a
  second real run with `dry-run: false` happens once the user has provisioned the token, on their own timeline.
- [Risk] A third-party action doing real, irreversible deletions in this repository's registry. → Mitigation:
  `dry-run: true` for the first real validation run (see above) — nothing is deleted until the user has seen
  a dry-run's reported deletion list and is comfortable with it.
- [Risk] `snok/container-retention-policy`'s multi-arch protection is a relatively recent feature (`v3.1.0`,
  2026-05-29) — worth confirming it actually holds for this repo's specific multi-arch shape during the
  dry-run validation, not just trusting the changelog claim. → Mitigation: dry-run's reported deletion list is
  inspected manually before ever running for real; task added to explicitly check no release/edge tag or its
  children appear in that list.
