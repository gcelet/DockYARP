# syntax=docker/dockerfile:1@sha256:ecfaec9ed6d810b56388c508f4121597bfbba70d41a6dfeee4d8cad5f295fc32

# ---- build (driven by the Nuke pipeline via build.sh) ----
FROM mcr.microsoft.com/dotnet/sdk:11.0@sha256:335dfd6679df1025209964f3e2592c0f1808501d17b9295640fd1a41d132c117 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:9651fa59abcdf177c30392cb44a820605ca5d618429ab37acbf6e7c644510b02 AS runtime
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
