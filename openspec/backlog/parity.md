# nginx-proxy ↔ DockYarp parity matrix

**Source of truth** for feature parity. `docs/architecture.md` keeps only a short summary that links here.
Each gap links to its backlog item in [`items/`](items/). Built from a direct read of the real nginx-proxy
sources (`nginx-proxy-nginx-proxy`, `nginx-proxy-docker-gen`) plus DockYarp's `openspec/specs/` and label
parser.

**Legend:** ✅ implemented · ⚠️ partial / runtime-unvalidated · ⛔ deferred (see item) · ➕ DockYarp extension
· 🚫 non-goal (architectural, see bottom).

> **Re-analysis 2026-07-31** (fresh read of `nginx.tmpl` + `docs/` + docker-gen `internal/`): the tables below
> track *which features exist*; the **Configuration source** section below tracks *how config is read* — a
> distinct axis that surfaced the env-var gap.

## Configuration source

nginx-proxy reads config from a container's **environment variables** (the canonical channel — the `VIRTUAL_*`
family is **env-only**) **and** a separate namespaced **label** set (`com.github.nginx-proxy.nginx-proxy.*`).
docker-gen exposes both `.Env` and `.Labels` with no precedence — the template decides.

| Source | nginx-proxy | DockYarp | Status | Notes / item |
|---|---|---|---|---|
| Container **environment variables** (`-e VIRTUAL_HOST=…`) | canonical for `VIRTUAL_*`, `CERT_NAME`, `NETWORK_ACCESS`, `SERVER_TOKENS`, `EXTERNAL_*_PORT`, per-vhost `HTTPS_METHOD`/`HSTS`/`SSL_POLICY`/`ENABLE_HTTP_ON_MISSING_CERT` | read via per-container inspect; **env wins over label** | ✅ | Live round-trip validated by the Aspire e2e: an env-only backend is routed, and an env var overrides a same-named label. |
| Container **labels** | namespaced `com.github.nginx-proxy.nginx-proxy.*` (loadbalance, keepalive, ssl_verify_client, http2/3, non-get-redirect, trust-default-cert, debug-endpoint) | reads config as labels (nginx-proxy env names + `DOCKYARP_*`) | ⚠️ | DockYarp implements the features via labels but under different names; decide whether to also accept the real nginx-proxy label namespace (see item). |
| Mounted **files** (certs, htpasswd, dhparam, vhost.d, conf.d) | extensive | certs + htpasswd + static JSON config | ⚠️/➕ | DockYarp uses structured config/overrides, not raw nginx files (see Ops row). |

## Routing

| Feature (nginx-proxy) | Status | Notes / item |
|---|---|---|
| `VIRTUAL_HOST`, host/path routing, clusters, load balancing | ✅ | RR / least-requests / power-of-two-choices / random / first-alphabetical. |
| Replica aggregation (one endpoint per container per cluster) | ✅ | |
| Multiple hosts per container (`VIRTUAL_HOST=a,b`) | ✅ | Comma-separated. |
| Host precedence (exact over wildcard) | ✅ | nginx `server_name` semantics; longest path prefix wins. |
| `VIRTUAL_HOST_MULTIPORTS` | ✅ | YAML host→path→{port,proto,dest}. |
| `DEFAULT_HOST` / catch-all + default response | ✅ | Default status configurable. |
| Priority (`DOCKYARP_PRIORITY`) | ➕ | Extension; orders within a host (does not override host specificity). |
| Wildcard host — leading `*.suffix` (any depth) | ✅ | Multi-level: `*.local` matches `app.local` and `a.b.local` (YARP + RouteMatcher). |
| Wildcard host — trailing `foo.bar.*` | ✅ | Custom `MatcherPolicy` (`DockYarpHostMatcherPolicy`) over route metadata. |
| Regex host `~^…$` (`VIRTUAL_HOST`) | ✅ | `MatcherPolicy` over route metadata; compiled/cached regex, ReDoS-bounded. |
| `VIRTUAL_DEST` path rewrite (arbitrary, e.g. `/api`→`/v2`) | ✅ | Strip + prepend via YARP transforms. |
| Regex `VIRTUAL_PATH` (location) | ✅ | `MatcherPolicy` over route metadata; compiled/cached regex, ReDoS-bounded. |
| `DEFAULT_ROOT` arbitrary fallback (return/redirect) | ✅ | Status or templated redirect (`$scheme`/`$host`/`$request_uri`); `Routing:DefaultResponseLocation`. |
| Raw-IP `VIRTUAL_HOST` | ✅ | Bare IPv4 matched exactly; IPv6 literal is a caveat. |
| Client-affinity / `ip_hash` (`loadbalance` label) | ⛔ | YARP has no ip-hash policy; session affinity (+ Data Protection gate) → [`add-session-affinity`](items/add-session-affinity.md). |

