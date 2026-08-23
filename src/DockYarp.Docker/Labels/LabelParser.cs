namespace DockYarp.Docker.Labels;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using DockYarp.Core.Models;
using DockYarp.Docker.Models;

/// <summary>Parses a container's configuration (environment variables + labels) into a <see cref="ContainerLabelConfig"/>.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class LabelParser
{
    /// <summary>Builds the effective configuration map: labels overlaid by environment variables (env wins).</summary>
    /// <param name="container">The container to read.</param>
    /// <returns>The merged key/value view; an environment variable overrides a same-named label.</returns>
    /// <remarks>Environment variables are nginx-proxy's canonical config channel; the label is the fallback.</remarks>
    public static IReadOnlyDictionary<string, string> EffectiveConfig(ContainerInfo container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (container.Env.Count == 0)
        {
            return container.Labels;
        }

        Dictionary<string, string> merged = new(container.Labels, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in container.Env)
        {
            merged[entry.Key] = entry.Value;
        }

        return merged;
    }

    /// <summary>Attempts to parse the container's configuration (env vars + labels) into a configuration.</summary>
    /// <param name="container">The container to parse.</param>
    /// <param name="config">The parsed configuration when successful.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    /// <returns><see langword="true"/> when the container declares a valid configuration.</returns>
    public static bool TryParse(
        ContainerInfo container,
        [NotNullWhen(true)] out ContainerLabelConfig? config,
        [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(container);
        config = null;
        IReadOnlyDictionary<string, string> labels = EffectiveConfig(container);

        ImmutableArray<string> hosts = labels.TryGetValue(DockerLabels.VirtualHost, out string? host)
            ? SplitHosts(host)
            : [];
        if (hosts.IsEmpty)
        {
            error = $"{DockerLabels.VirtualHost} is required.";
            return false;
        }

        if (!TryResolvePort(labels, container.ExposedPorts, out int port, out error))
        {
            return false;
        }

        error = null;
        string? virtualPath = GetOrNull(labels, DockerLabels.VirtualPath);
        (string? removePrefix, string? addPrefix) =
            PathRewrite.Resolve(GetOrNull(labels, DockerLabels.VirtualDest), virtualPath);
        config = new ContainerLabelConfig
        {
            Hosts = hosts,
            Port = port,
            Scheme = ParseScheme(GetOrNull(labels, DockerLabels.VirtualProto)),
            Http2 = ParseHttp2(GetOrNull(labels, DockerLabels.VirtualProto)),
            PathPrefix = virtualPath,
            PathRemovePrefix = removePrefix,
            PathAddPrefix = addPrefix,
            LetsEncryptHost = GetOrNull(labels, DockerLabels.LetsEncryptHost),
            LetsEncryptEmail = GetOrNull(labels, DockerLabels.LetsEncryptEmail),
            CertName = GetOrNull(labels, DockerLabels.CertName),
            SslPolicy = GetOrNull(labels, DockerLabels.SslPolicy),
            Http2Enabled = ParseBool(
                GetOrNull(labels, DockerLabels.Http2Enabled) ?? GetOrNull(labels, DockerLabels.NginxHttp2Enable)),
            ServerTokens = GetOrNull(labels, DockerLabels.ServerTokens),
            ExternalHttpsPort = ParseExternalPort(GetOrNull(labels, DockerLabels.ExternalHttpsPort)),
            EnableHttpOnMissingCert = ParseBool(GetOrNull(labels, DockerLabels.EnableHttpOnMissingCert)),
            TrustDefaultCert = ParseBool(
                GetOrNull(labels, DockerLabels.TrustDefaultCert) ?? GetOrNull(labels, DockerLabels.NginxTrustDefaultCert)),
            HttpsMethod = ParseHttpsMethod(GetOrNull(labels, DockerLabels.HttpsMethod)),
            Hsts = GetOrNull(labels, DockerLabels.Hsts),
            LoadBalancingPolicy = ResolveLoadBalancing(labels),
            SessionAffinityPolicy = ResolveAffinity(labels),
            Priority = ParsePriority(GetOrNull(labels, DockerLabels.Priority)),
            ClientCertificate = ResolveClientCertificate(labels),
            ChallengeType = ParseAcmeChallenge(GetOrNull(labels, DockerLabels.AcmeChallenge)),
            ProxyTimeout = ParseTimeoutSeconds(GetOrNull(labels, DockerLabels.ProxyTimeout)),
            MaxRequestBodySize = ParsePositiveLong(GetOrNull(labels, DockerLabels.MaxBodySize)),
            MaxConnectionsPerServer = ParsePositiveInt(GetOrNull(labels, DockerLabels.MaxConnections)),
            Auth = ParseAuth(labels),
            InternalOnly = ParseInternalOnly(labels),
        };
        return true;
    }

    /// <summary>Parses the container-level attributes (no host/port/path) shared by classic and multiports routes.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns>A configuration carrying auth, TLS, load balancing, and limit attributes.</returns>
    public static ContainerLabelConfig ParseCommon(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return new ContainerLabelConfig
        {
            Hosts = [],
            Port = 0,
            LetsEncryptHost = GetOrNull(labels, DockerLabels.LetsEncryptHost),
            LetsEncryptEmail = GetOrNull(labels, DockerLabels.LetsEncryptEmail),
            CertName = GetOrNull(labels, DockerLabels.CertName),
            SslPolicy = GetOrNull(labels, DockerLabels.SslPolicy),
            Http2Enabled = ParseBool(
                GetOrNull(labels, DockerLabels.Http2Enabled) ?? GetOrNull(labels, DockerLabels.NginxHttp2Enable)),
            ServerTokens = GetOrNull(labels, DockerLabels.ServerTokens),
            ExternalHttpsPort = ParseExternalPort(GetOrNull(labels, DockerLabels.ExternalHttpsPort)),
            EnableHttpOnMissingCert = ParseBool(GetOrNull(labels, DockerLabels.EnableHttpOnMissingCert)),
            TrustDefaultCert = ParseBool(
                GetOrNull(labels, DockerLabels.TrustDefaultCert) ?? GetOrNull(labels, DockerLabels.NginxTrustDefaultCert)),
            LoadBalancingPolicy = ResolveLoadBalancing(labels),
            SessionAffinityPolicy = ResolveAffinity(labels),
            Priority = ParsePriority(GetOrNull(labels, DockerLabels.Priority)),
            ClientCertificate = ResolveClientCertificate(labels),
            ChallengeType = ParseAcmeChallenge(GetOrNull(labels, DockerLabels.AcmeChallenge)),
            ProxyTimeout = ParseTimeoutSeconds(GetOrNull(labels, DockerLabels.ProxyTimeout)),
            MaxRequestBodySize = ParsePositiveLong(GetOrNull(labels, DockerLabels.MaxBodySize)),
            MaxConnectionsPerServer = ParsePositiveInt(GetOrNull(labels, DockerLabels.MaxConnections)),
            HttpsMethod = ParseHttpsMethod(GetOrNull(labels, DockerLabels.HttpsMethod)),
            Hsts = GetOrNull(labels, DockerLabels.Hsts),
            Auth = ParseAuth(labels),
            InternalOnly = ParseInternalOnly(labels),
        };
    }

    private static bool ParseInternalOnly(IReadOnlyDictionary<string, string> labels) =>
        string.Equals(GetOrNull(labels, DockerLabels.NetworkAccess), "internal", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reports whether exactly one of the auth user/password labels is present (incomplete).</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when auth labels are present but incomplete.</returns>
    public static bool HasIncompleteAuth(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        bool hasUser = GetOrNull(labels, DockerLabels.AuthUser) is not null;
        bool hasPassword = GetOrNull(labels, DockerLabels.AuthPassword) is not null;
        return hasUser != hasPassword;
    }

    /// <summary>Reports whether <c>VIRTUAL_PROTO</c> is present but not a supported scheme.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value is neither <c>http</c> nor <c>https</c>.</returns>
    public static bool HasUnsupportedProto(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? proto = GetOrNull(labels, DockerLabels.VirtualProto);
        return proto is not null && proto.ToUpperInvariant() is not ("HTTP" or "HTTPS" or "GRPC" or "GRPCS");
    }

    /// <summary>Reports whether <c>DOCKYARP_PRIORITY</c> is present but not a valid integer.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value cannot be parsed as an integer.</returns>
    public static bool HasInvalidPriority(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? raw = GetOrNull(labels, DockerLabels.Priority);
        return raw is not null
            && !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>Reports whether <c>DOCKYARP_CLIENT_CERT</c> is present but not a recognized value.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value is not required/optional/none/off.</returns>
    public static bool HasUnsupportedClientCert(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? value = GetOrNull(labels, DockerLabels.ClientCert);
        return value is not null && value.ToUpperInvariant() is not ("REQUIRED" or "OPTIONAL" or "NONE" or "OFF");
    }

    /// <summary>Reports whether <c>DOCKYARP_ACME_CHALLENGE</c> is present but not a recognized value.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value is not http-01/dns-01.</returns>
    public static bool HasUnsupportedAcmeChallenge(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? value = GetOrNull(labels, DockerLabels.AcmeChallenge);
        return value is not null && value.ToUpperInvariant() is not ("HTTP-01" or "DNS-01");
    }

    private static AcmeChallengeType ParseAcmeChallenge(string? value) =>
        value?.ToUpperInvariant() switch
        {
            "DNS-01" => AcmeChallengeType.Dns01,
            _ => AcmeChallengeType.Http01,
        };

    private static ClientCertificateRequirement ParseClientCertificate(string? value) =>
        value?.ToUpperInvariant() switch
        {
            "REQUIRED" => ClientCertificateRequirement.Required,
            "OPTIONAL" => ClientCertificateRequirement.Optional,
            _ => ClientCertificateRequirement.None,
        };

    private static ClientCertificateRequirement ResolveClientCertificate(IReadOnlyDictionary<string, string> labels)
    {
        // DockYarp-native wins; the nginx-proxy namespaced label is the compatibility fallback.
        if (GetOrNull(labels, DockerLabels.ClientCert) is { } native)
        {
            return ParseClientCertificate(native);
        }

        return GetOrNull(labels, DockerLabels.NginxSslVerifyClient) is { } verify
            ? TranslateNginxSslVerifyClient(verify)
            : ClientCertificateRequirement.None;
    }

    private static ClientCertificateRequirement TranslateNginxSslVerifyClient(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "ON" => ClientCertificateRequirement.Required,
            "OPTIONAL" or "OPTIONAL_NO_CA" => ClientCertificateRequirement.Optional,
            _ => ClientCertificateRequirement.None,
        };

    private static int ParsePriority(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority) ? priority : 0;

    /// <summary>Reports whether <c>DOCKYARP_PROXY_TIMEOUT</c> is present but not a positive integer.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value cannot be parsed as a positive number of seconds.</returns>
    public static bool HasInvalidProxyTimeout(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return GetOrNull(labels, DockerLabels.ProxyTimeout) is { } raw && ParseTimeoutSeconds(raw) is null;
    }

    /// <summary>Reports whether <c>DOCKYARP_MAX_BODY_SIZE</c> is present but not a positive integer.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value cannot be parsed as a positive number of bytes.</returns>
    public static bool HasInvalidMaxBodySize(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return GetOrNull(labels, DockerLabels.MaxBodySize) is { } raw && ParsePositiveLong(raw) is null;
    }

    /// <summary>Reports whether <c>EXTERNAL_HTTPS_PORT</c> is present but not a valid port.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value cannot be parsed as a port (1..65535).</returns>
    public static bool HasInvalidExternalHttpsPort(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return GetOrNull(labels, DockerLabels.ExternalHttpsPort) is { } raw && ParseExternalPort(raw) is null;
    }

    /// <summary>Reports whether <c>DOCKYARP_MAX_CONNECTIONS</c> is present but not a positive integer.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value cannot be parsed as a positive connection count.</returns>
    public static bool HasInvalidMaxConnections(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return GetOrNull(labels, DockerLabels.MaxConnections) is { } raw && ParsePositiveInt(raw) is null;
    }

    private static TimeSpan? ParseTimeoutSeconds(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static long? ParsePositiveLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0
            ? parsed
            : null;

    private static int? ParsePositiveInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : null;

    private static int? ParseExternalPort(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) && port is > 0 and <= 65535
            ? port
            : null;

    private static bool? ParseBool(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "ON" or "YES" or "1" => true,
            "FALSE" or "OFF" or "NO" or "0" => false,
            _ => null,
        };

    /// <summary>Reports whether <c>HTTPS_METHOD</c> is present but not a recognized value.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value is not one of redirect/noredirect/nohttp/nohttps.</returns>
    public static bool HasUnsupportedHttpsMethod(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? value = GetOrNull(labels, DockerLabels.HttpsMethod);
        return value is not null && !IsKnownHttpsMethod(value);
    }

    private static bool IsKnownHttpsMethod(string value) =>
        value.ToUpperInvariant() is "REDIRECT" or "NOREDIRECT" or "NOHTTP" or "NOHTTPS";

    private static HttpsMethod ParseHttpsMethod(string? value) =>
        value?.ToUpperInvariant() switch
        {
            "NOREDIRECT" => HttpsMethod.NoRedirect,
            "NOHTTP" => HttpsMethod.NoHttp,
            "NOHTTPS" => HttpsMethod.NoHttps,
            _ => HttpsMethod.Redirect,
        };

    private static BackendScheme ParseScheme(string? value) =>
        value?.ToUpperInvariant() switch
        {
            "HTTPS" or "GRPCS" => BackendScheme.Https,
            _ => BackendScheme.Http,
        };

    private static bool ParseHttp2(string? value) =>
        value?.ToUpperInvariant() is "GRPC" or "GRPCS";

    private static BasicAuthCredentials? ParseAuth(IReadOnlyDictionary<string, string> labels)
    {
        string? user = GetOrNull(labels, DockerLabels.AuthUser);
        string? password = GetOrNull(labels, DockerLabels.AuthPassword);
        if (user is null || password is null)
        {
            return null;
        }

        return new BasicAuthCredentials
        {
            Username = user,
            Password = password,
            Realm = GetOrNull(labels, DockerLabels.AuthRealm),
        };
    }

    private static bool TryResolvePort(
        IReadOnlyDictionary<string, string> labels,
        ImmutableArray<int> exposedPorts,
        out int port,
        [NotNullWhen(false)] out string? error)
    {
        error = null;
        port = 0;

        if (labels.TryGetValue(DockerLabels.VirtualPort, out string? raw))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) && port > 0)
            {
                return true;
            }

            error = $"{DockerLabels.VirtualPort} '{raw}' is not a valid port.";
            return false;
        }

        if (exposedPorts.Length == 1)
        {
            port = exposedPorts[0];
            return true;
        }

        error = $"{DockerLabels.VirtualPort} is required because the container exposes {exposedPorts.Length} ports.";
        return false;
    }

    /// <summary>Splits a comma-separated host label, trimming whitespace and dropping empty entries.</summary>
    private static ImmutableArray<string> SplitHosts(string raw)
    {
        ImmutableArray<string>.Builder hosts = ImmutableArray.CreateBuilder<string>();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string part in raw.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0 && seen.Add(trimmed))
            {
                hosts.Add(trimmed);
            }
        }

        return hosts.ToImmutable();
    }

    private static string? GetOrNull(IReadOnlyDictionary<string, string> labels, string key) =>
        labels.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static LoadBalancingPolicy? ParsePolicy(string? value)
    {
        return value?.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant() switch
        {
            "ROUNDROBIN" => LoadBalancingPolicy.RoundRobin,
            "LEASTREQUESTS" => LoadBalancingPolicy.LeastRequests,
            "POWEROFTWOCHOICES" => LoadBalancingPolicy.PowerOfTwoChoices,
            "RANDOM" => LoadBalancingPolicy.Random,
            "FIRSTALPHABETICAL" => LoadBalancingPolicy.FirstAlphabetical,
            _ => null,
        };
    }

    private static LoadBalancingPolicy? ResolveLoadBalancing(IReadOnlyDictionary<string, string> labels)
    {
        // DockYarp-native wins; the nginx-proxy namespaced label is the compatibility fallback.
        if (GetOrNull(labels, DockerLabels.LoadBalancing) is { } native)
        {
            return ParsePolicy(native);
        }

        // A DockYarp policy name under the alias key still works; otherwise translate the nginx directive.
        return GetOrNull(labels, DockerLabels.NginxLoadBalance) is { } directive
            ? ParsePolicy(directive) ?? TranslateNginxLoadBalance(directive)
            : null;
    }

    private static LoadBalancingPolicy? TranslateNginxLoadBalance(string value)
    {
        // nginx directive: drop a trailing ';' and any arguments (e.g. "hash $remote_addr;"), keep the name.
        string trimmed = value.Trim().TrimEnd(';').Trim();
        int space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        string directive = space >= 0 ? trimmed[..space] : trimmed;
        return directive.ToUpperInvariant() switch
        {
            "LEAST_CONN" => LoadBalancingPolicy.LeastRequests,
            "RANDOM" => LoadBalancingPolicy.Random,
            "ROUND_ROBIN" => LoadBalancingPolicy.RoundRobin,
            _ => null, // ip_hash / hash $x → session affinity, not a policy (see add-session-affinity)
        };
    }

    /// <summary>Reports whether <c>DOCKYARP_LB</c> is present but not a recognized policy.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value does not map to a known load-balancing policy.</returns>
    public static bool HasUnsupportedLoadBalancing(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? value = GetOrNull(labels, DockerLabels.LoadBalancing);
        return value is not null && ParsePolicy(value) is null;
    }

    /// <summary>Parses a <c>DOCKYARP_AFFINITY</c> value into a <see cref="SessionAffinityPolicy"/>.</summary>
    /// <param name="value">The raw label value.</param>
    /// <returns>The matching policy, or <see langword="null"/> when unrecognized.</returns>
    private static SessionAffinityPolicy? ParseAffinityPolicy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "IP-HASH" or "IPHASH" => SessionAffinityPolicy.ClientIpHash,
            "COOKIE" => SessionAffinityPolicy.Cookie,
            "CUSTOM-HEADER" or "CUSTOMHEADER" => SessionAffinityPolicy.CustomHeader,
            "FALSE" => SessionAffinityPolicy.None,
            _ => null,
        };
    }

    // DockYarp-native DOCKYARP_AFFINITY wins; the nginx-proxy loadbalance directive is the compatibility
    // fallback, but only for its ip_hash/hash shape — nginx has no equivalent to translate for cookie/
    // custom-header (open-source nginx, which nginx-proxy is built on, has no cookie-based sticky-session
    // mechanism at all).
    private static SessionAffinityPolicy? ResolveAffinity(IReadOnlyDictionary<string, string> labels)
    {
        if (GetOrNull(labels, DockerLabels.SessionAffinity) is { } native)
        {
            return ParseAffinityPolicy(native);
        }

        return GetOrNull(labels, DockerLabels.NginxLoadBalance) is { } directive
            ? TranslateNginxAffinity(directive)
            : null;
    }

    private static SessionAffinityPolicy? TranslateNginxAffinity(string value)
    {
        // Same trim/first-token shape as TranslateNginxLoadBalance: drop a trailing ';' and any arguments
        // (e.g. "hash $remote_addr consistent;"), keep the directive name.
        string trimmed = value.Trim().TrimEnd(';').Trim();
        int space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        string directive = space >= 0 ? trimmed[..space] : trimmed;
        return directive.ToUpperInvariant() switch
        {
            "IP_HASH" or "HASH" => SessionAffinityPolicy.ClientIpHash,
            _ => null,
        };
    }

    /// <summary>Reports whether <c>DOCKYARP_AFFINITY</c> is present but not a recognized policy.</summary>
    /// <param name="labels">The container labels.</param>
    /// <returns><see langword="true"/> when the value does not map to a known affinity policy.</returns>
    public static bool HasUnsupportedAffinity(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        string? value = GetOrNull(labels, DockerLabels.SessionAffinity);
        return value is not null && ParseAffinityPolicy(value) is null;
    }
}
