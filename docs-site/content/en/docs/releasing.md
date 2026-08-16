---
title: Releasing
weight: 8
description: How to cut a DockYARP release, from the first-release bootstrap to the tag push.
---

Cutting a release is a small number of manual, [GitVersion](https://gitversion.net/)-informed steps. Once the
tag is pushed, everything else — changelog, GitHub Release, image publish — runs automatically.

## First release only

DockYARP develops [GitFlow](https://gitversion.net/docs/learn/branching-strategies/gitflow)-style: pre-1.0,
work happens directly on `develop`, and `main` does not exist yet. `main` is created **once**, at the first
release:

```bash
git checkout develop
git pull
git checkout -b main
git push -u origin main
```

From then on, `main` exists permanently and this step is never repeated.

## Every release

1. **Check the version GitVersion would compute** before tagging anything:

   ```bash
   dotnet gitversion
   ```

   (or read it off the version stamped by the last CI/build run). This is the `X.Y.Z` you are about to tag —
   GitVersion derives it from the base version, commit height, and branch, so there is nothing to pick by hand.

2. **Push the release tag:**

   ```bash
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

That's the entire manual part.

## What happens automatically

Pushing a `vX.Y.Z` tag triggers two independent GitHub Actions workflows:

- [`release.yml`]({{< repo-file ".github/workflows/release.yml" >}}) builds a changelog from the commits since
  the previous release (grouped by type — Features, Fixes, …) with [git-cliff](https://git-cliff.org/), and
  publishes it as the GitHub Release notes for the tag.
- [`image.yml`]({{< repo-file ".github/workflows/image.yml" >}}) builds and publishes the Docker image for that
  version.

Neither workflow needs to be run by hand — pushing the tag is the only trigger.

## Worked example (illustrative)

No release has shipped yet, so this is a walkthrough with placeholder values rather than a real one:

```bash
$ dotnet gitversion
{ "MajorMinorPatch": "0.1.0", "PreReleaseTag": "" }

$ git tag v0.1.0
$ git push origin v0.1.0
```

A few minutes later, `v0.1.0` has a GitHub Release with a changelog grouped by Features / Fixes / …, and the
`ghcr.io/<repository>:0.1.0` image (also tagged `:latest`) is published.
