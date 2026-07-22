## 1. Dependencies & filesystem abstraction (AG-AT)

- [x] 1.1 Add `System.IO.Abstractions` (+ `System.IO.Abstractions.TestingHelpers` for tests) to `Directory.Packages.props`; reference in the Tls and Tls.Tests projects
- [x] 1.2 `FileCertificateStore` takes an `IFileSystem` and routes all IO through it; register `AddSingleton<IFileSystem, FileSystem>()`

## 2. Provided certificate loading (AG-AT)

- [x] 2.1 Load `{host}.pfx` via byte loader through `IFileSystem`
- [x] 2.2 Load `{host}.crt` + `{host}.key` PEM pairs (skip unpaired `.crt`); normalize via a PFX round-trip; PEM overrides PFX for the same host

## 3. Wildcard parent selection (AG-AT)

- [x] 3.1 `SniCertificateSelector`: exact host → parent-domain certificate → fallback (single-label strip, `ParentDomain`)

## 4. Tests & docs (AG-AT)

- [x] 4.1 Store tests (MockFileSystem): PEM pair loaded with private key; PFX loaded; unpaired `.crt` skipped
- [x] 4.2 Selector tests: exact match; parent-domain match; fallback when neither
- [x] 4.3 Document provided certificates + wildcard convention in `docs/tls-acme.md`
- [x] 4.4 Build + full test suite green via the Nuke CLI
