namespace DockYarp.Tls;

using System;

using Microsoft.Extensions.Logging;

/// <summary>Source-generated log messages for TLS provisioning.</summary>
internal static partial class TlsLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Provisioned certificate for {Host}.")]
    public static partial void CertificateProvisioned(ILogger logger, string host);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to provision certificate for {Host}.")]
    public static partial void ProvisioningFailed(ILogger logger, string host, Exception exception);

    // No Exception argument on purpose: a transient failure the reconcile loop retries should not render a stack
    // trace (that misleading noise is what this message replaces until the failure persists and escalates to Error).
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Provisioning certificate for {Host} did not succeed yet (attempt {Attempt}); will retry: {Reason}")]
    public static partial void ProvisioningRetrying(ILogger logger, string host, int attempt, string reason);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Shared certificate '{CertName}' pinned via CERT_NAME is not in the store; falling back to per-host selection.")]
    public static partial void SharedCertificateMissing(ILogger logger, string certName);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Unsupported per-host SSL_POLICY '{Policy}'; using the global TLS posture.")]
    public static partial void UnsupportedSslPolicy(ILogger logger, string policy);
}
