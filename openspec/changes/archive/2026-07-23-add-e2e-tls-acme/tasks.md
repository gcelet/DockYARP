## 1. ACME authority in the AppHost (AG-AT / AG-DEP)

- [x] 1.1 Add a `smallstep/step-ca` container: ACME provisioner enabled, PKI on a bind-mounted host dir (readiness via `WaitFor`)
- [x] 1.2 DockYarp TLS env: `Tls__AcmeDirectoryUri`, `Tls__AcceptTermsOfService`, `Tls__ContactEmail`
- [x] 1.3 Trust the CA: mount the step-ca root into DockYarp and set `SSL_CERT_FILE`; `WaitFor(step-ca)`
- [x] 1.4 Expose DockYarp's HTTPS endpoint (8443); add `--network-alias <host>` for each TLS host (HTTP-01)

## 2. TLS backends + mTLS wiring (AG-AT)

- [x] 2.1 Add a TLS backend (`VIRTUAL_HOST` + `LETSENCRYPT_HOST`) and an HSTS-labeled variant
- [x] 2.2 Add a mutual-TLS backend (`DOCKYARP_CLIENT_CERT=required`)
- [x] 2.3 Generate an ephemeral client CA in the fixture, mount it as `Tls__ClientCaCertificatePath`

## 3. TLS-aware test client + scenarios (AG-AT)

- [x] 3.1 Build an HTTPS `HttpClient` from the `https` endpoint that trusts the step-ca root and captures the server cert
- [x] 3.2 Scenarios (all `[Category("EndToEnd")]`): ACME cert provisioned for `LETSENCRYPT_HOST`; self-signed
      fallback for unknown host; HTTP→HTTPS redirect; per-host HSTS; mutual TLS (valid client cert ⇒ 200, none ⇒ 403)

## 4. Build & validation

- [x] 4.1 `./build.ps1 Test` green with the TLS scenarios present but excluded (no Docker)
- [x] 4.2 `openspec validate add-e2e-tls-acme --strict`
- [ ] 4.3 Runtime validation of the TLS scenarios via `./build.ps1 E2E` — deferred to a Docker-capable session
