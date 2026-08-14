## 1. Presets (AG-AT)
- [x] 1.1 `SslPolicyPresets.Presets`: add the 16 classic AWS ELB (ALB) policy names, each reusing an existing suite
  array by tier — `TLS13-1-3-2021-06` → (`Tls13`, `Tls13Suites`); the GCM/FS-only 1.2 policies → (`Tls12`,
  `IntermediateSuites`); the broader (CBC-including) policies → (`Tls12`, `OldSuites`)
- [x] 1.2 A short comment recording the collapse (1.2 floor → two version outcomes) and the best-effort-cipher rationale

## 2. Tests (AG-AT)
- [x] 2.1 `SslPolicyPresetsTests`: an ELB 1.3-only name → `Tls13`+`Tls13Suites`; a restricted-1.2 name → `Tls12`+
  `IntermediateSuites`; a broad name → `Tls12`+`OldSuites`; resolution is case-insensitive
- [x] 2.2 A specialized `-FIPS-*` name is NOT recognized → falls back (configured values unchanged); an explicit
  cipher list still overrides an ELB preset

## 3. Docs (AG-DOC — recognized config values)
- [x] 3.1 docs site `configuration.md` / `features.md` (+ `docs/labels-reference.md`): note `SSL_POLICY`/`Tls:SslPolicy`
  also accepts the classic AWS ELB policy names (clamped to the TLS 1.2 floor; best-effort ciphers; FIPS/PQ/RFC9151
  variants not mapped)

## 4. Verify (AG-AT)
- [x] 4.1 Nuke `Test` gate green (unit), warnings-as-errors clean
