# syntax=docker/dockerfile:1

# ---- build (driven by the Nuke pipeline via build.sh) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# build.sh bootstraps and runs the Nuke build in any .NET environment.
RUN bash build.sh Publish --configuration Release

# Seed an empty directory to become the app-owned /certs in the (shell-less) chiseled runtime.
RUN mkdir -p /certs-seed

# ---- runtime (chiseled, non-root) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /src/artifacts/publish ./

# Persistent state (certificates + Data Protection keys) lives on the mounted /certs volume. The chiseled
# image is non-root and has no shell, so create /certs owned by the app user via COPY --chown; a mounted
# named/anonymous volume then inherits that ownership (writable by the non-root app, and persistent).
COPY --chown=$APP_UID:$APP_UID --from=build /certs-seed /certs
ENV Tls__CertificateDirectory=/certs

# HTTP on 8080 (ACME challenge + redirects); HTTPS on 8443 (SNI per host, self-signed fallback).
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_HTTPS_PORTS=8443
EXPOSE 8080
EXPOSE 8443

# Mounted at runtime: certificates and static configuration.
VOLUME ["/certs", "/config"]

ENTRYPOINT ["dotnet", "DockYarp.App.dll"]
