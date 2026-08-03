## 1. Recognize the key (AG-DD)
- [x] 1.1 `DockerLabels.ExternalHttpsPort = "EXTERNAL_HTTPS_PORT"`
- [x] 1.2 `ContainerLabelConfig.ExternalHttpsPort` (int?)
- [x] 1.3 `LabelParser`: `ParseExternalPort` (1..65535 → int?, else null) in `TryParse` + `ParseCommon`;
      `HasInvalidExternalHttpsPort` diagnostic wired into `AddCommonWarnings`

## 2. Carry it per host (AG-DD / AG-SEC)
- [x] 2.1 `HostTlsMetadata.ExternalHttpsPort` (int?)
- [x] 2.2 `ContainerMapper`: set `ExternalHttpsPort` in both `HostTlsMetadata` blocks (classic + multiports)

## 3. Apply it (AG-SEC)
- [x] 3.1 `HttpsRedirectionMiddleware`: build the redirect authority with `tls.ExternalHttpsPort` (omit at 443)

## 4. Tests (AG-SEC / AG-DD)
- [x] 4.1 `LabelParser`: `EXTERNAL_HTTPS_PORT` parsed; invalid value → null + `HasInvalidExternalHttpsPort`
- [x] 4.2 `ContainerMapper`: a certified host carries it into `Tls.ExternalHttpsPort`
- [x] 4.3 `HttpsRedirectionMiddleware`: redirect targets `https://host:8443/…`; the default 443 stays omitted
      (existing redirect tests cover the no-port case)

## 5. Verify (AG-SEC)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker) — 317 tests, 0 failures
