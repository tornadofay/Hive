# Hive — Active Work

## Maintenance — Host/UI — Corrective Follow-up

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

Opened: 2026-09-26

### Authorization

This is an explicitly requested bounded corrective follow-up to the immediately preceding repository review. It restores and hardens existing lifecycle/thread-affinity behavior and does not advance the roadmap or add a new capability.

### Scope

Correct only the three concrete review findings:
- prevent the Host Composition unchanged-configuration path from publishing a non-terminal Ready status after disposal;
- enforce the WinForms UI-thread boundary for host capture before any live host-tree traversal;
- guarantee Host Composition lifetime-CTS cleanup even when the current graph throws during disposal;
- add focused regression coverage for these corrections.

### Corrective work completed

- The unchanged-configuration fast path now synchronizes status publication with the disposal state gate, so terminal Disposed state cannot be overwritten after shutdown.
- Host composition status reads use the same volatile state model as current-graph reads.
- Host Composition lifetime CTS disposal now occurs in a finally block even when disposal of the current graph throws.
- WinForms host capture now enforces root lifecycle/UI-thread checks before and after the host-context capture boundary, while preserving distinct disposed/UI-thread error semantics.
- Focused regression coverage was added for background-thread capture and graph-disposal failure cleanup.

### Explicit exclusions

- Phase 1.15+ work;
- new host integration capabilities or material public-contract expansion;
- business writes, receipts, Review, persistence changes, or provider changes;
- roadmap changes;
- unrelated refactoring.

### Required handoff

Example to run: existing UI Foundation / representative CRUD and Settings surfaces — Hive.Example.WinForms
Tests to run: HiveHostCompositionTests.cs; HiveWinFormsHostIntegrationTests.cs; then the full Hive.Tests suite.

No build, test, launch, migration, or manual UI verification has been performed by the agent.

### Verification failure

Developer verification reported xUnit1031 in `HiveHostCompositionTests.cs`: the `Dispose_StillDisposesLifetimeAfterCurrentGraphThrows` test uses blocking task operations and must be converted to an async test.

Remediation boundary: test-only async conversion of that regression. No production scope changes.
