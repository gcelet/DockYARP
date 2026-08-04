## 1. Document the gaps (AG-DOC)
- [x] 1.1 `features.md`: Access control (label + htpasswd Basic Auth, internal-only, mTLS cross-ref); Proxying
      (response compression, httpoxy `Proxy`-header stripping); regex `VIRTUAL_PATH` bullet; static-config
      `Overrides` (per-host / `default` response headers) with a JSON sample
- [x] 1.2 `configuration.md`: `VIRTUAL_PATH` notes the `~`-prefixed regex form
- [x] 1.3 `examples.md`: an htpasswd Basic Auth recipe

## 2. Spec (AG-DOC)
- [x] 2.1 MODIFY `documentation` "Runtime feature reference" to enumerate access control, compression + httpoxy,
      and per-host static-config overrides

## 3. Verify (AG-DOC)
- [x] 3.1 `openspec validate --strict` green; features verified against `openspec/specs/` + the code
