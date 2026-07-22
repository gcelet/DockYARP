## Context

`Program.cs` constructs option objects mostly by hand: `TlsOptions` only picks up `ContactEmail` and
`CertificateDirectory` (so `AcmeDirectoryUri`/`AcceptTermsOfService`/renewal margins are stuck at defaults —
staging, ToS not accepted), and `SecurityHeadersOptions` is `new()` (never bound). The `Add*` extensions
already take a concrete options instance; we just need to populate those instances from configuration.

## Goals / Non-Goals

**Goals:** bind `Tls`, `Security`, `Docker`, `AdminApi` (and keep `Host` shutdown) from configuration
(appsettings + env), preserving code defaults for unset keys; document the keys.

**Non-Goals:** no switch to the `IOptions<T>` pattern (the extensions take instances); no new options.

## Decisions

- **`GetSection("X").Bind(instance)`** per section, over a freshly-constructed options object, so unset keys
  keep the object's initializer defaults. Rationale: minimal, preserves the existing extension signatures,
  and `Bind` (unlike re-instantiating) leaves defaults intact.
  - `Tls` → `TlsOptions` (ContactEmail, CertificateDirectory, `AcmeDirectoryUri` (Uri), `AcceptTermsOfService`
    (bool), `RenewBeforeExpiry`/`CheckInterval` (TimeSpan)). Uri/TimeSpan bind via built-in type converters.
  - `Security` → `SecurityHeadersOptions`.
  - `Docker` → `DockerDiscoveryOptions` (endpoint, backoff); the `Docker:Enabled` gate stays a separate
    `GetValue<bool>` (not a property on the options).
  - `AdminApi` → `AdminApiOptions` (ApiKey).
- **Safe defaults preserved**: with no configuration, `AcmeDirectoryUri` stays the Let's Encrypt staging
  endpoint, headers keep their defaults, and the admin API stays closed (empty key ⇒ 401).
- **`appsettings.json`** gains the section skeletons (aligned with defaults) so the knobs are discoverable.

## Risks / Trade-offs

- Binding `Uri`/`TimeSpan` relies on framework type converters → covered by an integration test that sets
  a custom ACME directory and asserts the resolved `TlsOptions`.

## Migration Plan

Additive: replace manual option construction with section binding; add documented appsettings sections.

## Open Questions

- None.
