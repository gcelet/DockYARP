## 1. Options (AG-DD)
- [x] 1.1 `DockerDiscoveryOptions.CertPath` (string?) and `TlsVerify` (bool)

## 2. Credentials factory (AG-DD)
- [x] 2.1 `DockerTlsCredentials.Create(endpointUsesTls, tlsVerify, caPem, certPem, keyPem)` → `Credentials?`
      (null for non-TLS endpoint or missing client cert)
- [x] 2.2 Private `ClientCertificateCredentials : Credentials` wiring `ManagedHandler.ClientCertificates` +
      `ServerCertificateValidationCallback`; `IsTlsCredentials() => true`
- [x] 2.3 `LoadClientCertificate` (PEM → PKCS12, Windows-usable key); `BuildServerValidation` (accept-any when
      `!tlsVerify`, else `X509Chain` custom-root-trust vs `ca.pem`)

## 3. Wire it into the client (AG-DD)
- [x] 3.1 `DockerContainerSource.CreateClient(options)`: read `ca/cert/key.pem` from `CertPath`, compute
      `endpointUsesTls` from the URI scheme, pass credentials to `DockerClientConfiguration` (or the plain
      path when null)

## 4. Tests (AG-DD)
- [x] 4.1 Test-cert helper: a CA + a client leaf + a server leaf (signed by the CA) + an unrelated cert, as PEM
- [x] 4.2 `Create`: non-TLS endpoint → null; missing client cert → null
- [x] 4.3 `Create` (TLS, no verify) → TLS credentials; `GetHandler(new ManagedHandler())` sets the client cert
      and a callback that accepts any server cert
- [x] 4.4 `Create` (TLS, verify) → the callback accepts a daemon cert chaining to `ca.pem` and rejects an
      unrelated one

## 5. Docs (AG-DOC)
- [x] 5.1 Site configuration reference: document `Docker:CertPath` / `Docker:TlsVerify`

## 6. Verify (AG-DD)
- [x] 6.1 Nuke `Test` gate green (unit/integration, no Docker)
