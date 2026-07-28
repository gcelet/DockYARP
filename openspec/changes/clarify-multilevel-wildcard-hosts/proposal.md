## Why
The parity backlog listed `VIRTUAL_HOST` wildcard matching as "single-level `*.suffix` only" (`⚠️`). A closer
read shows that is inaccurate: DockYarp's `RouteMatcher` matches a wildcard by **suffix**
(`host.EndsWith(".suffix")`), so `*.example.com` already matches `a.example.com` **and** `a.b.example.com`; and
YARP's own `Match.Hosts` wildcard is likewise multi-level (`*.domain.com` matches `www.subdomain.domain.com`). So
**multi-level leading wildcards already route end to end** — only there is no test pinning it, and the matrix is
wrong.

The genuinely missing forms are **trailing wildcards** (`foo.bar.*`) and **regex hosts** (`~^…$`). Both are
unsupported by YARP's native host matching and need a custom, non-YARP matching layer (and regex needs ReDoS
guards), so they are split into their own backlog items rather than bundled here.

## What Changes
- Add `RouteMatcher` unit tests pinning multi-level leading-wildcard behavior: a nested subdomain matches
  `*.suffix`, and an exact host still wins over a matching multi-level wildcard.
- Clarify the proxy-routing spec: a wildcard `*.suffix` matches a subdomain of **any depth**.
- Split the remaining gaps into new backlog items `add-trailing-wildcard-hosts` and `add-regex-hosts`; the old
  `add-wildcard-regex-hosts` item is retired and the parity matrix row is split accordingly at archive.

No product code changes — behavior already works; this pins it and corrects the record.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `proxy-routing`: clarifies that wildcard host matching covers nested (multi-level) subdomains.

## Impact
- **Tests**: `tests/DockYarp.Core.Tests/RouteMatcherTests.cs` (multi-level wildcard scenarios).
- **Docs/backlog**: `openspec/backlog/parity.md` (split the wildcard row), new items
  `add-trailing-wildcard-hosts` + `add-regex-hosts`, retire `add-wildcard-regex-hosts`.
- **Owning agent**: AG-RP.
- **Backlog**: resolves the leading-wildcard half of `add-wildcard-regex-hosts`; the rest moves to the two new
  items.
