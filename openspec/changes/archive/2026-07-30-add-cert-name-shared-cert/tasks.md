## 1. Model + labels (AG-AT/AG-DD)
- [x] 1.1 `HostTlsMetadata`: add `CertificateName`
- [x] 1.2 `DockerLabels.CertName` (`CERT_NAME`) + `ContainerLabelConfig.CertName`; `LabelParser` parses it
- [x] 1.3 `ContainerMapper`: create TLS metadata when `LETSENCRYPT_HOST` **or** `CERT_NAME` is set; set
      `CertificateName`; `CertificateHost` = LetsEncrypt host or the vhost (classic and multiports)

## 2. Resolver + SNI (AG-AT)
- [x] 2.1 New pure `CertificateNameResolver.Resolve(snapshot, host)` (host-pattern match → `CertificateName`)
- [x] 2.2 `SniCertificateSelector`: inject the routing store + logger; a resolved `CERT_NAME` whose certificate
      exists overrides host lookup; a missing named certificate falls back and warns once per name

## 3. ACME + gating (AG-AT/AG-SEC)
- [x] 3.1 `TlsDomains.Desired`: exclude routes carrying a `CertificateName`
- [x] 3.2 `CertificateAvailabilityAdapter`: a host resolving to a stored `CERT_NAME` is available

## 4. Tests (AG-AT)
- [x] 4.1 `CertificateNameResolver`: host with/without `CERT_NAME`; wildcard host match
- [x] 4.2 `SniCertificateSelector`: `CERT_NAME` override served; missing named certificate falls back
- [x] 4.3 `TlsDomains`: a `CERT_NAME` host is excluded from the desired set
- [x] 4.4 `LabelParser`/`ContainerMapper`: `CERT_NAME` parsed and mapped to `Tls.CertificateName`

## 5. Verify (AG-AT)
- [x] 5.1 Nuke `Test` gate green
