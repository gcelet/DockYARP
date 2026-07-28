## 1. Stabilize certificates in the e2e (AG-DEP)
- [x] 1.1 `DockYarp.E2E.AppHost/Program.cs`: set `Tls__RenewBeforeExpiry` below step-ca's certificate lifetime
      (`00:01:00`) on the proxy, with a comment explaining it avoids renewal churn (left `CheckInterval` as is —
      it drives the post-discovery provisioning retry)

## 2. Fixture restart helper (AG-DEP)
- [x] 2.1 `AspireAppHostFixture`: add `RestartProxyAndWaitHealthyAsync(CancellationToken)` — execute
      `KnownResourceCommands.RestartCommand` via `ResourceCommandService`, throw on failure, then
      `WaitForResourceHealthyAsync("dockyarp")`
- [x] 2.2 `TlsHarness.PrepareCertsDirectory`: make the reset best-effort — swallow `UnauthorizedAccessException`/
      `IOException` from deleting the non-root container's `dataprotection-keys/` subdir so the suite runs on
      repeat (fixes a permission failure on the second e2e run)

## 3. Restart-persistence scenario (AG-DEP)
- [x] 3.1 `RestartPersistenceTests` (`[Category("EndToEnd")]`): poll `https://tls.local/` until an ACME
      certificate is served and capture thumbprint T1
- [x] 3.2 Restart the proxy via the fixture helper
- [x] 3.3 Poll `https://tls.local/` until an ACME certificate is served and capture thumbprint T2; assert
      `T2 == T1` (certificate reused from the persisted `/certs` volume across the container recreation)

## 4. Verify (AG-DEP)
- [x] 4.1 `dotnet build` green (all projects compile, incl. the E2E suite)
- [ ] 4.2 At the next `E2E` run: `RestartPersistenceTests` passes and `dockyarp.log` shows a second startup
      (the restart) with neither `FileSystemXmlRepository[60]` nor `XmlKeyManager[35]`
