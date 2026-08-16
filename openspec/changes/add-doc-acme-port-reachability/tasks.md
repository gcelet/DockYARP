## 1. No-remap topology recipe (AG-DOC)

- [x] 1.1 `examples.md`: added the "No host port-remap (macvlan, host networking)" recipe right after
      "Dedicated admin host" — `cap_add: [NET_BIND_SERVICE]`, `Server__HttpPort: "80"`, `Server__HttpsPort:
      "443"`, no `ports:` block, explanation of why (no Docker-level remap in that topology).
- [x] 1.2 Validated: all 14 YAML fenced blocks in `examples.md` (including the new one) parse via
      `yaml.safe_load`.

## 2. Port reachability statement (AG-DOC)

- [x] 2.1 `configuration.md`: added a paragraph to the `Server` section stating the port-80/443 reachability
      requirement, linking to the new Examples recipe for the no-remap case.

## 3. Spec sync prep (AG-DOC)

- [x] 3.1 Verified the delta spec's two MODIFIED requirements ("Application configuration reference", "Worked
      configuration examples") match what actually shipped in sections 1-2.
