using System.Collections.Generic;

using DockYarp.E2E.AppHost;

// End-to-end distributed system: DockYarp (as a container mounting the Docker socket) in front of a set
// of labeled backend containers on the same Docker network. DockYarp discovers the backends from their
// labels and proxies to them; the NUnit harness asserts the behaviour over HTTP and HTTPS. A local step-ca
// ACME server issues real certificates for the TLS backends over the HTTP-01 challenge.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Shared with the test harness (X-Api-Key for the admin API); a literal on both sides on purpose.
const string apiKey = "e2e-secret-key";

// Local ACME certificate authority. step-ca initialises its PKI (root + intermediate + an ACME provisioner)
// on first boot into the bind-mounted directory; DockYarp and the tests read the root from there.
var stepca = builder.AddContainer("stepca", "smallstep/step-ca")
    .WithBindMount(E2EPaths.StepCaDirectory, "/home/step")
    .WithEnvironment("DOCKER_STEPCA_INIT_NAME", "DockYarp E2E CA")
    .WithEnvironment("DOCKER_STEPCA_INIT_DNS_NAMES", "stepca,localhost")
    .WithEnvironment("DOCKER_STEPCA_INIT_ACME", "true")
    .WithEnvironment("DOCKER_STEPCA_INIT_REMOTE_MANAGEMENT", "false")
    .WithHttpsEndpoint(targetPort: 9000, name: "acme");

// DockYarp runs as a container mounting the Docker socket read-only; /metrics gates readiness and
// Routing__DefaultHost sends unknown hosts to the default backend (exercised by a scenario).
var proxy = builder.AddContainer("dockyarp", "dockyarp", "local")
    .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock", isReadOnly: true)
    .WithEnvironment("Docker__Enabled", "true")
    .WithEnvironment("AdminApi__ApiKey", apiKey)
    .WithEnvironment("Routing__DefaultHost", BackendCatalog.DefaultHost)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/metrics");

// TLS: point DockYarp's ACME client at step-ca, trust its root (Certes uses the default HttpClient, so the
// container OS must trust the CA via SSL_CERT_FILE), enable mutual TLS, and expose the HTTPS listener. The
// network aliases let step-ca resolve each LETSENCRYPT_HOST back to DockYarp for the HTTP-01 challenge.
proxy
    .WithBindMount(E2EPaths.StepCaDirectory, "/stepca", isReadOnly: true)
    .WithBindMount(E2EPaths.ClientCaDirectory, "/clientca", isReadOnly: true)
    .WithEnvironment("SSL_CERT_FILE", "/stepca/certs/root_ca.crt")
    .WithEnvironment("Tls__AcmeDirectoryUri", "https://stepca:9000/acme/acme/directory")
    .WithEnvironment("Tls__AcceptTermsOfService", "true")
    .WithEnvironment("Tls__ContactEmail", "e2e@dockyarp.local")
    .WithEnvironment("Tls__ClientCaCertificatePath", "/clientca/client-ca.crt")
    .WithHttpsEndpoint(targetPort: 8443, name: "https")
    .WithContainerRuntimeArgs(NetworkAliasArgs())
    .WaitFor(stepca);

foreach (BackendSpec backend in BackendCatalog.All)
{
    var resource = builder
        .AddContainer(backend.Name, backend.Image, backend.Tag)
        .WithContainerRuntimeArgs(backend.ToRuntimeArgs());

    foreach ((string key, string value) in backend.Environment)
    {
        resource.WithEnvironment(key, value);
    }
}

await builder.Build().RunAsync();

// Each TLS host resolves to DockYarp so step-ca can reach it on 8080 during HTTP-01 validation.
static string[] NetworkAliasArgs()
{
    List<string> args = new(BackendCatalog.TlsHosts.Count * 2);
    foreach (string host in BackendCatalog.TlsHosts)
    {
        args.Add("--network-alias");
        args.Add(host);
    }

    return [.. args];
}
