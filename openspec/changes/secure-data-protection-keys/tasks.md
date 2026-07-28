## 1. Options + wiring (AG-SEC)
- [x] 1.1 `DockYarp.Security`: add `DataProtectionOptions` record (`CertificatePath`, `CertificatePassword`)
- [x] 1.2 `DockYarp.Security`: add `AddDockYarpDataProtection(IHostApplicationBuilder, DataProtectionOptions,
      string keyDirectory)` — persist keys; if a certificate is configured, load it (actionable throw on failure)
      and `ProtectKeysWithCertificate`; else raise only the `XmlKeyManager` category floor to `Error` (justified)
- [x] 1.3 `Program.cs`: bind the `DataProtection` section and replace the inline DP block with the extension call

## 2. Docs (AG-DEP)
- [x] 2.1 `docs/deployment.md`: document the `DataProtection` section, that the certificate must live outside the
      `/certs` volume for real protection, and the "encrypt only when a feature needs it" policy

## 3. Tests (AG-SEC)
- [x] 3.1 `DockYarp.Security.Tests`: options binding / no-path ⇒ null
- [x] 3.2 `DockYarp.Security.Tests`: certificate loading — valid self-signed PFX loads (thumbprint matches);
      missing path throws actionable; wrong password throws actionable
- [x] 3.3 `DockYarp.Security.Tests`: decision — certificate configured ⇒ encrypted (round-trip); absent ⇒ suppressed

## 4. Backlog trace (AG-DEP)
- [x] 4.1 `add-loadbalance-policies`: note that implementing session affinity MUST require the DP encryption
      certificate and fail fast if absent (the deferred case-3 gate)

## 5. Verify (AG-DEP)
- [x] 5.1 `dotnet build` + Nuke `Test` gate green (176 passed)
- [x] 5.2 Confirm at the next `E2E` run (default config, no certificate) that `dockyarp.log` shows neither
      `FileSystemXmlRepository[60]` nor `XmlKeyManager[35]` — validated together with change `test-restart-state-persistence`
