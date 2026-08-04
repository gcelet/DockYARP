# Design — add-backend-keepalive

## Which knob to expose
The backlog item leaves the choice open ("idle timeout vs max connections vs lifetime"). YARP 2.3.0's
**declarative** `HttpClientConfig` surface is narrow: `SslProtocols`, `DangerousAcceptAnyServerCertificate`,
`MaxConnectionsPerServer`, `EnableMultipleHttp2Connections`, `WebProxy`, header encodings. It does **not**
expose `PooledConnectionIdleTimeout`/`Lifetime` (those live on `SocketsHttpHandler` and would require a custom
`IForwarderHttpClientFactory`, out of scope). The one clean, declarative pool-tuning knob is
`MaxConnectionsPerServer` — so that is what this change exposes.

## Not a 1:1 nginx port
nginx `keepalive N` bounds the number of *idle* keep-alive connections retained for reuse (default `auto`
≈ 2× server count). YARP already keeps backend connections alive and reuses them via `SocketsHttpHandler`'s
pool, with no fixed idle-pool size to configure. Mapping nginx's small `keepalive` count onto
`MaxConnectionsPerServer` would be a footgun (it would cap *total* concurrency, not idle retention), so this
change instead exposes an explicit, correctly-named **connection cap**. Documented as such; the namespaced
`keepalive` label is **not** aliased to it.

## Data path (mirrors ProxyTimeout -> Cluster.RequestTimeout)
```
DockerLabels.MaxConnections ("DOCKYARP_MAX_CONNECTIONS")
  -> LabelParser: MaxConnectionsPerServer = ParsePositiveInt(config)   (int > 0, env wins via EffectiveConfig)
  -> ContainerLabelConfig.MaxConnectionsPerServer
  -> ContainerMapper: Cluster { MaxConnectionsPerServer = first/common.MaxConnectionsPerServer }  (both blocks)
  -> YarpConfigMapper.BuildCluster: HttpClient = BuildHttpClientConfig(cluster)
```

## Mapping
```
BuildHttpClientConfig(cluster) =
    cluster.MaxConnectionsPerServer is { } max
        ? new HttpClientConfig { MaxConnectionsPerServer = max }
        : null                       // unset -> no HttpClient override, YARP defaults unchanged
```
`ClusterConfig.HttpClient` stays `null` unless a knob is set, so existing clusters are byte-for-byte unchanged.

## Parsing + diagnostic
- `ParsePositiveInt`: `int` `> 0`, else `null` (ignored) — mirrors `ParsePositiveLong` for `MAX_BODY_SIZE`.
- `HasInvalidMaxConnections`: present but not a positive integer → a warning via `AddCommonWarnings`
  (matches the `DOCKYARP_PROXY_TIMEOUT` / `DOCKYARP_MAX_BODY_SIZE` / `EXTERNAL_HTTPS_PORT` diagnostics). An
  ignored/invalid value leaves YARP's default pooling.

## Out of scope
- `EnableMultipleHttp2Connections`, idle timeout / connection lifetime (would need a custom client factory).
- Any change to routing, health, or the existing timeout/body-size cluster knobs.
