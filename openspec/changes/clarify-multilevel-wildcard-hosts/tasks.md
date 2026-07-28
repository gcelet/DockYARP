## 1. Pin multi-level wildcard behavior (AG-RP)
- [x] 1.1 `RouteMatcherTests`: a nested subdomain (`a.b.local`) matches a `*.local` wildcard route
- [x] 1.2 `RouteMatcherTests`: an exact host wins over a matching multi-level wildcard

## 2. Spec (AG-RP)
- [x] 2.1 proxy-routing spec: clarify that a `*.suffix` wildcard matches a subdomain of any depth

## 3. Split the remaining gaps (AG-RP)
- [x] 3.1 New backlog item `add-trailing-wildcard-hosts` (`foo.bar.*`; needs custom non-YARP matching)
- [x] 3.2 New backlog item `add-regex-hosts` (`~^…$`; custom matcher + compiled-regex cache + ReDoS guards)

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
