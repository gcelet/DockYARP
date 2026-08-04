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
// DockYarp is deliberately NOT gated on step-ca (its provisioning retries in the background), because the
// step-ca image's health check can stay "starting" and would otherwise block DockYarp from ever starting.
builder.AddContainer("stepca", "smallstep/step-ca")
    .WithBindMount(E2EPaths.StepCaDirectory, "/home/step")
    .WithEnvironment("DOCKER_STEPCA_INIT_NAME", "DockYarp E2E CA")
    .WithEnvironment("DOCKER_STEPCA_INIT_DNS_NAMES", "stepca,localhost")
    .WithEnvironment("DOCKER_STEPCA_INIT_ACME", "true")
    .WithEnvironment("DOCKER_STEPCA_INIT_REMOTE_MANAGEMENT", "false")
    .WithHttpsEndpoint(targetPort: 9000, name: "acme");

// step-ca serves a leaf signed by its intermediate but does not send the intermediate, so trusting the root
// alone gives a PartialChain error. This one-shot container waits for step-ca's PKI, writes a root+intermediate
// bundle DockYarp trusts (SSL_CERT_FILE), then exits; DockYarp waits for it so the bundle exists before
// the first ACME call (avoids a cached trust failure).
const string bundleScript =
    "until [ -s /stepca/certs/intermediate_ca.crt ] && [ -s /stepca/certs/root_ca.crt ]; do sleep 1; done; " +
    "cat /stepca/certs/intermediate_ca.crt /stepca/certs/root_ca.crt > /stepca/ca-bundle.crt";
var caBundle = builder.AddContainer("ca-bundle", "alpine")
    .WithBindMount(E2EPaths.StepCaDirectory, "/stepca")
    .WithArgs("sh", "-c", bundleScript);

// Read-only Docker API gateway; the only component that mounts the socket. DockYarp reaches the API
// through it over TCP, so the proxy container itself stays non-root (matching the reference stack).
var dockerproxy = builder.AddContainer("dockerproxy", "tecnativa/docker-socket-proxy")
    .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock", isReadOnly: true)
    .WithEnvironment("CONTAINERS", "1");

// DockYarp runs as a non-root container reaching the Docker API via the socket proxy; /metrics gates
// readiness and Routing__DefaultHost sends unknown hosts to the default backend (exercised by a scenario).
var proxy = builder.AddContainer("dockyarp", "dockyarp", "local")
    .WithEnvironment("Docker__Enabled", "true")
    .WithEnvironment("Docker__DockerEndpoint", "tcp://dockerproxy:2375")
    .WithEnvironment("AdminApi__ApiKey", apiKey)
    .WithEnvironment("Routing__DefaultHost", BackendCatalog.DefaultHost)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/metrics")
    .WaitFor(dockerproxy)
    .WaitForCompletion(caBundle); // bundle written before DockYarp's first ACME call (trust ready)

// TLS: point DockYarp's ACME client at step-ca, trust its root (Certes uses the default HttpClient, so the
// container OS must trust the CA via SSL_CERT_FILE), enable mutual TLS, and expose the HTTPS listener.
// HTTP-01 host resolution is handled by the socat sidecar below, so DockYarp itself needs no aliases here.
proxy
    .WithBindMount(E2EPaths.StepCaDirectory, "/stepca", isReadOnly: true)
    .WithBindMount(E2EPaths.ClientCaDirectory, "/clientca", isReadOnly: true)
    .WithBindMount(E2EPaths.CertsDirectory, "/certs") // writable: DockYarp persists certs + DP keys here
    .WithEnvironment("SSL_CERT_FILE", "/stepca/ca-bundle.crt")
    .WithEnvironment("Tls__AcmeDirectoryUri", "https://stepca:9000/acme/acme/directory")
    .WithEnvironment("Tls__AcceptTermsOfService", "true")
    .WithEnvironment("Tls__ContactEmail", "e2e@dockyarp.local")
    .WithEnvironment("Tls__ClientCaCertificatePath", "/clientca/client-ca.crt")
    .WithEnvironment("Tls__CheckInterval", "00:00:05") // retry provisioning after discovery (startup pass races it; default 12h)

    // step-ca issues ~24h certs while the default renewal margin is 30 days, so every 5s pass would renew and
    // churn the served thumbprint. A short margin keeps a provisioned cert stable for the restart-reuse test
    // (RestartPersistenceTests); the more coherent CA-side fix is deferred (backlog e2e-stepca-long-cert-duration).
    .WithEnvironment("Tls__RenewBeforeExpiry", "00:01:00")
    .WithHttpsEndpoint(targetPort: 8443, name: "https");

// ACME HTTP-01 front door. step-ca validates a challenge by fetching http://<LETSENCRYPT_HOST>/.well-known/...
// on port 80. Under DCP, containers resolve each other by resource name only, and DockYarp is non-root on
// 8080 (cannot bind 80). This socat sidecar carries the TLS host names as native network aliases and forwards
// port 80 to DockYarp's HTTP endpoint, so the challenge is reachable without touching DockYarp. Only
// step-ca uses these names; the tests reach DockYarp's HTTPS endpoint directly.
builder.AddContainer("acme-http01", "alpine/socat")
    .WithContainerNetworkAlias("tls.local")
    .WithContainerNetworkAlias("hsts.local")
    .WithContainerNetworkAlias("mtls.local")
    .WithContainerNetworkAlias("modern.local")
    .WithArgs("TCP-LISTEN:80,fork,reuseaddr", "TCP:dockyarp:8080")
    .WaitFor(proxy);

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
