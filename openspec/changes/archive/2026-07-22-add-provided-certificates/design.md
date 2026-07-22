## Context

`FileCertificateStore` loads only `*.pfx` (keyed by file name) via static `System.IO`, and
`SniCertificateSelector` matches only the exact host. nginx-proxy users mount PEM pairs and expect a
wildcard certificate to cover subdomains. This adds PEM loading and wildcard-parent selection, and makes the
store's filesystem access mockable.

## Goals / Non-Goals

**Goals:** load mounted PEM pairs and PFX from the certificate directory; select a parent-domain
(wildcard) certificate for SNI when no exact match; unit-test loading against a mock filesystem.

**Non-Goals (deferred):** `CERT_NAME` (per-vhost shared certificate label), encrypted private keys, chained
intermediate handling beyond what the PEM contains, and multi-level wildcard matching (single-label strip
only).

## Decisions

- **`System.IO.Abstractions`**: `FileCertificateStore` takes an `IFileSystem` and performs all IO through it
  (byte/text-based certificate loaders so a `MockFileSystem` works). Registered as
  `AddSingleton<IFileSystem, FileSystem>()`. Chosen over static `System.IO` for testability.
- **PEM loading**: for each `{host}.crt` with a matching `{host}.key`, `X509Certificate2.CreateFromPem` then
  a PFX round-trip (`LoadPkcs12(pem.Export(Pfx), null)`) to normalize the private key so it is usable for
  TLS across platforms. A `.crt` with no `.key` is skipped. PFX is loaded first, then PEM, so a mounted PEM
  wins for the same host.
- **Wildcard parent**: `SniCertificateSelector` tries the exact host, then `ParentDomain(host)` (strip the
  leftmost label, only when the remainder still contains a dot — so `foo.example.com` → `example.com`, but
  `example.com` has no parent), then the fallback. A wildcard cert is provided under its base domain
  (`example.com.crt` for `*.example.com`), which is filesystem-safe (no `*` in file names).

## Risks / Trade-offs

- The PFX round-trip on PEM import is defensive (Windows ephemeral-key issue); harmless on Linux and keeps
  the in-memory certificate uniform. We cannot runtime-test the handshake here, so loading/selection logic
  is covered by unit tests and the round-trip guards the private-key path.
- Single-label wildcard strip matches nginx-proxy/docker-gen; multi-level wildcards are out of scope.

## Migration Plan

Additive: new dependency, `IFileSystem` ctor parameter on `FileCertificateStore` (DI-provided; direct
constructors in tests pass a filesystem), extra loading + selection paths. PFX-only setups are unchanged.

## Open Questions

- `CERT_NAME` shared-certificate label — deferred to its own change (needs discovery → TLS-metadata → SNI
  plumbing).
