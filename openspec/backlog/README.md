<!-- This directory is NOT an OpenSpec artifact. The OpenSpec CLI only scans `openspec/changes/` and
     `openspec/specs/`; it never reads `openspec/backlog/`. Do NOT add a `.openspec.yaml` here. -->

# DockYarp backlog

A lightweight, in-repo staging area for planned work — a substitute for a GitHub issue tracker while the
project is not yet pushed. It is **OpenSpec-adjacent**: every item is pre-shaped so it converts one-to-one
into an OpenSpec change via `/opsx:propose <id>`.

- **[`parity.md`](parity.md)** — the **source of truth** nginx-proxy ↔ DockYarp feature matrix. Every gap
  (`⚠️`/`⛔`) links to an item below. `docs/architecture.md` shows only a short summary and links here.
- **[`items/`](items/)** — one file per gap. Each file is a "proposal-lite" (its front-matter `id` **is** the
  future change id): `Why`, the real nginx-proxy behavior, DockYarp's current state, an approach sketch, and
  acceptance criteria that become `#### Scenario:` blocks in the spec delta.

## Change lifecycle (applies to EVERY DockYarp change)

This is the standing process for all work on DockYarp — see `AGENTS.md` ("Change lifecycle") for the
authoritative statement.

```
backlog item ──/opsx:propose <id>──▶ change ──/opsx:apply──▶ implement ──▶ commit
     ▲                                                                        │
     └────────── flip status: done + parity row ✅ ◀──/opsx:archive◀──────────┘
```

1. **Backlog** — ensure the work has an item `items/<id>.md` (add one if it is new).
2. **Propose** — `/opsx:propose <id>`; author `proposal.md` / `design.md` / `tasks.md` / `specs/<capability>/spec.md`
   from the stub's *Why* + *Acceptance criteria*.
3. **Apply** — `/opsx:apply`; implement with the Nuke gate green (`build.ps1 Test` / `build.sh Test`).
4. **Commit + archive** — present the commit (the user commits), then `/opsx:archive <id>` (syncs
   `openspec/specs/`); present the archive commit.
5. **Close the loop** — set the item's `status: done` (or delete it) and flip its `parity.md` row `⛔/⚠️ → ✅`;
   update `docs/` if user-facing.

## Item front-matter

| Field | Meaning |
|---|---|
| `id` | Future OpenSpec change id — kebab, verb-first (`add-*`, `fix-*`, `clarify-*`, `refine-*`, `finish-*`). |
| `capability` | `proxy-routing` · `docker-discovery` · `yarp-dynamic-config` · `tls-acme` · `security` · `admin-api` · `deployment`. |
| `agent` | Owning domain agent: `AG-RP` · `AG-DD` · `AG-AT` · `AG-SEC` · `AG-AA` · `AG-DEP`. |
| `tier` | `A-structural` (in-process, spec-able now) · `B-runtime` (Kestrel/listener/Docker-heavy) · `C-doc` (small/polish). |
| `priority` | `high` · `medium` · `low`. |
| `status` | `backlog` (not started) · `proposed` (a change exists) · `done` (archived; parity flipped ✅). |

## Adding an item

Copy an existing `items/<id>.md`, pick a kebab verb-first `id`, fill every section, and add a row/link in
`parity.md`. Keep `parity.md` and the item files in sync: a `⚠️`/`⛔` matrix row must always point to an item.
