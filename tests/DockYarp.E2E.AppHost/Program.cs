using DockYarp.E2E.AppHost;

// End-to-end distributed system: DockYarp (as a container mounting the Docker socket) in front of a set
// of labeled backend containers on the same Docker network. DockYarp discovers the backends from their
// labels and proxies to them; the NUnit harness asserts the behaviour over HTTP.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Shared with the test harness (X-Api-Key for the admin API); a literal on both sides on purpose.
const string apiKey = "e2e-secret-key";

// DockYarp runs as a container mounting the Docker socket read-only; /metrics gates readiness and
// Routing__DefaultHost sends unknown hosts to the default backend (exercised by a scenario).
builder.AddContainer("dockyarp", "dockyarp", "local")
    .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock", isReadOnly: true)
    .WithEnvironment("Docker__Enabled", "true")
    .WithEnvironment("AdminApi__ApiKey", apiKey)
    .WithEnvironment("Routing__DefaultHost", BackendCatalog.DefaultHost)
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/metrics");

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
