# syntax=docker/dockerfile:1

# ---- build (driven by the Nuke pipeline via build.sh) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# build.sh bootstraps and runs the Nuke build in any .NET environment.
RUN bash build.sh Publish --configuration Release

# ---- runtime (chiseled, non-root) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /src/artifacts/publish ./

# Listen on 8080 (HTTP). HTTPS (8443) requires certificate configuration in production.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
EXPOSE 8443

# Mounted at runtime: certificates and static configuration.
VOLUME ["/certs", "/config"]

ENTRYPOINT ["dotnet", "DockYarp.App.dll"]
