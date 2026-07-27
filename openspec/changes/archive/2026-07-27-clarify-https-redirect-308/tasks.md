## 1. Lock behavior (AG-SEC)
- [x] 1.1 Unit test (`tests/DockYarp.Security.Tests`): an enforced host redirects a POST with status 308 and
      the HTTPS `Location` for the same host/path

## 2. Spec & docs (AG-SEC)
- [x] 2.1 Clarify in the `security` spec that HTTP→HTTPS redirects use 308 (permanent, method-preserving)
- [x] 2.2 Note in `docs/security-middleware.md` that DockYarp uses 308 for all redirects, so no separate
      `NON_GET_REDIRECT` knob is provided
