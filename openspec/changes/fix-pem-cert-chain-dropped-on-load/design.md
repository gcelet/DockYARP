## Context

See `proposal.md` - Why for the root-cause analysis (verified against Microsoft's own
`X509Certificate2.CreateFromPem` documentation and reproduced live with `curl -vvvv`). This section covers the
concrete implementation approach, verified against the actual .NET APIs (via `dotnet-inspect`, not assumed)
before committing to it.

## Goals / Non-Goals

**Goals:**
- `PemCertificateLoader.TryLoad` preserves every certificate present in a `.crt` file, not just the first.
- The private key is attached to whichever certificate in the file it actually belongs to — not assumed to be
  "the first one" — since a defensive approach costs little extra code and removes an ordering assumption.
- The single-certificate case (today's only tested path) is unchanged.

**Non-Goals:**
- Not validating or reordering the chain (leaf → intermediate → ... → root ordering as sent over the wire is
  whatever order the certificates end up in the resulting `X509Certificate2`'s chain; .NET/Kestrel handles
  chain-sending order internally once the certificate carries the full chain — this change is only about *not
  discarding* certificates, not about re-deriving chain topology).
- Not adding certificate validation (expiry, hostname match, trust) beyond what already exists — out of scope,
  a separate concern from "is the chain preserved."

## Decisions

- **Build the fix as: `X509Certificate2Collection.ImportFromPem` (loads every cert) → find the entry the
  private key actually matches via `CopyWithPrivateKey` → re-export the whole collection as PKCS12 → reload via
  `X509CertificateLoader.LoadPkcs12`.** This exactly mirrors the shape `FileCertificateStore`'s existing `.pfx`
  branch already loads correctly (and that `CertesAcmeClient.BuildPfx` already produces for ACME-issued certs),
  rather than inventing a new in-memory chain-attachment mechanism. Verified via `dotnet-inspect` (not assumed)
  before choosing this: `X509Certificate2Collection.ImportFromPem(ReadOnlySpan<char>)`,
  `X509Certificate2.CopyWithPrivateKey(RSA|ECDsa|DSA)` (extension methods), `RSA.ImportFromPem`/
  `ECDsa.ImportFromPem`, and `X509Certificate2Collection.Export(X509ContentType.Pkcs12)` all exist with the
  expected signatures on the target .NET version.
- **Find the leaf by trying `CopyWithPrivateKey` on each candidate, not by assuming "first cert in the file is
  the leaf."** The leaf-first convention holds for every real-world tool this project has seen (acme.sh,
  certbot, Let's Encrypt's own `fullchain.pem`, nginx-proxy/acme-companion), but the backlog stub's own caution
  ("don't assume first cert = leaf") costs nothing to honor defensively: try the private key against each
  certificate in the parsed collection, use the one that doesn't fail. This removes the ordering assumption
  entirely rather than documenting it as a known limitation.
- **RSA and ECDsa key support, matching the existing XML doc's stated support ("an RSA or EC private key").**
  Try both key types when attaching (parse the key PEM once per algorithm attempt, or detect the key's PEM
  label to pick the algorithm directly) — no DSA support needed, nothing in this codebase or its target
  ecosystem (ACME certs) uses DSA.

## Risks / Trade-offs

- [Risk] The "try each candidate" leaf-matching loop adds a small amount of one-time startup cost per
  certificate file (negligible — certificate loading happens once at startup/on file change, not per-request) →
  [Mitigation] not a hot-path concern; correctness here matters more than a few extra milliseconds at startup.
- [Risk] A malformed `.crt` file (e.g. the private key matches none of the certificates present) needs a clear
  failure mode → [Mitigation] tasks.md includes this as an explicit test case; the existing `TryLoad` contract
  already returns `false`/skips on failure, so this fits the existing error-handling shape without a new one.
