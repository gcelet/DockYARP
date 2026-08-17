namespace DockYarp.E2E.AppHost;

using System;
using System.Collections.Generic;

/// <summary>The labeled backend containers DockYarp must discover during the end-to-end run.</summary>
/// <remarks>
/// Each backend carries DockYarp labels as real Docker labels so discovery reads them exactly as in
/// production. Virtual hosts are distinct per scenario (except <c>priority.local</c>, shared on purpose to
/// exercise route precedence), so all backends can coexist under a single proxy instance.
/// </remarks>
internal static class BackendCatalog
{
    /// <summary>The echo backend image built by <c>dotnet publish -t:PublishContainer</c> before the run.</summary>
    internal const string EchoImage = "dockyarp-e2e-backend";

    /// <summary>The gRPC echo backend image (HTTP/2 h2c) built the same way, for the <c>VIRTUAL_PROTO=grpc</c> scenario.</summary>
    internal const string GrpcImage = "dockyarp-e2e-grpc-backend";

    /// <summary>The virtual host routed to the default backend when no other host matches.</summary>
    internal const string DefaultHost = "default.local";

    private const string WhoamiImage = "traefik/whoami";
    private const string EchoTag = "local";
    private const string EchoPort = "8080";
    private const string EchoUrlsKey = "ASPNETCORE_URLS";
    private const string BackendIdKey = "BACKEND_ID";
    private const string HttpScheme = "http";

    private const string VirtualHost = "VIRTUAL_HOST";
    private const string VirtualPort = "VIRTUAL_PORT";
    private const string VirtualPath = "VIRTUAL_PATH";
    private const string VirtualDest = "VIRTUAL_DEST";
    private const string VirtualHostMultiports = "VIRTUAL_HOST_MULTIPORTS";
    private const string VirtualProto = "VIRTUAL_PROTO";
    private const string AuthUser = "DOCKYARP_AUTH_USER";
    private const string AuthPassword = "DOCKYARP_AUTH_PASSWORD";
    private const string MaxBodySize = "DOCKYARP_MAX_BODY_SIZE";
    private const string ProxyTimeout = "DOCKYARP_PROXY_TIMEOUT";
    private const string LetsEncryptHost = "LETSENCRYPT_HOST";
    private const string Hsts = "HSTS";
    private const string ClientCert = "DOCKYARP_CLIENT_CERT";
    private const string SslPolicy = "SSL_POLICY";
    private const string DockYarpHttp2 = "DOCKYARP_HTTP2";

