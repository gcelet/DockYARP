## 1. Model + parsing (AG-RP)
- [x] 1.1 `LoadBalancingPolicy`: add `PowerOfTwoChoices`, `Random`, `FirstAlphabetical`
- [x] 1.2 `LabelParser.ParsePolicy`: map the three new normalized values; add `HasUnsupportedLoadBalancing`
- [x] 1.3 `ContainerMapper`: warn `unrecognized DOCKYARP_LB; using round-robin` on an unsupported value

## 2. YARP mapping (AG-RP)
- [x] 2.1 `YarpConfigMapper.MapPolicy`: map all enum values to `LoadBalancingPolicies` constants (no local
      string literals)

## 3. Split affinity (AG-RP)
- [x] 3.1 New backlog item `add-session-affinity` (nginx `ip_hash` ≈ YARP session affinity + the Data
      Protection gate: require the DP certificate, fail fast at startup)

## 4. Tests (AG-RP)
- [x] 4.1 `LabelParser`: `power-of-two-choices`/`random`/`first-alphabetical` parse; unknown → null +
      `HasUnsupportedLoadBalancing` true
- [x] 4.2 `YarpConfigMapper`: each policy maps to the matching YARP policy string
- [x] 4.3 `ContainerMapper`: an unknown `DOCKYARP_LB` produces the warning

## 5. Verify (AG-RP)
- [x] 5.1 Nuke `Test` gate green
