## ADDED Requirements

### Requirement: Certificate download from the dashboard
The system SHALL support downloading a stored certificate's public material (`{host}.crt`, the leaf plus any
chain) and its private key (`{host}.key`) from the admin dashboard, gated by an explicit opt-in setting
`AdminApi:AllowCertificateDownload` (default `false`). When `false`, no download route SHALL be mapped and no
download link SHALL be rendered on `/dashboard` — the same "not mapped when disabled" pattern the dashboard
itself already follows for `AdminApi:Surface`. When `true`, the download routes SHALL be reachable only under
the same host-isolation boundary as the rest of the dashboard (`AdminApi:Host`), not routed through the
API-key-protected `/api/*` surface — a browser-initiated download SHALL NOT require the admin API key to reach
the browser, preserving the existing "no admin API key in the delivered HTML/JavaScript" guarantee. Requesting
a download for a host with no stored certificate SHALL return 404, not an error page or empty file.

#### Scenario: Disabled by default, nothing exposed
- **WHEN** `AdminApi:AllowCertificateDownload` is left at its default (`false`)
- **THEN** no certificate download route responds, and `/dashboard`'s certificate table shows no download link

#### Scenario: Downloading the public certificate
- **WHEN** `AdminApi:AllowCertificateDownload` is `true` and an operator downloads a stored certificate for a
  known host
- **THEN** they receive `{host}.crt` as a PEM file attachment containing the leaf and any chain certificates

#### Scenario: Downloading the private key
- **WHEN** `AdminApi:AllowCertificateDownload` is `true` and an operator downloads the private key for a known
  host
- **THEN** they receive `{host}.key` as a PEM file attachment

#### Scenario: Download follows the dashboard's host isolation
- **WHEN** `AdminApi:AllowCertificateDownload` is `true` and `AdminApi:Host` is set
- **THEN** the download routes respond only on the admin host, the same way `/dashboard` itself does

#### Scenario: Download never requires the admin API key in the browser
- **WHEN** an operator downloads a certificate or private key from the dashboard
- **THEN** the request succeeds without any admin API key being present in the page, a cookie, or a header the
  browser had to be given

#### Scenario: Unknown host returns 404
- **WHEN** a download is requested for a host with no stored certificate
- **THEN** the response is 404, not a server error or an empty/malformed file