    /// <summary>Gets every backend the AppHost adds to the distributed system.</summary>
    public static IReadOnlyList<BackendSpec> All { get; } =
    [
        new BackendSpec
        {
            Name = "whoami", // basic discovery + forwarded headers (off-the-shelf whoami on port 80)
            Image = WhoamiImage,
            Labels = [Kv(VirtualHost, "whoami.local"), Kv(VirtualPort, "80")],
        },
        new BackendSpec
        {
            Name = "whoami-multihost", // multi-host: one container exposed under two virtual hosts
            Image = WhoamiImage,
            Labels = [Kv(VirtualHost, "a.local,b.local"), Kv(VirtualPort, "80")],
        },
        new BackendSpec
        {
            Name = "echo-path", // path rewrite: /api/* forwarded with the /api prefix stripped (VIRTUAL_DEST=/)
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "echo.local"),
                Kv(VirtualPort, EchoPort),
                Kv(VirtualPath, "/api"),
                Kv(VirtualDest, "/"),
            ],
            Environment = EchoEnv(EchoPort, id: null),
        },
        new BackendSpec
        {
            Name = "echo-multiport", // multi-port: one container on two ports, routed per path
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHostMultiports, "{multiport.local: {/: {port: 8080}, /api: {port: 8081}}}")],
            Environment = EchoEnv("8080;8081", id: null),
        },
        new BackendSpec
        {
            Name = "echo-env", // config source = environment variables only (VIRTUAL_HOST/PORT as env, no labels)
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [],
            Environment = EchoEnvRouted(EchoPort, id: "env", virtualHost: "env.local", virtualPort: EchoPort),
        },
        new BackendSpec
        {
            // env wins over a same-named label: the container is routed under the env VIRTUAL_HOST, not the label's.
            Name = "echo-env-override",
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "envlabel.local")],
            Environment = EchoEnvRouted(EchoPort, id: "envwin", virtualHost: "envwins.local", virtualPort: EchoPort),
        },
        new BackendSpec
        {
            Name = "echo-auth", // Basic Auth: 401 without credentials, 200 with
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "auth.local"),
                Kv(VirtualPort, EchoPort),
                Kv(AuthUser, "alice"),
                Kv(AuthPassword, "secret"),
            ],
            Environment = EchoEnv(EchoPort, id: null),
        },
        new BackendSpec
        {
            Name = "echo-limits", // proxy tuning: oversized body rejected, slow response cancelled
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "limits.local"),
                Kv(VirtualPort, EchoPort),
                Kv(MaxBodySize, "1024"),
                Kv(ProxyTimeout, "1"),
            ],
            Environment = EchoEnv(EchoPort, id: null),
        },
        new BackendSpec
        {
            Name = "echo-default", // default host: unknown hosts route here (Routing__DefaultHost)
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, DefaultHost), Kv(VirtualPort, EchoPort)],
            Environment = EchoEnv(EchoPort, id: "default"),
        },
        new BackendSpec
        {
            Name = "echo-unhealthy", // health-aware: a deliberately unhealthy container is excluded from routing
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "unhealthy.local"), Kv(VirtualPort, EchoPort)],
            Environment = EchoEnv(EchoPort, id: "unhealthy"),
            ExtraRuntimeArgs =
                ["--health-cmd", "exit 1", "--health-interval", "2s", "--health-retries", "1", "--health-timeout", "1s"],
            WaitForRunning = false,
        },
        new BackendSpec
        {
            Name = "echo-tls", // ACME: a certificate is provisioned for LETSENCRYPT_HOST and served over HTTPS
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "tls.local"), Kv(VirtualPort, EchoPort), Kv(LetsEncryptHost, "tls.local")],
            Environment = EchoEnv(EchoPort, id: "tls"),
        },
        new BackendSpec
        {
            Name = "echo-hsts", // per-host HSTS: the HTTPS response carries Strict-Transport-Security
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "hsts.local"),
                Kv(VirtualPort, EchoPort),
                Kv(LetsEncryptHost, "hsts.local"),
                Kv(Hsts, "max-age=31536000"),
            ],
            Environment = EchoEnv(EchoPort, id: "hsts"),
        },
        new BackendSpec
        {
            Name = "echo-mtls", // mutual TLS: a valid client certificate is required
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "mtls.local"),
                Kv(VirtualPort, EchoPort),
                Kv(LetsEncryptHost, "mtls.local"),
                Kv(ClientCert, "required"),
            ],
            Environment = EchoEnv(EchoPort, id: "mtls"),
        },
        new BackendSpec
        {
            // per-vhost HTTP/2 disable: DOCKYARP_HTTP2=false restricts ALPN so this host negotiates HTTP/1.1 only.
            Name = "echo-http1",
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "http1.local"),
                Kv(VirtualPort, EchoPort),
                Kv(LetsEncryptHost, "http1.local"),
                Kv(DockYarpHttp2, "false"),
            ],
            Environment = EchoEnv(EchoPort, id: "http1"),
        },
        new BackendSpec
        {
            Name = "echo-sslpolicy", // per-vhost SSL_POLICY: Mozilla-Modern floors this host at TLS 1.3
            Image = EchoImage,
            Tag = EchoTag,
            Labels =
            [
                Kv(VirtualHost, "modern.local"),
                Kv(VirtualPort, EchoPort),
                Kv(LetsEncryptHost, "modern.local"),
                Kv(SslPolicy, "Mozilla-Modern"),
            ],
            Environment = EchoEnv(EchoPort, id: "sslpolicy"),
        },
        new BackendSpec
        {
            // Operator-provided (non-ACME) full-chain PEM: TlsHarness mounts pem.local.crt/.key (leaf +
            // intermediate) into the certs directory before DockYarp starts. No LETSENCRYPT_HOST — this host
            // must never be provisioned by ACME, only served from the mounted chain.
            Name = "echo-pem",
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "pem.local"), Kv(VirtualPort, EchoPort)],
            Environment = EchoEnv(EchoPort, id: "pem"),
        },
        new BackendSpec
        {
            // gRPC passthrough: VIRTUAL_PROTO=grpc → an HTTP/2-exact cluster. Served over the HTTPS listener
            // (gRPC needs h2, and DockYarp's HTTP listener is HTTP/1 only) with the default certificate — no
            // LETSENCRYPT_HOST, so the scenario does not depend on ACME timing. The backend speaks h2c.
            Name = "echo-grpc",
            Image = GrpcImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "grpc.local"), Kv(VirtualPort, EchoPort), Kv(VirtualProto, "grpc")],
            Environment = EchoEnv(EchoPort, id: null),
        },
    ];

    private static string Kv(string key, string value) => key + "=" + value;

    private static Dictionary<string, string> EchoEnv(string listenPorts, string? id)
    {
        string[] ports = listenPorts.Split(';');
        string[] urls = new string[ports.Length];
        for (int index = 0; index < ports.Length; index++)
        {
            // Container-internal listener; plain HTTP is intentional (TLS, when present, terminates at DockYarp).
            urls[index] = HttpScheme + "://+:" + ports[index];
        }

        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            [EchoUrlsKey] = string.Join(';', urls),
        };
        if (id is not null)
        {
            environment[BackendIdKey] = id;
        }

        return environment;
    }

    /// <summary>Echo env plus <c>VIRTUAL_HOST</c>/<c>VIRTUAL_PORT</c> set as environment variables (not labels).</summary>
    private static Dictionary<string, string> EchoEnvRouted(
        string listenPorts, string id, string virtualHost, string virtualPort)
    {
        Dictionary<string, string> environment = EchoEnv(listenPorts, id);
        environment[VirtualHost] = virtualHost;
        environment[VirtualPort] = virtualPort;
        return environment;
    }
}