## Protocols

| Feature | Status | Notes / item |
|---|---|---|
| `VIRTUAL_PROTO` http/https | ✅ | |
| WebSocket | ✅ | YARP default. |
| HTTP/2 | ✅ | Configurable protocols. |
| `VIRTUAL_PROTO=grpc` | ✅ | grpc/grpcs → HTTP/2-exact cluster (trailers forwarded). E2E round-trip: → [`e2e-grpc-passthrough`](items/e2e-grpc-passthrough.md). |
| `VIRTUAL_PROTO` fastcgi / uwsgi | 🚫 | Non-HTTP upstreams — out of scope. |
| HTTP/3 (QUIC) | ⚠️ | Config toggle; needs MsQuic runtime → [`finish-http3`](items/finish-http3.md). |

## TLS

| Feature | Status | Notes / item |
|---|---|---|
| ACME HTTP-01, renewal, SNI, self-signed fallback | ✅ ➕ | HTTPS on 8443. **Note**: in the nginx-proxy world ACME issuance + `LETSENCRYPT_HOST/EMAIL` are the separate **acme-companion** container; DockYarp does this in-process → superset. |
| ACME HTTP-01 challenge options (`ACME_HTTP_CHALLENGE_LOCATION`/accept-unknown-host) | ⛔ | Challenge served, always-on → [`add-acme-challenge-options`](items/add-acme-challenge-options.md). |
| Provided certs (PEM/PFX), wildcard-parent selection | ✅ | |
| `HTTPS_METHOD` (redirect/noredirect/nohttp/nohttps) | ✅ | Redirect gated on real cert availability. |
| HSTS (preload + per-host `HSTS`) | ✅ | |
| SNI certificate selection | ✅ | Validated by the Aspire e2e (step-ca). |
| Min TLS version / ciphers / protocols | ⚠️ | Wired as config; cipher allow-list Linux/macOS only. |
| `CERT_NAME` shared/SAN cert | ✅ | `CERT_NAME` pins a named shared certificate in SNI selection; the host is not ACME-provisioned. |
| `default.crt` + `TRUST_DEFAULT_CERT` + `ENABLE_HTTP_ON_MISSING_CERT` | ✅ | Operator `default.crt`/`.key` preferred; `Security:TrustDefaultCert` (500 on untrusted) + `Security:EnableHttpOnMissingCert`. |
| `SSL_POLICY` presets — global default | ✅ | `Tls:SslPolicy` Mozilla Modern/Intermediate/Old → version + ciphers. Live negotiation: → [`e2e-ssl-policy-negotiation`](items/e2e-ssl-policy-negotiation.md). |
| `SSL_POLICY` per-vhost override (`-e`/label) | ⚠️ | Recognized per-container + applied per-SNI (protocols + ciphers), unit-tested; live per-host negotiation → [`e2e-ssl-policy-negotiation`](items/e2e-ssl-policy-negotiation.md) (flips ✅ when green). |
| OCSP stapling (`.chain.pem`) | ⛔ | → [`add-ocsp-stapling`](items/add-ocsp-stapling.md). |
| ACME DNS-01 | ⛔ | HTTP-01 only → [`add-acme-dns01`](items/add-acme-dns01.md). |
| DH params (`DHPARAM_*`, per-vhost) | ⛔ | → [`add-dhparam-config`](items/add-dhparam-config.md). |

## mTLS / Auth / Access control

| Feature | Status | Notes / item |
|---|---|---|
| Mutual TLS (`DOCKYARP_CLIENT_CERT` + CA), per-host enforcement | ✅ | CA validation + enforcement; handshake proven by the Aspire e2e (step-ca). |
| Basic Auth (`DOCKYARP_AUTH_*` labels) | ✅ | Label-based. |
| `ssl_verify_client=optional` passthrough + CRL + global CA | ⛔ | CRL/optional-passthrough missing → [`add-mtls-optional-crl`](items/add-mtls-optional-crl.md). |
| htpasswd files (per vhost + per path) | ✅ | bcrypt / apr1 / SHA1; `Security:HtpasswdDirectory`; hot-reloaded. |
| `NETWORK_ACCESS=internal` (403 for external clients) | ✅ | Per-route client-IP gate; configurable `Security:InternalRanges`. |

## Headers & proxying

