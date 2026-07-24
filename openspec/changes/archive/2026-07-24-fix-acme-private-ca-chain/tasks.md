## 1. Certificate-building fallback (AG-AT)

- [x] 1.1 `CertesAcmeClient`: build the PFX with `FullChain = true`, and on Certes' chain-resolution
      `AcmeException` rebuild with `FullChain = false` (leaf only)
- [x] 1.2 Keep the fallback scoped to the `Build` call so earlier ACME failures still surface
- [x] 1.3 (harness) TLS test client disables connection reuse (`PooledConnectionLifetime = TimeSpan.Zero`) so the
      poll re-handshakes and observes the cert once provisioned (a pooled connection kept the initial fallback)

## 2. Build & validation

- [x] 2.1 `./build.ps1 Test` green (existing unit/integration suite; no regression)
- [x] 2.2 `openspec validate fix-acme-private-ca-chain --strict`
- [x] 2.3 Runtime: `./build.ps1 E2E` — validated in WSL, all 21 e2e tests pass (both ACME scenarios green)
