# Design — secure-data-protection-keys

## Context
`persist-state-on-writable-volume` persisted the Data Protection (DP) key ring to `/certs` and silenced
`FileSystemXmlRepository[60]`. `XmlKeyManager[35]` ("No XML encryptor configured. Key {..} may be persisted to
storage in unencrypted form.") remains. On Linux/chiseled there is no OS key-protection (no DPAPI), so ASP.NET
emits `[35]` whenever the key ring is persisted without an explicit encryptor.

Established fact (verified in code): **nothing configures YARP session affinity**, and DockYarp has no cookies,
antiforgery, or auth-cookie. The DP key ring therefore protects **no sensitive payload** today; it is initialized
only because YARP registers DP transitively and ASP.NET initializes the ring at startup.

## Decision: conditional real encryption, honest default

Three cases, decided with the user:

1. **No DP consumer + no encryption certificate (today's default).** Do not require anything. The key ring still
   initializes (framework-driven), so `[35]` would fire; because it is benign here (nothing sensitive is
   protected), suppress **only** that warning and document why.
2. **An encryption certificate is configured.** Protect the key ring with it via ASP.NET's built-in X.509
   encryptor — real at-rest protection when the certificate's private key lives **outside** `/certs`. No
   suppression is needed: a configured encryptor makes `[35]` disappear on its own.
3. **A future DP-consuming feature (session affinity) is enabled without a certificate.** Fail fast at startup
   with an actionable message. **Deferred**: there is no such feature yet, so building the gate now would be
   speculative (YAGNI). The requirement is recorded on the `add-loadbalance-policies` backlog item (where session
   affinity will be implemented), so the gate ships with its first real consumer.

### Why a certificate, not a passphrase
ASP.NET ships a first-party X.509 key-ring encryptor (`ProtectKeysWithCertificate` /
`UnprotectKeysWithAnyCertificate`). Using it avoids hand-rolled crypto (a passphrase would require a custom
`IXmlEncryptor` with KDF + AEAD — more code and more risk, for no analyzer-approved benefit). The operator
supplies a PFX/PEM path (+ optional password); DockYarp treats it as opaque key material.

### Why co-located encryption is rejected
Encrypting the key ring with a certificate stored inside `/certs` (next to the encrypted keys) gives an attacker
with volume access both the ciphertext and the key — no real protection. Hence the certificate is operator-
supplied and expected to live outside the state volume; the docs say so explicitly. We do **not** auto-generate a
co-located certificate just to silence the warning.

### Why suppress `[35]` in the default case (and only then)
`[35]` is not actionable when nothing sensitive is protected, and there is no ASP.NET "I accept unencrypted keys"
flag. Suppression is scoped to the single category
`Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager` (raise its floor to `Error`) and applied
**only** when no encryptor is configured — the moment a certificate is supplied, the floor is left untouched and
the warning is gone for real. This mirrors, but does not contradict, the earlier decision **not** to suppress
`[60]`: `[60]` signalled a real consequence (keys lost across restarts → broken affinity if ever enabled), which
we fixed by persisting; `[35]` signals a consequence only for sensitive payloads, of which there are none.

## Shape
- `DataProtectionOptions` (record): `CertificatePath` (string?), `CertificatePassword` (string?).
- `DataProtectionSetup.AddDockYarpDataProtection(this IHostApplicationBuilder builder, DataProtectionOptions
  options, string keyDirectory)` in `DockYarp.Security`:
  - `AddDataProtection().PersistKeysToFileSystem(<keyDirectory>/dataprotection-keys).SetApplicationName("dockyarp")`.
  - if `CertificatePath` set → load the certificate (throw an actionable exception on failure) and
    `ProtectKeysWithCertificate(cert)`;
  - else → `builder.Logging.AddFilter("…KeyManagement.XmlKeyManager", LogLevel.Error)` with a justification
    comment.
- `Program.cs` replaces the inline DP block with a single call, passing `tlsOptions.CertificateDirectory`.

## Testability
DI/logging wiring is awkward to assert directly, so the unit tests target the extractable logic:
- options binding from configuration;
- certificate loading: a self-signed PFX written to a temp file loads and matches by thumbprint; a missing path
  and a wrong password each throw an actionable exception;
- the decision: certificate configured ⇒ the setup reports "encrypted"; absent ⇒ "suppressed".

## Risks
- Raising the `XmlKeyManager` category floor to `Error` also hides that category's informational key-creation
  logs; acceptable and documented (the diagnostics we care about — repository path, provisioning — are other
  categories).
- If a consumer of DP is ever added without also wiring the case-3 fail-fast, unencrypted keys could silently
  protect real payloads. Mitigated by the explicit note on `add-loadbalance-policies`.
