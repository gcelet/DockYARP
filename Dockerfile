# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e

# ---- build (driven by the Nuke pipeline via build.sh) ----
FROM mcr.microsoft.com/dotnet/sdk:11.0@sha256:e2438fd800a09d2d761aefe4edd30675c8f1db8958e4423c0844cc9a0240e60f AS build
# The version is computed on the host (GitVersion needs .git, which is excluded from this context) and injected
# here; the Nuke build stamps it explicitly instead of recomputing.
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY . .
# build.sh bootstraps and runs the Nuke build in any .NET environment.
RUN bash build.sh Publish --configuration Release --version "$VERSION"

# Seed an empty directory to become the app-owned /certs in the (shell-less) chiseled runtime.
RUN mkdir -p /certs-seed

# ---- runtime (chiseled, non-root) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:84def277556c2843a44fdeefbd3145d0a2e32d8f736d0c1a7b0a96b2880b0187 AS runtime
WORKDIR /app
COPY --from=build /src/artifacts/publish ./

# Persistent state (certificates + Data Protection keys) lives on the mounted /certs volume. The chiseled
# image is non-root and has no shell, so create /certs owned by the app user via COPY --chown; a mounted
# named/anonymous volume then inherits that ownership (writable by the non-root app, and persistent).
COPY --chown=$APP_UID:$APP_UID --from=build /certs-seed /certs
ENV Tls__CertificateDirectory=/certs

# HTTP on 8080 (ACME challenge + redirects); HTTPS on 8443 (per-SNI TLS, self-signed fallback). DockYarp binds
# these explicitly (the HTTPS endpoint attaches a per-connection TLS callback), so ASPNETCORE_*_PORTS no longer
# apply — the data-plane ports are configured via Server__*.
ENV Server__HttpPort=8080
ENV Server__HttpsPort=8443
EXPOSE 8080
EXPOSE 8443

# Mounted at runtime: certificates and static configuration.
VOLUME ["/certs", "/config"]

ENTRYPOINT ["dotnet", "DockYarp.App.dll"]
