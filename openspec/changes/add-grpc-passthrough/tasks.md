## 1. Model + parsing (AG-RP/AG-DD)
- [x] 1.1 `Cluster`: add `Http2Only` (bool)
- [x] 1.2 `ContainerLabelConfig`: add `Http2`
- [x] 1.3 `LabelParser`: `grpc`→Http+Http2, `grpcs`→Https+Http2, `https`→Https, else Http; recognize
      `grpc`/`grpcs` in `HasUnsupportedProto`
- [x] 1.4 `ContainerMapper` (classic cluster): set `Cluster.Http2Only` from the parsed config

## 2. YARP mapping (AG-RP)
- [x] 2.1 `YarpConfigMapper.BuildCluster`: when `Http2Only`, set `HttpRequest.Version = 2.0` and
      `VersionPolicy = RequestVersionExact` (combined with any request timeout)

## 3. Split runtime validation (AG-RP)
- [x] 3.1 New backlog item `e2e-grpc-passthrough` (gRPC backend fixture + unary/streaming round-trip)

## 4. Tests (AG-RP)
- [x] 4.1 `LabelParser`: grpc/grpcs → scheme + http2; grpc/grpcs not "unsupported"
- [x] 4.2 `YarpConfigMapper`: an http2 cluster maps to `Version 2.0` + `RequestVersionExact`

## 5. Verify (AG-RP)
- [x] 5.1 Nuke `Test` gate green
