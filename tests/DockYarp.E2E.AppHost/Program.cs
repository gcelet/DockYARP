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
// The image entrypoint (bash /entrypoint.sh) runs `step ca init` when config/ca.json is absent, then `exec "$@"`.
// We override that CMD: after init has created the ACME provisioner, widen its certificate durations to 60 days
// (offline ca.json edit — remote management is off) so DockYarp, running its realistic default 30-day renewal
// margin, never renews during a run (a stable served thumbprint for RestartPersistenceTests). `;` (not `&&`) so
// a patch hiccup only affects that one test rather than blocking the whole ACME chain. Then serve as the image's
// default CMD does. This replaces the former Tls__RenewBeforeExpiry test override on the proxy.
const string stepCaCommand =
    "step ca provisioner update acme --x509-default-dur=1440h --x509-max-dur=1440h ; " +
    "exec step-ca --password-file /home/step/secrets/password /home/step/config/ca.json";
builder.AddContainer("stepca", "smallstep/step-ca")
    .WithBindMount(E2EPaths.StepCaDirectory, "/home/step")
    .WithEnvironment("DOCKER_STEPCA_INIT_NAME", "DockYarp E2E CA")
    .WithEnvironment("DOCKER_STEPCA_INIT_DNS_NAMES", "stepca,localhost")
    .WithEnvironment("DOCKER_STEPCA_INIT_ACME", "true")
    .WithEnvironment("DOCKER_STEPCA_INIT_REMOTE_MANAGEMENT", "false")
    .WithArgs("sh", "-c", stepCaCommand)
    .WithHttpsEndpoint(targetPort: 9000, name: "acme");

// step-ca serves a leaf signed by its intermediate but does not send the intermediate, so trusting the root
// alone gives a PartialChain error. This one-shot container waits for step-ca's PKI, writes a root+intermediate
// bundle DockYarp trusts (SSL_CERT_FILE), then exits; DockYarp waits for it so the bundle exists before
// the first ACME call (avoids a cached trust failure).
// The trailing chmod widens read+traverse access on everything step-ca wrote (certs/root_ca.crt included):
// on a native Linux runner, step-ca's own container UID owns those files/dirs on the host bind mount, and only
// the owner (or root) can chmod them — the host test process (a different, non-root UID) cannot do this itself
// (confirmed live: File.SetUnixFileMode from the host failed with EPERM/"Operation not permitted"). This
// container already runs as root (alpine, no WithUser override) and already has write access to the same bind
// mount, so it is the natural place to fix it. `a+rX` only ever ADDS bits (read for all, execute for
// directories/already-executable files) — it cannot strip step-ca's own write access to files it creates later.
const string bundleScript =
    "until [ -s /stepca/certs/intermediate_ca.crt ] && [ -s /stepca/certs/root_ca.crt ]; do sleep 1; done; " +
    "cat /stepca/certs/intermediate_ca.crt /stepca/certs/root_ca.crt > /stepca/ca-bundle.crt; " +
    "chmod -R a+rX /stepca";
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
// AdminApi__Surface must be non-Disabled for /api/* and /metrics to be mapped at all (default changed to
// Disabled). AdminApi__Host is "localhost" (no port) rather than a fixed port: RequireHost("localhost")
// matches any port on that host, which is required here since Aspire assigns the published host port
// dynamically per run, and both the health check and the test harness's Proxy client reach DockYarp via
// localhost:<that dynamic port>.
var proxy = builder.AddContainer("dockyarp", "dockyarp", "local")
    .WithEnvironment("Docker__Enabled", "true")
    .WithEnvironment("Docker__DockerEndpoint", "tcp://dockerproxy:2375")

    // Host-network e2e scenario (HostNetworkModeTests): BackendAddressResolver.Resolve only reads HostAddress
    // for a host-mode container, so setting this unconditionally has no effect on any other backend's address
    // resolution. host.docker.internal resolves natively on Docker Desktop; native Linux Docker (this project's
    // CI runner) needs the --add-host below (Docker Engine 20.10+'s host-gateway special value).
    .WithEnvironment("Docker__HostAddress", "host.docker.internal")
    .WithContainerRuntimeArgs("--add-host", "host.docker.internal:host-gateway")
    .WithEnvironment("AdminApi__ApiKey", apiKey)
    .WithEnvironment("AdminApi__Surface", "Api")
    .WithEnvironment("AdminApi__Host", "localhost")
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
    .WithEnvironment("Tls__ClientCrlPath", "/clientca/client-ca.crl")
    .WithEnvironment("Tls__CheckInterval", "00:00:05") // retry provisioning after discovery (startup pass races it; default 12h)

    // DockYarp runs with its realistic default renewal margin (30 days): step-ca is configured to issue certs
    // far longer than that (60 days, see the stepca container), so no renewal is due during a run and the served
    // thumbprint stays stable for the restart-reuse test (RestartPersistenceTests) — no product-option override.
    .WithHttpsEndpoint(targetPort: 8443, name: "https");

// Dedicated DockYarp instance with the PROXY protocol enabled on its edge, for the proxy-protocol scenario. It
// MUST be separate: with Server__EnableProxyProtocol every edge connection has to be prefixed by a PROXY header,
// which would break the plain-HTTP connections every other scenario makes against the shared proxy. It reuses the
// read-only socket proxy for discovery; no TLS is configured (the test drives the plaintext HTTP edge, sending the
// PROXY header before the request). No health check — a plain /metrics probe would be rejected for lacking the
// PROXY preamble; the test polls the edge until the route is live instead.
builder.AddContainer("dockyarp-pp", "dockyarp", "local")
    .WithEnvironment("Docker__Enabled", "true")
    .WithEnvironment("Docker__DockerEndpoint", "tcp://dockerproxy:2375")
    .WithEnvironment("Routing__DefaultHost", BackendCatalog.DefaultHost)
    .WithEnvironment("Server__EnableProxyProtocol", "true")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WaitFor(dockerproxy);

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
