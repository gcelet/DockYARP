---
id: add-htpasswd-files
capability: security
agent: AG-SEC
tier: A-structural
priority: medium
status: backlog
nginx-proxy: /etc/nginx/htpasswd/<host>[_<PATH_SHA1>]
provenance: this parity pass (matrix: htpasswd ⛔)
---

## Why
nginx-proxy enables Basic Auth by mounting htpasswd files per vhost (and per path). DockYarp only supports
Basic Auth via `DOCKYARP_AUTH_*` labels (single credential in the container's environment), which is awkward
for multiple users and puts credentials in labels. File-based htpasswd is the idiomatic operator workflow.

## nginx-proxy behavior
- `/etc/nginx/htpasswd/<VIRTUAL_HOST>` enables Basic Auth for a vhost; `<VIRTUAL_HOST>_<PATH_SHA1>` scopes it
  to a `VIRTUAL_PATH`. Files are standard Apache htpasswd (bcrypt/apr1/sha).

## DockYarp today
Label-based Basic Auth only: `DOCKYARP_AUTH_USER`/`_PASSWORD`/`_REALM` → per-route credentials
(`LabelParser.cs:207-222`), enforced by `src/DockYarp.Security/BasicAuthMiddleware.cs`. No htpasswd files.

## Proposed change (sketch)
Add an htpasswd source: a configurable directory watched for `<host>` / `<host>_<pathhash>` files, parsed
(support bcrypt + apr1 + sha1), producing per-route Basic Auth credential sets that the existing middleware
consults (files complement/override labels). Never log credentials.

## Acceptance criteria (→ scenarios)
- **WHEN** an htpasswd file exists for a host **THEN** requests without valid credentials get 401 +
  `WWW-Authenticate: Basic`, and a valid user passes.
- **WHEN** an htpasswd file is scoped to a path **THEN** only that path is protected.
- **WHEN** both a label credential and an htpasswd file exist **THEN** the documented precedence applies (and
  multiple htpasswd users all work).

## Notes / risks / references
- Support the common htpasswd hash formats; pick a maintained verification approach (bcrypt via .NET).
