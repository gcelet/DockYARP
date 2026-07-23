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
    public static string StepCaDirectory { get; } = Path.Combine(Path.GetTempPath(), "dockyarp-e2e", "stepca");

    /// <summary>The step-ca root certificate file, written by step-ca on first boot.</summary>
    public static string StepCaRootFile { get; } = Path.Combine(StepCaDirectory, "certs", "root_ca.crt");

    /// <summary>The directory holding the ephemeral client CA used for the mutual-TLS scenario.</summary>
    public static string ClientCaDirectory { get; } = Path.Combine(Path.GetTempPath(), "dockyarp-e2e", "clientca");

    /// <summary>The client CA certificate file (PEM) mounted as <c>Tls__ClientCaCertificatePath</c>.</summary>
    public static string ClientCaFile { get; } = Path.Combine(ClientCaDirectory, "client-ca.crt");
}
