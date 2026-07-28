namespace DockYarp.E2E.AppHost;

using System.IO;

/// <summary>Host paths shared between the AppHost and the test harness for TLS material.</summary>
/// <remarks>
/// The paths derive from <see cref="Path.GetTempPath"/>, which resolves identically in the AppHost and the
/// tests because <c>Aspire.Hosting.Testing</c> hosts the AppHost in the test process. step-ca writes its PKI
/// (including the root) into <see cref="StepCaDirectory"/>; the tests generate an ephemeral client CA into
/// <see cref="ClientCaDirectory"/>. Both are bind-mounted into the DockYarp container.
/// </remarks>
public static class E2EPaths
{
    /// <summary>The directory step-ca initialises its PKI into (bind-mounted at the container's <c>/home/step</c>).</summary>
    /// <remarks>
    /// The <c>ca-bundle</c> container also mounts this directory to write <c>ca-bundle.crt</c> (root+intermediate),
    /// which DockYarp trusts via <c>SSL_CERT_FILE</c> — the root alone yields a <c>PartialChain</c> error
    /// because step-ca does not send its intermediate.
    /// </remarks>
    public static string StepCaDirectory { get; } = Path.Combine(Path.GetTempPath(), "dockyarp-e2e", "stepca");

    /// <summary>The directory holding the ephemeral client CA used for the mutual-TLS scenario.</summary>
    public static string ClientCaDirectory { get; } = Path.Combine(Path.GetTempPath(), "dockyarp-e2e", "clientca");

    /// <summary>The client CA certificate file (PEM) mounted as <c>Tls__ClientCaCertificatePath</c>.</summary>
    public static string ClientCaFile { get; } = Path.Combine(ClientCaDirectory, "client-ca.crt");

    /// <summary>The (writable) directory DockYarp persists certificates and Data Protection keys into,
    /// bind-mounted at the container's <c>/certs</c>.</summary>
    public static string CertsDirectory { get; } = Path.Combine(Path.GetTempPath(), "dockyarp-e2e", "certs");
}
