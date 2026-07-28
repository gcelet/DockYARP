## Why
DockYarp persists its Data Protection key ring to the `/certs` volume (from `persist-state-on-writable-volume`),
which silenced the ephemeral-keys warning `FileSystemXmlRepository[60]`. The keys are still written **unencrypted
at rest**, so `XmlKeyManager[35]` ("No XML encryptor configured…") is emitted at every startup.

Crucially, **Data Protection currently protects nothing sensitive** in DockYarp: no session affinity is
configured on any cluster, and there are no cookies, antiforgery, or auth-cookie payloads. YARP registers Data
Protection transitively and ASP.NET initializes the key ring at startup, but no meaningful payload is ever
protected. So encrypting the key ring with a key stored **next to** it (in `/certs`) would silence `[35]` while
adding no real at-rest protection — security theater. Forcing an operator to supply an encryption secret when no
feature needs it is equally unjustified.

This change makes at-rest encryption **real but conditional**: an operator MAY supply an encryption certificate
(kept outside `/certs`) to genuinely protect the key ring, and when they do not, DockYarp requires nothing and
suppresses only the benign `[35]` warning (with a written justification). The fail-fast that will *require* the
secret once a Data-Protection-consuming feature (session affinity) exists is deferred to that feature's change
(tracked on `add-loadbalance-policies`).

## What Changes
- **New `DataProtection` configuration section**: `CertificatePath` (+ optional `CertificatePassword`) naming an
  X.509 certificate used to encrypt the persisted key ring.
- **Conditional encryptor**: when a certificate is configured, protect the key ring with it
  (`ProtectKeysWithCertificate` — ASP.NET's built-in X.509 encryptor, no custom crypto); startup fails with an
  actionable error if the certificate cannot be loaded.
- **Default case**: when no certificate is configured, suppress only the benign `XmlKeyManager[35]` warning
  (raise that one category's log floor) with a justification comment, since no sensitive payload is protected.
- Move the Data Protection wiring out of `Program.cs` into a testable `AddDockYarpDataProtection` extension in the
  Security module (options binding + certificate loading are unit-tested).
- Document the section and the "encrypt only when a feature needs it" policy in `docs/deployment.md`.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: the persisted Data Protection key ring can be encrypted at rest with an operator-supplied
  certificate; when none is supplied, DockYarp starts without requiring one and does not emit the
  unencrypted-keys warning.

## Impact
- **Code**: `src/DockYarp.Security/` (new `DataProtectionOptions` + `AddDockYarpDataProtection` extension +
  certificate loader), `src/DockYarp.App/Program.cs` (call the extension), `docs/deployment.md`.
- **Tests**: `tests/DockYarp.Security.Tests` — options binding, certificate loading (valid / missing / bad
  password), and the encrypt-vs-suppress decision.
- **Deferred**: the fail-fast that *requires* the encryption certificate once session affinity (a Data
  Protection consumer) is implemented — noted on `add-loadbalance-policies`. Runtime restart-persistence
  coverage is the sibling change `test-restart-state-persistence`.
- **Owning agent**: AG-DEP (with AG-SEC for the certificate/crypto wiring).
- **Backlog**: resolves `encrypt-data-protection-keys-at-rest`.
