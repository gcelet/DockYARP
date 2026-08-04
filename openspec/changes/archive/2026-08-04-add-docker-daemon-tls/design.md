# Design — add-docker-daemon-tls

## Constraint: no CertificateCredentials in the pinned Docker.DotNet
Docker.DotNet **3.125.15** ships only `AnonymousCredentials` and the abstract base `Docker.DotNet.Credentials`
(`bool IsTlsCredentials()`, `HttpMessageHandler GetHandler(HttpMessageHandler inner)`). The historical
`CertificateCredentials` (from the old `Docker.DotNet.X509` package) is **absent**. Rather than add an
unmaintained package, this change implements a small custom `Credentials` subclass — Docker.DotNet's own
handler is the public `Microsoft.Net.Http.Client.ManagedHandler`, which exposes settable `ClientCertificates`
(`X509CertificateCollection`) and `ServerCertificateValidationCallback` (`RemoteCertificateValidationCallback`):

```csharp
private sealed class ClientCertificateCredentials(
    X509Certificate2 clientCertificate, RemoteCertificateValidationCallback serverValidation) : Credentials
{
    public override bool IsTlsCredentials() => true;
    public override HttpMessageHandler GetHandler(HttpMessageHandler innerHandler)
    {
        if (innerHandler is ManagedHandler managed)
        {
            managed.ClientCertificates = new X509CertificateCollection { clientCertificate };
            managed.ServerCertificateValidationCallback = serverValidation;
        }
        return innerHandler;
    }
}
```
`DockerClientConfiguration.CreateClient()` builds a `ManagedHandler` and calls `credentials.GetHandler(it)`, so
the cast wires exactly the live handler.

## Split: pure factory (tested) vs file IO (runtime)
`DockerTlsCredentials.Create` takes **PEM strings**, not paths, so it is fully unit-testable with in-memory
certs; `DockerContainerSource.CreateClient` performs the file IO (reads `ca.pem`/`cert.pem`/`key.pem` from
`CertPath`) and passes the strings in.

```
Create(bool endpointUsesTls, bool tlsVerify, string? caPem, string? certPem, string? keyPem) : Credentials?
  - endpointUsesTls == false        -> null   (unix/npipe: unchanged)
  - certPem/keyPem missing          -> null   (no client cert: unchanged)
  - else -> ClientCertificateCredentials(LoadClientCert(certPem,keyPem), BuildServerValidation(tlsVerify,caPem))
```
- `endpointUsesTls` = the endpoint scheme is `tcp` (computed by the caller from the `Uri`).
- `LoadClientCert`: `X509Certificate2.CreateFromPem(certPem, keyPem)` re-exported through
  `X509CertificateLoader.LoadPkcs12` so the private key is usable for TLS on Windows (the repo's
  `PemCertificateLoader` uses the same idiom; it lives in `DockYarp.Tls`, out of `DockYarp.Docker`'s
  dependency graph, so the 3-line idiom is repeated here rather than referenced).
- `BuildServerValidation`: `tlsVerify == false` → accept any; else validate the daemon cert against the CA via
  an `X509Chain` with `CustomRootTrust` + `RevocationMode.NoCheck` (mirrors `ClientCertificateValidator`).

## Endpoint gating
Client TLS applies only to `tcp://` endpoints. A unix socket / `npipe` (the default) yields
`endpointUsesTls == false` → `null` credentials → the existing `DockerClientConfiguration(uri)` /
`DockerClientConfiguration()` path, byte-for-byte unchanged.

## Options
`DockerDiscoveryOptions.CertPath` (dir with `ca.pem`/`cert.pem`/`key.pem`, Docker's `DOCKER_CERT_PATH`
convention) and `TlsVerify` (bool), bound from the `Docker` section like the existing discovery options.

## Out of scope
- Mapping the literal `DOCKER_HOST`/`DOCKER_TLS_VERIFY`/`DOCKER_CERT_PATH` env names (DockYarp uses its
  `Docker:*` app-config section; `DockerEndpoint` already stands in for `DOCKER_HOST`).
- CRL/OCSP revocation of the daemon cert (offline custom-trust validation only).
