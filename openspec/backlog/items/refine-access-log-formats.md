---
id: refine-access-log-formats
capability: admin-api
agent: AG-AA
tier: C-doc
priority: low
status: backlog
nginx-proxy: LOG_FORMAT (LOG_JSON / DISABLE_ACCESS_LOGS already covered)
provenance: this parity pass (matrix: custom LOG_FORMAT ⚠️)
---

## Why
DockYarp already emits structured access logs, JSON output, and a disable switch — matching `LOG_JSON` and
`DISABLE_ACCESS_LOGS`. The only remaining gap is a **custom** log format/template like nginx-proxy's
`LOG_FORMAT` for operators who need a specific field layout.

## nginx-proxy behavior
- `LOG_FORMAT` sets a custom nginx `log_format`; `LOG_FORMAT_ESCAPE` sets the escape mode; `LOG_JSON` selects a
  predefined JSON format; `DISABLE_ACCESS_LOGS` turns logging off.

## DockYarp today
Structured per-request access logging with fixed fields (method/host/path/status/elapsed), JSON via the
logging provider, disable-able, excludes configured prefixes (`src/DockYarp.App/Observability/AccessLog*.cs`,
`openspec/specs/admin-api/spec.md`). No operator-defined field template.

## Proposed change (sketch)
Add a configurable field set/template for the access log (which fields, order, names), layered on the existing
structured logger. Keep JSON + disable behavior intact. Likely a small options extension.

## Acceptance criteria (→ scenarios)
- **WHEN** a custom field template is configured **THEN** access log entries contain exactly those fields.
- **WHEN** no template is configured **THEN** the current default fields are emitted (unchanged).

## Notes / risks / references
- Smallest of the ops items; could ship as a docs+options refinement.
