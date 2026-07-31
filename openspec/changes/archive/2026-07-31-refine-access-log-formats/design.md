# Design — refine-access-log-formats

## Context
`AccessLogMiddleware` writes one entry per handled request via `AccessLog.Request`, a source-generated
`LoggerMessage` with **fixed** fields (Method, Scheme, Host, Path, StatusCode, ElapsedMs). Compile-time
`LoggerMessage` cannot carry a runtime-configurable field set. DockYarp logs structurally, so the `LOG_FORMAT`
analog is a **field selection**, not a raw nginx format string.

## Decisions

### 1. `AccessLog:Fields` — ordered selection from a fixed catalog
`AccessLogOptions.Fields` (`string[]?`) lists the field names to emit, in order, chosen from a fixed catalog:
`Method`, `Scheme`, `Host`, `Path`, `Query`, `Protocol`, `RemoteIp`, `UserAgent`, `Referer`, `StatusCode`,
`ElapsedMs`. Names are matched case-insensitively; unknown names are ignored. Empty/unset selects the default.

### 2. Keep the default fast path unchanged
When `Fields` is unset, the middleware keeps calling the existing source-generated `AccessLog.Request`, so the
default output (message and fields) and its performance are **unchanged** (satisfies the default-behavior AC).
The dynamic path runs only when a custom selection is configured.

### 3. Pure selection + structured emission
`AccessLogFields.Select(catalog, fields)` is pure: it returns the catalog entries named by `fields`, in the
configured order, canonical-cased. The middleware builds the catalog from the request/response, selects, and
logs the result as a structured state (`IReadOnlyList<KeyValuePair<string, object>>`) with a `key=value`
message formatter — so a JSON logging provider emits exactly the selected fields, and text output renders them.

## Verification
- Unit only: `AccessLogFields.Select` (ordered subset, unknown skipped, and the default field set). The
  catalog-from-context build and the structured `logger.Log` call are thin and verified by inspection; the
  rendered JSON is the logging provider's concern. No e2e needed.

## Risks
- The dynamic path allocates a small field list per logged request (only when a custom selection is
  configured); acceptable for an operator-opted logging feature.
