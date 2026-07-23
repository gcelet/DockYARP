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
    private const string Priority = "DOCKYARP_PRIORITY";
    private const string AuthUser = "DOCKYARP_AUTH_USER";
    private const string AuthPassword = "DOCKYARP_AUTH_PASSWORD";
    private const string MaxBodySize = "DOCKYARP_MAX_BODY_SIZE";
    private const string ProxyTimeout = "DOCKYARP_PROXY_TIMEOUT";

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
            Name = "echo-priority-low", // priority: lower DOCKYARP_PRIORITY loses priority.local
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "priority.local"), Kv(VirtualPort, EchoPort), Kv(Priority, "1")],
            Environment = EchoEnv(EchoPort, id: "low"),
        },
        new BackendSpec
        {
            Name = "echo-priority-high", // priority: higher DOCKYARP_PRIORITY wins priority.local
            Image = EchoImage,
            Tag = EchoTag,
            Labels = [Kv(VirtualHost, "priority.local"), Kv(VirtualPort, EchoPort), Kv(Priority, "10")],
            Environment = EchoEnv(EchoPort, id: "high"),
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
}