| Feature | Status | Notes / item |
|---|---|---|
| `X-Forwarded-*` / `X-Real-IP` / Host / downstream-proxy trust | ✅ | |
| `client_max_body_size` (`DOCKYARP_MAX_BODY_SIZE`) | ✅ | Per-route. |
| Proxy timeouts (`DOCKYARP_PROXY_TIMEOUT`) | ✅ | Per-cluster activity timeout. |
| Backend keepalive / connection pooling (`keepalive` label) | ⛔ | YARP default pooling; no per-cluster override → [`add-backend-keepalive`](items/add-backend-keepalive.md). |
| gzip response compression | ✅ | gzip + brotli, on by default; `Compression:Enabled`. |
| httpoxy mitigation (strip inbound `Proxy` header) | ✅ | Stripped in the forwarded-headers transform. |
| PROXY protocol (`ENABLE_PROXY_PROTOCOL`) + real client IP | ⛔ | → [`add-proxy-protocol`](items/add-proxy-protocol.md). |
| `NON_GET_REDIRECT` (307/308 for non-GET) | ✅ | DockYarp redirects with 308 (method-preserving) for all methods; no separate knob needed. |
| Response buffering | 🚫 | YARP streams by design. |

## Discovery & network

| Feature | Status | Notes / item |
|---|---|---|
| Network selection (preferred network, skip Swarm `ingress`) | ✅ | Deterministic. |
| Health-aware (exclude unhealthy/starting; react to `health_status`) | ✅ | |
| `DOCKER_CONTAINER_FILTERS` (scope discovery) | ✅ | `Docker:ContainerFilters` (map key→values) applied to the authoritative container listing. |
| Host-network-mode backends | ✅ | `Docker:HostAddress` targets the host on `VIRTUAL_PORT`; skipped with a warning if unset. Live reachability: → [`e2e-host-network-mode`](items/e2e-host-network-mode.md). |
| IPv6 listeners (`ENABLE_IPV6`) + `PREFER_IPV6_NETWORK` | ⛔ | → [`add-ipv6-support`](items/add-ipv6-support.md). |
| Multi-network attach / unreachable-network resilience | ✅ | `Docker:ProxyNetworks` → reachability-aware selection; unreachable backends skipped. Runtime auto-detection: → [`e2e-multi-network`](items/e2e-multi-network.md). |
| Docker Swarm services | ⛔ ➕ | **Beyond nginx-proxy**: docker-gen only exposes *classic-swarm* node metadata (`container.Node`), never Swarm-mode services/tasks. Swarm-mode support would be a DockYarp extension → [`add-docker-swarm-support`](items/add-docker-swarm-support.md). |
| Remote daemon over TLS (`DOCKER_HOST`+`DOCKER_TLS_VERIFY`/`CERT_PATH`) | ⛔ | Endpoint URI only; no client-cert/CA/verify → [`add-docker-daemon-tls`](items/add-docker-daemon-tls.md). |
| Event debounce (docker-gen `-wait`) | ⛔ | Reconcile per event; coalesce bursts → [`add-reconcile-debounce`](items/add-reconcile-debounce.md). |
| `RESOLVERS` (custom DNS) | 🚫 | .NET resolves DNS; not applicable. |
| Custom external ports (`HTTP_PORT`/`EXTERNAL_*_PORT`) | ⛔ | Listener config → [`add-proxy-protocol`](items/add-proxy-protocol.md) tracks the listener rework; port config folded there. |

## Ops & extensibility

| Feature | Status | Notes / item |
|---|---|---|
| Static configuration source (file) | ✅ | JSON variant, merged with precedence. |
| Custom error pages | ✅ | DockYarp-generated errors. |
| Access logging (structured + JSON + disable) | ✅ | `LOG_JSON`/`DISABLE_ACCESS_LOGS` covered. |
| Admin API (`/api/*`) + Prometheus `/metrics` | ✅ | |
| Graceful shutdown | ✅ | |
| `vhost.d`-style per-vhost/global config + route override | ✅ | Structured overrides: per-host/`default` response headers + static-route replacement (not raw nginx; request-headers/location = future structured additions). |
| `SERVER_TOKENS` (hide/adjust `Server`) | ✅ | Suppressed by default; global `Security:ServerHeader` for a custom value; per-host `SERVER_TOKENS=off` opts a host out. |
| `DEBUG_ENDPOINT`-style config dump | ✅ | Admin `GET /api/resolve?host=&path=` returns the effective route/transforms/TLS/security/cluster (API-key protected). |
| Custom `LOG_FORMAT` template | ✅ | `AccessLog:Fields` selects an ordered field set (structured analog of `LOG_FORMAT`). |

## Non-goals (architectural — deliberately not ported)

- **fastcgi / uwsgi upstreams** — not HTTP; outside a YARP L7 reverse proxy.
- **TCP/UDP `stream {}` (L4) proxying** — YARP is L7.
- **Split docker-gen / nginx container mode** — DockYarp is a single .NET process; the split exists only to
  keep the Docker socket off nginx, which the socket-proxy already solves for us.
- **`RESOLVERS`** — DNS resolution is handled by .NET, not an nginx `resolver` directive.
- **Response buffering to disk** — YARP streams by default (a feature, not a gap).
