## 1. Capture (AG-DEP)
- [x] 1.1 Add a `ResourceFileLoggerProvider` that writes each log category to `<logdir>/<category>.log`
      (lazy, auto-flushed); register it on `builder.Services.AddLogging` before `BuildAsync`, capturing all
      categories (`AddFilter<...>(null, Trace)`)
- [x] 1.2 Resolve `<logdir>` from `DOCKYARP_E2E_LOG_DIR`, falling back to `<test-bin>/e2e-logs`

## 2. Nuke surfacing (AG-DEP)
- [x] 2.1 `E2E` target: create `artifacts/e2e-logs`, pass it as `DOCKYARP_E2E_LOG_DIR` to the test run
- [x] 2.2 On failure, log the directory and a tail of `dockyarp.log`

## 3. Docs (AG-DEP)
- [x] 3.1 Document the e2e diagnostics location in `docs/deployment.md`
