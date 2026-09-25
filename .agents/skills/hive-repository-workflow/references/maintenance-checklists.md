# Hive Maintenance Checklists

These are review checklists, not permission to expand task scope.

## Backend

### Local code
- correctness and invariants
- nullable/public API correctness
- validation and structured errors
- async/cancellation propagation
- concurrency and race prevention
- lifecycle/state transitions
- disposal/resource ownership
- connection/stream/reader lifetime
- unnecessary allocations and expensive operations
- readability/naming/deterministic behavior

### Boundaries
- public API consistency
- dependency direction
- persistence/transaction boundaries
- query efficiency/indexing
- database/network I/O
- authorization/ownership/scope
- credential/secret isolation
- configuration/effective configuration
- timeout/budget/cancellation
- serialization/contracts
- provider failure handling
- stale-result protection
- idempotency
- recovery/failure isolation
- event/lifecycle semantics

### Architecture
- responsibility ownership
- MAF boundary
- orchestration ownership
- persistence ownership
- extensibility/replaceability
- compatibility/versioning
- duplicated responsibility
- unnecessary wrappers
- hidden coupling

## WinForms/UI

### Visual system
- hierarchy and visual rhythm
- spacing/alignment/sizing
- typography/readability
- information architecture
- density/screen-space use
- Light/Dark/System consistency
- semantic contrast

### Interaction
- selected/hover/pressed/focused/disabled/read-only states
- keyboard/focus behavior
- CRUD flow
- editors/dialogs/confirmation
- validation/error/empty/loading/success states
- buttons/inputs/grids/tabs/panels
- primary/secondary/destructive distinction

### Desktop behavior
- resizing/anchoring/docking
- minimum sizes/overflow
- DPI/scaling
- responsiveness
- thread affinity
- disposal/resource ownership
- theme updates without unnecessary layout/repaint

### Architecture
- existing Hive UI API reused
- no wrapper-only controls
- no business logic in reusable UI controls
- persistence/provider/authorization logic remains outside UI
- Example Host remains a consumer

## Host/UI boundary

Pay special attention to:

- host registration/ownership
- composition/service-graph lifecycle
- async event boundaries
- cancellation/disposal
- stale view/result protection
- host/public contract consistency
- secret/error isolation
- native/custom control adaptation
- bounded discovery/interaction authority
- Example Host use of public contracts

Do not implement future host-action/business-write capabilities unless explicitly authorized.
