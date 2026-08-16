## Context

See `proposal.md` - Why. Grounding done before writing (not assumed):
- The advanced-path source material is the user's own real installation, described in a local, gitignored file
  (`nginx-proxy-real-installation-private-information.md`, never committed) — the classic
  `nginxproxy/nginx-proxy` + `nginxproxy/docker-gen` + `nginxproxy/acme-companion` trio, `VIRTUAL_HOST`/
  `LETSENCRYPT_HOST`-style env vars per backend stack, a private step-ca instance as the ACME CA
  (`ACME_CA_URI` pointed at it, `REQUESTS_CA_BUNDLE` trusting its root for acme-companion's own client), and
  `SSL_POLICY: Mozilla-Intermediate` set globally. The guide's advanced path generalizes this pattern — it does
  not reproduce the user's real hostnames/domains/IPs.
- **Cert reuse verified by reading code**, not assumed: `PemCertificateLoader` (`src/DockYarp.Tls/`) delegates
  to `X509Certificate2.CreateFromPem`, which accepts a full chain in `.crt` and either RSA or EC keys in
  `.key` — exactly acme-companion's output shape. `FileCertificateStore.Load()` auto-detects `{host}.crt`+
  `{host}.key` pairs at startup and reuses them (skipping ACME provisioning) unless within `RenewBeforeExpiry`
  of expiry. No conversion step exists in the code because none is needed.
- **Private-CA ACME trust verified with a live, isolated test**, not documentation alone — the user explicitly
  flagged that this dev machine already trusts their real private CA, so a same-machine test could give a false
  positive. Generated a throwaway self-signed root + leaf cert (unrelated to the user's real CA), served it on
  its own Docker network, and ran `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` (DockYarp's exact base
  image) against it: without `SSL_CERT_FILE` set, the TLS handshake failed (`PartialChain` — proving the test
  container had no ambient trust); with `SSL_CERT_FILE` pointing at the throwaway root, it succeeded. .NET on
  Linux delegates trust-store resolution to OpenSSL and honors this standard env var.
- `ACME_HTTP_CHALLENGE_LOCATION` (present in the user's real `.env`) only controls which component
  (acme-companion vs. nginx-proxy's own built-in support) writes the nginx challenge-location config — not a
  DNS-01 toggle. No DNS-provider credentials exist anywhere in the real config, confirming HTTP-01, which
  DockYarp already supports.
- `examples.md`'s "Base stack" (socket-proxy + `dockyarp` service, `certs`/`config` volumes) is the site's
  existing shared reference stack every recipe builds on — the migration guide should point to it, not
  duplicate it, for the DockYarp-side compose shape.
- Doc-site page weights are currently sequential 1-8 (`getting-started` … `releasing`), no gaps.

## Goals / Non-Goals

**Goals:**
- Cover both a basic and an advanced (private-CA, env-var-config, multi-network) migration, generalized from a
  real installation without exposing its specifics.
- Make the certificate/rollback story explicit and safe: copy, don't move; no conversion; old install stays
  intact.
- Place the page where an evaluating/switching operator would actually look for it (near the front of the docs,
  not buried after developer-facing pages).

**Non-Goals:**
- No DockYarp code changes — both technical claims this guide makes (cert reuse, private-CA trust) were
  verified to need none. If a future finding contradicts that, it becomes a separate code item, not folded in
  here.
- Not a full walkthrough of every nginx-proxy feature — link to `openspec/backlog/parity.md` for the exhaustive
  comparison rather than restating it.
- Not reproducing the user's real hostnames, IPs, or domains anywhere in the committed page — every example
  uses placeholder values.

## Decisions

- **New page `migrating-from-nginx-proxy.md`, weight 5** — inserted right after Examples (4) and before
  Architecture (5), shifting Architecture→6, Deployment→7, Contributing→8, Releasing→9. Placement reasons: this
  page's audience is someone *evaluating or switching to* DockYarp, closer in intent to Getting
  Started/Examples than to the developer-facing pages (Contributing, Releasing) it would otherwise land after
  at the end of the nav.
- **One page, two sections (Basic / Advanced)** — mirrors how `examples.md` already groups many recipes on one
  page rather than one-page-per-recipe; the two migration paths share enough structure (compose swap, cert
  handling) to read better together than split.
- **Certificates section is shared by both paths**, not duplicated per-path — the copy/no-conversion/rollback
  story is identical whether the source is public or private ACME.
- **Private-CA section is advanced-path only** — the basic path assumes public Let's Encrypt, where this
  doesn't apply.
- **Content stays pattern-level, not a literal transcript** of the user's real setup — e.g. "a private ACME CA
  reached over HTTP-01" and "backend stacks declare `VIRTUAL_HOST` via environment variables," not their real
  domains or infrastructure layout. The private recap file is grounding material for accuracy, not a source to
  quote from.

## Risks / Trade-offs

- [Advanced-path claims (cert reuse, private-CA trust) are specific enough that a subtly different real-world
  nginx-proxy/acme-companion version could behave differently] → both claims were verified against the
  mechanism (PEM parsing behavior, OpenSSL env var honoring), not a specific acme-companion version's quirks;
  low risk, but the guide should note these as the general mechanism, not a guarantee for every possible
  acme-companion configuration.
- [Page weight renumbering touches four other pages] → mechanical, no content changes to those pages beyond the
  `weight:` front-matter value.
