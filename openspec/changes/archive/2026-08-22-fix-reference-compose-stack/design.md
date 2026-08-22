## Context

See `proposal.md` and `openspec/backlog/items/fix-reference-compose-stack.md` for the live-investigated
findings (not re-derived here).

## Goals / Non-Goals

**Goals:**
- Make `docker-compose.yml`'s image reference actually resolve to a real, published image.
- Remove or explain every piece of config in the reference stack that currently does nothing.
- Let a reader who only opens this one file discover TLS/ACME and the dashboard exist, without those examples
  actively running (and failing) in a plain local demo.

**Non-Goals:**
- Deciding whether DockYarp ever publishes to Docker Hub — `add-registry-readme-sync` owns that decision;
  this change only makes examples match the registry the project *currently, actually* publishes to (GHCR).
- Wiring up a working `StaticConfig` demo in the reference stack — it would duplicate/conflict with the
  existing Docker-label-driven `whoami` demo in the same file; a pointer to the docs is enough here.

## Decisions

**The dead `./config:/config` mount is removed, not wired up with a working example.**

Rationale: the reference stack already demonstrates routing via Docker labels (`whoami`); adding a second,
parallel static-config-driven route in the same minimal file would be confusing (two config sources for one
demo) rather than illustrative. A short comment pointing at `StaticConfig:Path` in the docs gives a newcomer
the pointer without the complexity.

**The TLS/ACME example is added commented-out, not live.**

Rationale: the file's own existing header comment already states "real ACME/TLS needs public DNS" — the demo
domain (`whoami.local`) isn't publicly resolvable, so a live `LETSENCRYPT_HOST` on it would just retry and fail
forever on `docker compose up`, which is a worse first-run experience than not showing it at all. A clearly
marked commented block that a reader uncomments and points at their own real domain avoids that failure mode
while still answering "how do I turn HTTPS on" from this one file.

## Risks / Trade-offs

- [Risk] A commented-out example can silently drift from working syntax over time (never executed, so a typo
  wouldn't be caught by CI). → Accepted: the same risk already exists for every code block in the docs site;
  not a new category of risk this change introduces, and the labels shown (`LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`)
  are simple, stable, unlikely to change shape.
