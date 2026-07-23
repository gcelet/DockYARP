namespace DockYarp.Docker.Labels;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using DockYarp.Core.Models;
using DockYarp.Docker.Models;

/// <summary>Parses a container's labels into a <see cref="ContainerLabelConfig"/>.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class LabelParser
{
    /// <summary>Attempts to parse the container's labels into a configuration.</summary>
    /// <param name="container">The container to parse.</param>
    /// <param name="config">The parsed configuration when successful.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    /// <returns><see langword="true"/> when the container declares a valid configuration.</returns>
    public static bool TryParse(
        ContainerInfo container,
        [NotNullWhen(true)] out ContainerLabelConfig? config,
        [NotNullWhen(false)] out string? error)
    {
        config = null;
        IReadOnlyDictionary<string, string> labels = container.Labels;

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
        config = new ContainerLabelConfig
        {
            Hosts = hosts,
            Port = port,
            Scheme = ParseScheme(GetOrNull(labels, DockerLabels.VirtualProto)),
            PathPrefix = GetOrNull(labels, DockerLabels.VirtualPath),
            PathRemovePrefix = ResolvePathRewrite(labels),
            LetsEncryptHost = GetOrNull(labels, DockerLabels.LetsEncryptHost),
            LetsEncryptEmail = GetOrNull(labels, DockerLabels.LetsEncryptEmail),
            HttpsMethod = ParseHttpsMethod(GetOrNull(labels, DockerLabels.HttpsMethod)),
            Hsts = GetOrNull(labels, DockerLabels.Hsts),
            LoadBalancingPolicy = ParsePolicy(GetOrNull(labels, DockerLabels.LoadBalancing)),
            Priority = ParsePriority(GetOrNull(labels, DockerLabels.Priority)),
            ClientCertificate = ParseClientCertificate(GetOrNull(labels, DockerLabels.ClientCert)),
            ProxyTimeout = ParseTimeoutSeconds(GetOrNull(labels, DockerLabels.ProxyTimeout)),
            MaxRequestBodySize = ParsePositiveLong(GetOrNull(labels, DockerLabels.MaxBodySize)),
            Auth = ParseAuth(labels),
        };
        return true;
    }

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
        return proto is not null
            && !proto.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !proto.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Derives the path prefix to strip: <c>VIRTUAL_DEST</c> maps <c>VIRTUAL_PATH</c> to root.</summary>
    private static string? ResolvePathRewrite(IReadOnlyDictionary<string, string> labels)
    {
        // VIRTUAL_DEST rewrites the matched VIRTUAL_PATH; only the "/" prefix-strip is supported today.
        string? dest = GetOrNull(labels, DockerLabels.VirtualDest);
        string? path = GetOrNull(labels, DockerLabels.VirtualPath);
        return dest is not null && path is not null ? path : null;
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

    private static ClientCertificateRequirement ParseClientCertificate(string? value) =>
        value?.ToUpperInvariant() switch
        {
            "REQUIRED" => ClientCertificateRequirement.Required,
            "OPTIONAL" => ClientCertificateRequirement.Optional,
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

    private static TimeSpan? ParseTimeoutSeconds(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static long? ParsePositiveLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0
            ? parsed
            : null;

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
        string.Equals(value, "https", StringComparison.OrdinalIgnoreCase)
            ? BackendScheme.Https
            : BackendScheme.Http;

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
        foreach (string part in raw.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
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
            _ => null,
        };
    }
}
