using DockYarp.E2E.GrpcBackend;

using Microsoft.AspNetCore.Server.Kestrel.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// TLS terminates at DockYarp; the backend speaks cleartext HTTP/2 (h2c) so YARP can forward gRPC over HTTP/2.
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http2));

builder.Services.AddGrpc();

WebApplication app = builder.Build();
app.MapGrpcService<EchoerService>();
await app.RunAsync();
