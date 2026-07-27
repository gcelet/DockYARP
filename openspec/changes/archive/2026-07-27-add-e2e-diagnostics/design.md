## Context
`AspireAppHostFixture` boots the distributed application and disposes it in `[OneTimeTearDown]`, which tears
down all containers. In the Aspire **testing** host, resource console output is **redirected to the
application's logging pipeline** (`ILogger`) — the dashboard's `ResourceLoggerService` is not populated — so a
first attempt using `ResourceLoggerService.WatchAsync` produced empty files. A logging provider on
`builder.Services` is the supported way to capture resource logs in tests.

## Goals / Non-Goals
- **Goal**: per-resource logs written to durable host files during the run; failure surfaces their location.
- **Non-Goal**: forwarding logs to the test console (rejected previously); a live dashboard.

## Decisions
- Register a custom `ResourceFileLoggerProvider` on `builder.Services.AddLogging` **before** `BuildAsync`. It
  writes each log **category** to its own file, opened lazily on first write with `AutoFlush` (partial logs
  survive a crash). Resource logs use the category `<AppHostAssembly>.Resources.<name>`, so the provider strips
  that prefix and names the file `<name>.log` (framework categories keep their full name). An
  `AddFilter<ResourceFileLoggerProvider>(null, Trace)` ensures every resource line reaches the files regardless
  of the AppHost's own log configuration.
- The logger factory disposes the provider when the application is disposed at teardown, flushing/closing the
  files — no manual pump or cancellation needed.
- The log directory comes from `DOCKYARP_E2E_LOG_DIR` (set by the Nuke `E2E` target to `artifacts/e2e-logs`),
  falling back to `<test-bin>/e2e-logs` for a bare `dotnet test`.
- Keep the console silent; the Nuke target reports the directory (and a tail of `dockyarp.log`) only on
  failure.

## Risks / Trade-offs
- Non-resource framework categories also get a file each; acceptable (they aid diagnosis) and bounded.
- Logs accumulate under `artifacts/` (git-ignored); kept every run by choice.

## Migration Plan
- None (test/build infrastructure only).

## Open Questions
- Whether a resource-state timeline file is worth adding later (deferred).
