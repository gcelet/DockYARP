## 1. Shared host matcher (AG-RP)
- [x] 1.1 `DockYarp.Core`: add `HostPattern` (classify exact / leading `*.suffix` / trailing `prefix.*`; expose
      kind + `Matches(host)`), with precedence exact > leading > trailing
- [x] 1.2 `RouteMatcher`: use `HostPattern`; add the trailing-wildcard tier after exact and leading wildcard

## 2. YARP metadata + matcher policy (AG-RP)
- [x] 2.1 `YarpConfigMapper`: for a trailing-wildcard host, omit `Match.Hosts`, set a catch-all `Path`, and record
      `RouteConfig.Metadata["DockYarp.HostTrailing"]`; native forms keep using `Match.Hosts`
- [x] 2.2 `DockYarpHostMatcherPolicy` (`MatcherPolicy` + `IEndpointSelectorPolicy`): invalidate candidates whose
      request `Host` does not match the metadata host pattern; runs after the built-in host policy
- [x] 2.3 Register the policy in DI (`services.AddSingleton<MatcherPolicy, DockYarpHostMatcherPolicy>()`)

## 3. Tests (AG-RP)
- [x] 3.1 `HostPattern`: exact / leading multi-level / trailing matching; classification/precedence
- [x] 3.2 `RouteMatcher`: trailing-wildcard route matches; exact and leading wildcard win over trailing
- [x] 3.3 `DockYarpHostMatcherPolicy`: applies only with the metadata; invalidates a non-matching candidate, keeps
      a matching one

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
