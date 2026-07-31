## Why
DockYarp already emits structured access logs, JSON output (via the logging provider), a disable switch, and
prefix exclusions — matching `LOG_JSON` and `DISABLE_ACCESS_LOGS`. The only remaining `LOG_FORMAT` gap is a
**custom field template**: letting an operator choose exactly which fields appear (and in what order).

## What Changes
- Add `AccessLog:Fields` — an ordered list of field names selected from a fixed catalog (`Method`, `Scheme`,
  `Host`, `Path`, `Query`, `Protocol`, `RemoteIp`, `UserAgent`, `Referer`, `StatusCode`, `ElapsedMs`).
- When configured, each access-log entry contains exactly those fields, in that order (structured, so JSON
  output carries exactly them). When **not** configured, the current default entry is emitted unchanged
  (same source-generated message + fields).
- Unknown field names in the list are ignored. Since DockYarp logs structurally (not raw nginx strings), this
  is a field **selection**, not a raw format string.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `admin-api`: access logging supports an operator-defined field selection.

## Impact
- **Code**: `DockYarp.App` — `AccessLogOptions.Fields`; a pure `AccessLogFields` (catalog + `Select` +
  message `Format`); `AccessLogMiddleware` branches to the custom-fields path when `Fields` is set, else keeps
  the existing `AccessLog.Request` fast path.
- **Tests**: `AccessLogFields.Select` (ordered subset, unknown skipped, default set); the default path stays
  unchanged.
- **Owning agent**: AG-AA. Resolves `refine-access-log-formats`.
