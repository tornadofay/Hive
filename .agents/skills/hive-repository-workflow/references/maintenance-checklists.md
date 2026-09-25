# Hive Maintenance Checklists

Use only the sections relevant to the authorized maintenance boundary. These checklists define review depth, not permission to expand scope.

## Backend / Integration
- root cause, invariants, contracts, validation/errors
- async/cancellation, concurrency, lifecycle/disposal
- I/O, timeouts/budgets, persistence/transactions/indexing
- authorization/ownership/secrets
- serialization/provider/MAF boundaries
- stale-state, idempotency, recovery, observability
- duplicate responsibility, hidden coupling, unnecessary abstraction
- focused regression coverage

## WinForms / UI
- hierarchy, spacing, typography, density, theme/contrast
- selected/hover/focus/disabled/read-only states
- keyboard/focus, validation, loading/empty/error/success
- dialogs, CRUD flows, responsiveness, resize/DPI
- thread affinity, disposal, repaint/layout efficiency
- existing Hive UI API reuse and correct responsibility ownership

## Host / UI Boundary
- host registration/composition/lifetime
- async event and UI-thread boundaries
- cancellation/disposal/stale-view protection
- public contract consistency and secret/error isolation
- bounded discovery/interaction authority
- native/custom control adaptation
- Example Host use of public contracts

Never implement future host actions/business writes or widen the authorized boundary during Maintenance. If a production fix requires new capability or material public-contract expansion, stop and request authorization.
