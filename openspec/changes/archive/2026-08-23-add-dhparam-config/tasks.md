## 1. Document the non-applicability (AG-AT)

- [x] 1.1 Add a note to `docs/tls-acme.md`'s `Configuration (TlsOptions)` section explaining that DH-param
      group configuration (nginx-proxy's `DHPARAM_*`) has no Kestrel/.NET application-level equivalent
      (Windows: SChannel/OS-policy-managed; Linux/macOS: `CipherSuitesPolicy` restricts suite selection but
      not DH-group parameters; TLS 1.3 default doesn't use classic DH at all), verified by the note being
      present next to the existing `CipherSuites` bullet.
- [x] 1.2 Flip `openspec/backlog/parity.md`'s `DH params (DHPARAM_*, per-vhost)` row from ⛔ to ✅, with a
      note that closure is by assessment (non-applicable), not by a built feature, verified by the row
      reading correctly against the legend.
