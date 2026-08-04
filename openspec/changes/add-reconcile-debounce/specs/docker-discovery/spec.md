## ADDED Requirements

### Requirement: Debounced event reconciliation
The system SHALL coalesce a burst of Docker lifecycle events that arrive close together into a single
reconciliation: after an event it SHALL wait a quiet window (`Docker:ReconcileDebounceMin`, extended by each
further event) before reconciling, and SHALL never defer the reconcile longer than a hard cap
(`Docker:ReconcileDebounceMax`) measured from the first event of the burst. A single event with no other
event in the quiet window SHALL still reconcile within that window. Startup and post-reconnect
reconciliations are not event-driven and SHALL remain immediate. Setting `Docker:ReconcileDebounceMin` to
zero SHALL disable debouncing, reconciling once per event.

#### Scenario: Burst coalesced into one reconcile
- **WHEN** several container lifecycle events arrive within the debounce window
- **THEN** a single reconciliation runs for the whole burst instead of one per event

#### Scenario: Sparse event reconciles promptly
- **WHEN** a single lifecycle event arrives and no other event follows within the quiet window
- **THEN** it reconciles within the quiet window

#### Scenario: Unsettled burst flushed at the cap
- **WHEN** events keep arriving without a quiet pause past the hard cap from the first event of the burst
- **THEN** a reconciliation runs at the cap rather than being deferred indefinitely
