## Why
nginx-proxy's `SSL_POLICY` is **DUAL**: a global default on the proxy container **and** a per-vhost override set
as an env var/label on the backend container. DockYarp implements only the **global** posture (`Tls:SslPolicy`);
a container that sets `-e SSL_POLICY=Mozilla-Modern` on itself is ignored. This closes that per-container
env-var-compatibility gap, on top of the per-connection TLS assembly point introduced by
`add-tls-handshake-callback`.

## What Changes
- `SSL_POLICY` becomes a recognized per-container key (environment variable or label; environment wins, via the
  existing `EffectiveConfig` merge). It is parsed into the route's per-host TLS metadata.
- During the SNI handshake, a host that declares a recognized `SSL_POLICY` preset negotiates with that preset's
  minimum TLS version and cipher policy, **overriding** the global posture; other hosts keep the global posture.
- An unrecognized per-host `SSL_POLICY` is ignored (the global posture applies) with a one-time diagnostic.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `tls-acme`: the per-connection TLS posture (minimum version + ciphers) can be overridden per host by a
  recognized `SSL_POLICY` preset, defaulting to the global posture.

## Impact
- **Code**: `DockYarp.Docker` — `DockerLabels.SslPolicy`; `ContainerLabelConfig.SslPolicy`; `LabelParser`
  reads it (both `TryParse` and `ParseCommon`); `ContainerMapper` carries it into `HostTlsMetadata`.
  `DockYarp.Core` — `HostTlsMetadata.SslPolicy`. `DockYarp.Tls` — `SslPolicyPresets` exposes its preset names;
  new `HostSslPolicyResolver`; `SniTlsHandshakeCallback` resolves the per-host preset (precomputed prepared
  policies; warn-once on unknown).
- **Tests (unit)**: `LabelParser` parses `SSL_POLICY` (env + label, env wins); `ContainerMapper` carries it;
  `HostSslPolicyResolver` matches by host; `SniTlsHandshakeCallback` applies the per-host preset and falls back
  to global for an absent/unknown value.
- **Runtime / e2e**: live per-host negotiation validated by extending [`e2e-ssl-policy-negotiation`] with a
  two-host case (different policies, one Kestrel instance). Cipher enforcement stays Linux/macOS-only; the
  per-host protocol floor works cross-platform.
- **Scope**: per-host `SSL_POLICY` is honored for hosts that are TLS-configured (`LETSENCRYPT_HOST` or
  `CERT_NAME`), consistent with how the existing per-host `HSTS`/`HTTPS_METHOD` attributes are carried; it does
  not create an ACME certificate desire.
- **Owning agent**: AG-AT. Resolves `add-per-vhost-ssl-policy`.
