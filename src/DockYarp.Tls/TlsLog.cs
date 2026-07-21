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
}
