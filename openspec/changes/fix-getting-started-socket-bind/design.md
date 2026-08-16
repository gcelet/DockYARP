## Context

See `proposal.md` - Why. Three files carry the affected content: `docs-site/content/en/docs/getting-started.md`
(broken quick-start), `docs-site/content/en/docs/examples.md` (correct pattern, understated as optional),
`docs-site/content/en/docs/deployment.md` and root `docker-compose.yml` (inconsistent image name). No
application code is touched — this is a documentation-content fix.

## Goals / Non-Goals

**Goals:**
- The Getting Started quick-start works verbatim on a standard Linux Docker install.
- The socket-proxy requirement is stated as a requirement, not a hardening suggestion.
- One consistent published image name (`gcelet/dockyarp`) across every doc page and the root compose file.

**Non-Goals:**
- No change to the Dockerfile, image build, or runtime behavior — the image is already correctly non-root; only
  the documentation describing how to run it is wrong.
- No new "Base stack" abstraction shared between `getting-started.md` and `examples.md` — the two pages serve
  different readers (first-run vs. recipe reference) and can each show the compose snippet directly.

## Decisions

- **Rewrite the quick-start to use the socket-proxy pattern outright, rather than keeping the direct bind mount
  plus a warning.** A warning next to a broken command still leaves the reader with a broken first experience if
  they skip the prose (a fair assumption for a quick-start). Matching `examples.md`'s already-correct pattern
  also means there is only one canonical "how DockYARP reaches Docker" shape documented site-wide.
- **`gcelet/dockyarp` becomes the canonical example image name everywhere; `dockyarp:local` is kept as the
  documented local-build alternative**, using the exact annotated form already established in the nginx-proxy
  migration guide (`image: gcelet/dockyarp  # or dockyarp:local for a local build`) rather than inventing a new
  phrasing.

## Risks / Trade-offs

- [Risk] The quick-start becomes one container longer (socket-proxy + dockyarp instead of just dockyarp) →
  [Mitigation] this is the actual minimum working setup; hiding that complexity behind a broken shorter example
  is worse than showing the real one.
