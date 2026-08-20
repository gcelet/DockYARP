## Context

See `proposal.md` — Why, and the resolved gating question (asked explicitly): the user chose a dedicated
opt-in (`AdminApi:AllowCertificateConversion`), separate from `AllowCertificateDownload`, specifically because
this is the first *mutating* action ever added to the admin surface — not because it shares download's
secret-exposure risk (it doesn't expose anything new; the private key involved was already reachable via
volume access regardless of on-disk format).

Confirmed by reading the code:
- `FileCertificateStore.Save(host, certificate)` (post `change-cert-store-format-to-pem`) always writes PEM and
  replaces the in-memory `certificates[host]` entry, disposing whatever was previously there. Reusing `Save()`
  directly for "rewrite the SAME already-loaded certificate" is a real trap: `Save()`'s remove-then-dispose
  logic would dispose the very `LoadedCertificate` object being passed back in (same reference), leaving the
  in-memory entry backed by disposed `X509Certificate2` instances — the next SNI lookup for that host would
  throw `ObjectDisposedException`. The conversion path must NOT go through `Save()`.
- `ICertificateStore`/`FileCertificateStore` have no existing way to report which on-disk format currently
  backs a host's certificate — `CertificateInfo` carries only host + expiry.
- `AllowCertificateDownload`'s dashboard routes (`add-admin-dashboard-cert-download`) are plain `GET` minimal-API
  endpoints — appropriate there because downloading doesn't change server state. A mutating action needs a
  different HTTP verb and CSRF protection, which Razor Pages' `OnPost` handlers get automatically via the
  built-in anti-forgery token (emitted by the `<form>` tag helper, validated by the framework before the
  handler runs) — no extra plumbing needed, unlike the minimal-API routes used for download.

## Goals / Non-Goals

**Goals:**
- Convert an already-loaded, already-valid `.pfx`-backed certificate to the canonical PEM pair with no ACME
  round-trip and no change to what's served.
- Never dispose or otherwise invalidate the in-memory certificate object while doing so — the store's existing
  SNI-serving path must be unaffected by a conversion happening concurrently or immediately before/after.
- CSRF-safe by construction (POST + anti-forgery token), not an afterthought.

**Non-Goals:**
- The reverse direction (PEM → PFX) — not requested; the user's concrete need is exactly "normalize a leftover
  PFX," not general bidirectional format choice. `add-admin-dashboard-cert-download`'s scope already covers
  "get a cert out of DockYarp in PEM"; nothing here adds a PFX *export* capability.
- Uploading/importing an external certificate file — the motivating case is entirely about certificates DockYarp
  already manages (loaded in memory), not files from outside the store. See the backlog item's own note: an
  external PFX either loads into DockYarp as-is (`Load()` already accepts `.pfx` directly) or gets converted
  with `openssl` locally — no product feature needed for that case.
- A configurable default write format — considered and rejected in the same conversation this item came from;
  no concrete driver for it, and it would reintroduce the ambiguity `change-cert-store-format-to-pem` just
  removed.
- Encryption-at-rest for the private key (passphrase) — a real, separate idea from the same conversation,
  tracked as its own backlog item (`add-tls-private-key-encryption`), not folded in here.

## Decisions

**`ICertificateStore` gains `IsPfxBacked(string host)` as a live filesystem check, not new in-memory tracking.**

Rationale: `FileCertificateStore` already knows the certificate directory and the `{host}.pfx` naming
convention (`PathFor`); checking `fileSystem.File.Exists(PathFor(host, ".pfx")) &&
!fileSystem.File.Exists(PathFor(host, ".crt"))` is a couple of lines, always reflects the true current disk
state (no risk of a stale in-memory flag drifting from reality), and needs no changes to `Load()`'s existing
two-pass merge logic. Considered and rejected: tracking format in a parallel dictionary updated alongside
`Load()`/`Save()` — more state to keep consistent for no accuracy benefit over a direct filesystem check, which
is cheap (this is called once per dashboard page render per stored host, not a hot path).

**`ICertificateStore.ConvertToPem(string host)` writes PEM files directly from the already-loaded certificate
and deletes the stale `.pfx` — it does NOT call `Save()`.**

