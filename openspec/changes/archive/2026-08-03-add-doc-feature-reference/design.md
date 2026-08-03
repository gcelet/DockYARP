# Design — add-doc-feature-reference (bounded: labels + environment variables)

## Scope
The container-configuration surface only: every key DockYARP reads from a container's labels **or** environment
variables. The proxy's own application configuration (appsettings sections) and worked recipes are deferred to
`add-doc-capability-reference` and `add-doc-examples`.

## Source of truth
`src/DockYarp.Docker/Labels/DockerLabels.cs` (the recognized keys) + the archived `openspec/specs/`. The
reference must use real key names and realistic examples (no placeholders), per the documentation spec.

## Content plan (both pages kept consistent)
Lead note (both pages): **any key may be set as a container label or an environment variable; when both are set
for the same key, the environment variable wins** (nginx-proxy's canonical channel is the env var). Then the
full reference, grouped:
- **Routing**: `VIRTUAL_HOST`, `VIRTUAL_HOST_MULTIPORTS`, `VIRTUAL_PORT`, `VIRTUAL_PATH`, `VIRTUAL_PROTO`
  (`http`/`https`/`grpc`/`grpcs`), `VIRTUAL_DEST`.
- **TLS**: `LETSENCRYPT_HOST`, `LETSENCRYPT_EMAIL`, `CERT_NAME`, `SSL_POLICY`, `HTTPS_METHOD`, `HSTS`,
  `EXTERNAL_HTTPS_PORT`, `ENABLE_HTTP_ON_MISSING_CERT`, `TRUST_DEFAULT_CERT`.
- **Access control, headers & tuning**: `NETWORK_ACCESS`, `DOCKYARP_CLIENT_CERT`,
  `DOCKYARP_AUTH_USER`/`_PASSWORD`/`_REALM`, `DOCKYARP_LB`, `DOCKYARP_PRIORITY`, `DOCKYARP_PROXY_TIMEOUT`,
  `DOCKYARP_MAX_BODY_SIZE`, `SERVER_TOKENS`.
- **nginx-proxy namespaced label aliases**: `…loadbalance`→`DOCKYARP_LB`, `…ssl_verify_client`→
  `DOCKYARP_CLIENT_CERT`, `…trust-default-cert`→`TRUST_DEFAULT_CERT` (DockYarp-native key wins).

## Verify
Docs-only: no .NET build gate. `openspec validate --strict`. A Hugo build is not run locally (the toolchain/CI
is tracked by `add-doc-ci-publish`); the Markdown follows the existing page conventions.
