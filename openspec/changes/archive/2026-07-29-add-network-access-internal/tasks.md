## 1. Model + labels (AG-SEC/AG-DD)
- [x] 1.1 `RouteRule`: add `InternalOnly` (bool)
- [x] 1.2 `DockerLabels`: add `NETWORK_ACCESS`; `ContainerLabelConfig`: add `InternalOnly`
- [x] 1.3 `LabelParser`: parse `NETWORK_ACCESS=internal` → `InternalOnly` in `TryParse` and `ParseCommon`
- [x] 1.4 `ContainerMapper`: thread `InternalOnly` into `RouteRule` on the classic and multiports paths

## 2. Config + middleware (AG-SEC)
- [x] 2.1 `SecurityHeadersOptions`: add `InternalRanges` (CIDR list) with private-range defaults
- [x] 2.2 `NetworkAccessMiddleware`: parse ranges once; for an internal-only route, 403 unless the client IP
      (`RemoteIpAddress`, IPv4-mapped normalized, fail-closed on null) is within a range
- [x] 2.3 Register the middleware (DI) and add it to the pipeline after headers, before HTTPS redirect

## 3. Spec (AG-SEC)
- [x] 3.1 security spec: add an "Internal-only network access" requirement (external 403, internal served,
      custom ranges)

## 4. Tests (AG-SEC)
- [x] 4.1 `NetworkAccessMiddleware`: internal IP served; external IP 403; custom range honored; unknown IP 403;
      non-internal-only route always served
- [x] 4.2 `LabelParser`: `NETWORK_ACCESS=internal` → `InternalOnly` true; absent → false

## 5. Verify (AG-SEC)
- [x] 5.1 Nuke `Test` gate green