Rationale: as noted in Context, `Save()`'s dispose-the-previous-entry logic is unsafe when the "new" and "old"
certificate are the *same object* (exactly the case here — nothing about the certificate itself changes, only
its on-disk serialization). `ConvertToPem` reuses the `LoadedCertificatePem` extension methods
(`ExportChainPem`/`ExportPrivateKeyPem`, from `add-admin-dashboard-cert-download`) to write `{host}.crt`/`.key`
directly, then deletes `{host}.pfx` if present — no touch to the in-memory `certificates` dictionary at all,
since the object reference is unchanged. Returns `false` (no-op) when the host isn't found, mirroring `Find`'s
nullable-return shape rather than throwing for an ordinary "not present" case.

**The dashboard action is a Razor Pages `OnPost` handler on the existing `IndexModel`, not a new minimal-API
route under `DashboardEndpointMapping` (unlike download).**

Rationale: this is the one place in the admin surface where CSRF actually matters — a mutating action reachable
by a same-network browser needs protection against a forged cross-site request, and Razor Pages' `<form
method="post">` tag helper + built-in anti-forgery validation gives this for free, without hand-rolling a token
scheme for a single action. `add-admin-dashboard-cert-download`'s plain `GET` links were correct for that
feature (no state change, nothing to forge meaningfully) — reusing that same shape here would remove the exact
protection this action needs. `OnPostConvertAsync(string host)` checks
`AdminApiOptions.AllowCertificateConversion` itself (defense in depth: the option gates both whether the form
renders in the view AND whether the handler honors an invocation, not just the UI).

**`ICertificateConverter` (new, `DockYarp.AdminApi`) mirrors `ICertificateExporter`'s adapter pattern, kept as
its own interface rather than added to an existing one.**

Rationale: same reasoning as `ICertificateExporter` vs. `ICertificateInventory` — a distinct capability
(mutating, format-only) deserves a distinct, narrowly-scoped interface rather than growing an existing one's
surface with a concern it wasn't designed around.

## Correction, found live during implementation (not in the original design)

`ConvertToPem`'s first real test run failed with `CryptographicException: The requested operation is not
supported` — a certificate loaded from `.pfx` via `X509CertificateLoader.LoadPkcs12Collection` imports its
private key into a non-exportable key store by default on some platform PALs (confirmed: CNG on Windows), so
re-exporting it as PEM always fails, not just intermittently. This was a **latent gap in
`CertificateCollectionLoader.LoadKeyed`** that predates this change — nothing had ever needed to re-export a
PFX-loaded key before `ConvertToPem` existed. Fixed at the source: `LoadKeyed` now passes
`X509KeyStorageFlags.Exportable` to `LoadPkcs12Collection` (verified against Microsoft's own docs before using
it). This also quietly benefits the pre-existing PFX read path in general, not just conversion.

## Risks / Trade-offs

- [Risk] A conversion could race with a concurrent renewal for the same host (the provisioning service calling
  `Save()` while `ConvertToPem` is mid-write). → Mitigation: both paths ultimately write the *same* PEM content
  for a given `LoadedCertificate` state (chain + key), and `FileCertificateStore` already serializes its own
  mutations under its internal `gate` lock for the in-memory dictionary; `ConvertToPem`'s file writes should
  take the same lock around the read-then-write sequence to avoid reading a certificate that's being replaced
  mid-conversion. Worst case without this would be a harmless double-write, not corruption, but the design
  takes the lock anyway since it's cheap and removes the ambiguity.
- [Risk] Deleting the stale `.pfx` after writing PEM could leave neither file present if the process is killed
  between the two writes and the delete. → Accepted: write `.crt`, then `.key`, then delete `.pfx` last — the
  only bad window is between the two writes (a partially-written pair), which `Load()` already handles safely
  today (a `.crt` without a matching `.key` is skipped, falling back to whatever `.pfx` still exists at that
  point since it hasn't been deleted yet) — the ordering itself is the mitigation, not a new safeguard.
- [Risk] This is more implementation surface than the "just delete the file yourself" workaround the user
  already knows about. → Accepted per the user's explicit ask; the value is skipping a real ACME round-trip for
  an already-valid certificate, which the manual workaround cannot avoid.
