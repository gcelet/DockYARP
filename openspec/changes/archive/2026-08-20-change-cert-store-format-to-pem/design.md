## Context

See `proposal.md` — Why. Current concrete shapes, read directly from the code (not re-derived):

- `LoadedCertificate(X509Certificate2 Leaf, IReadOnlyList<X509Certificate2> Additional)`
  (`src/DockYarp.Tls/LoadedCertificate.cs`) — `Leaf` carries the private key (RSA or EC — both loaders
  `PemCertificateLoader.TryAttachPrivateKey` and `CertesAcmeClient.BuildLoadedCertificate` support only these
  two), `Additional` never does.
- `FileCertificateStore.Save()` (`src/DockYarp.Tls/FileCertificateStore.cs:39-54`) currently does
  `fileSystem.File.WriteAllBytes(PathFor(host, ".pfx"), ExportChain(certificate))`, `ExportChain` being
  `[Leaf, .. Additional].Export(X509ContentType.Pfx)!` (no password).
- `FileCertificateStore.Load()` (lines 79-112) enumerates `*.pfx` first, then `*.crt` (paired with `.key` via
  `PemCertificateLoader.TryLoad`) — whichever runs second currently wins for a given host, since both loops
  write into the same `certificates` dictionary keyed by host with no ordering guard beyond "PEM was enumerated
  after PFX".
- `TlsHarness.PrepareMountedChain()` (test-only, `tests/DockYarp.E2E.Tests/TlsHarness.cs`) already builds a
  full-chain PEM the same way this change needs to: `leaf.ExportCertificatePem() + "\n" +
  intermediate.ExportCertificatePem()` — confirms the API shape works for this .NET version, not a new pattern.

## Goals / Non-Goals

**Goals:**
- `Save()` writes PEM (`{host}.crt` full chain, `{host}.key` private key) instead of PFX, for both RSA and EC
  leaf keys (both currently supported via the two loaders above — the write path must handle both, not just
  the ACME path's EC-only key algorithm).
- `Load()`'s PEM-over-PFX precedence for the same host is explicit and deterministic (not implied by directory
  enumeration order, which the current code silently relies on).
- No forced migration: a deployment with only legacy `.pfx` files keeps working unchanged until that host's
  certificate is next provisioned/renewed.

**Non-Goals:**
- Deleting/renaming a legacy `.pfx` once its PEM replacement is written — left on disk (Non-Goal; see Risks).
  Removing it automatically risks deleting a file the operator may have placed there deliberately (an
  operator-provided PFX for a host DockYarp has never itself provisioned), and `Load()`'s new PEM-wins
  precedence already makes a stale PFX harmless, not just cosmetically stale.
- Changing the `LoadedCertificate` shape, `ICertificateStore` interface, or anything upstream of `Save()`/
  `Load()` — this is purely the store's own on-disk serialization; nothing above it (provisioning service,
  SNI selector) needs to know or care which format is on disk.
- A deprecation timeline for PFX *reading* — `Load()` keeps reading `.pfx` indefinitely (an operator can still
  drop one in manually); only the *write* side changes.

## Decisions

**Build the PEM text directly from `LoadedCertificate` in `FileCertificateStore.Save()` — no new abstraction,
no shared "PEM builder" type.**

`.crt`: `string.Join('\n', new[] { certificate.Leaf }.Concat(certificate.Additional).Select(c =>
c.ExportCertificatePem()))` (mirrors `TlsHarness.PrepareMountedChain`'s existing pattern for building a
full-chain PEM, just generalized to N additional certs instead of exactly one).
`.key`: detect RSA vs EC on `certificate.Leaf` (`GetRSAPrivateKey()` first, `GetECDsaPrivateKey()` if that's
null — the same try-order `PemCertificateLoader.TryAttachPrivateKey` already uses, just for export instead of
import) and call `ExportPkcs8PrivateKeyPem()` on whichever key object is non-null. Considered and rejected:
extracting a shared helper in `CertificateCollectionLoader` or a new type — this is a handful of lines used in
exactly one place (`Save()`); a shared abstraction for a single call site is premature.

**`Load()`: after both enumeration loops, if a host has entries from both formats, keep the PEM one — done by
loading PFX into a separate temporary dictionary first, then PEM into the final `certificates` dictionary
(unconditionally overwriting), then merging any PFX-only hosts the PEM pass didn't already provide.**

Rationale: the simplest correct fix for "PEM wins regardless of enumeration order" without changing the
existing two-loop structure's shape — enumerate PFX into a working set, enumerate PEM directly into
`certificates` (so PEM entries are never at risk of being overwritten by a later PFX read), then copy over only
the PFX entries whose host isn't already present. This keeps `Load()`'s existing per-format loop bodies
essentially unchanged (still simple `foreach` + `TryLoad`/`LoadKeyed` calls), just changes which dictionary
each loop targets and adds one final merge step. Considered and rejected: enumerating PEM first, then skipping
a PFX file if its host key already exists in `certificates` — behaviorally equivalent, but couples the PFX
loop's body to knowledge of the PEM loop's results (an `if (certificates.ContainsKey(host)) continue;` check
scattered into the "wrong" loop reads less clearly than a small merge step at the end that says outright "PEM
wins").

## Risks / Trade-offs

- [Risk] A stale `<host>.pfx` accumulates on disk indefinitely once that host's PEM pair exists (Non-Goal:
  not deleted). → Mitigation: harmless per the new precedence (PEM always wins), and worth a one-line mention
  in the docs-site TLS section so an operator understands why both files might coexist, rather than assuming
  it's a bug.
- [Risk] `ExportPkcs8PrivateKeyPem()` on an unencrypted key writes the same private-key exposure the current
  PFX already has (no password either) — not a new risk, but worth stating explicitly since "PEM private key
  on disk" sounds more alarming at a glance than "PFX on disk" even though the actual protection (filesystem
  permissions on the certs volume) is identical. → Accepted, matches the existing threat model exactly; no
  mitigation needed beyond what already protects the PFX today.
- [Risk] Any external tooling an operator has pointed at `<host>.pfx` directly (outside DockYarp) breaks once
  that host's certificate is next renewed and only `.crt`/`.key` exist. → Accepted (see proposal.md's
  **BREAKING** note) — nothing in-product reads the file directly other than `FileCertificateStore` itself.

## Migration Plan

No explicit migration step for the operator: on upgrade, existing `.pfx` files keep being read and served
exactly as before (`Load()` still supports the format). The *next* time a host's certificate is provisioned or
renewed (ACME) or an operator replaces a `.pfx` with a PEM pair themselves, that host's on-disk representation
becomes PEM going forward. No rollback concern — reverting to a prior DockYarp version would simply stop
writing PEM (old `Save()` behavior) while `Load()` in that prior version already reads whatever the certs
directory happens to contain, PFX or PEM state notwithstanding (the read side isn't new in this change; it's
been PEM-and-PFX-capable since `fix-pem-cert-chain-dropped-on-load`).
