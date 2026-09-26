# Hive — Active Work

## Maintenance — UI — Corrective Follow-up

Status: IMPLEMENTATION COMPLETE — VERIFICATION PENDING

Opened: 2026-09-26

### Authorization

This is an explicitly requested bounded corrective follow-up to the completed UI maintenance pass. It restores and hardens existing WinForms lifecycle/threading behavior and does not advance the roadmap or add a new capability.

### Scope

Correct only the concrete production issues identified by the repository review of the preceding UI maintenance result:
- prevent background-thread WinForms interaction from traversing the host control tree before the required UI-thread boundary is enforced;
- ensure cancelled Settings-page initialization can be retried after a rapid navigation sequence;
- prevent synchronous Host Composition disposal from blocking while an async composition operation owns the reconfiguration gate;
- ensure a late candidate produced after disposal is not published or leaked;
- add focused regression coverage for the corrected behavior.

### Explicit exclusions

- Phase 1.15+ work;
- new host integration capabilities or material public-contract expansion;
- business writes, receipts, Review, persistence changes, or provider changes;
- roadmap changes;
- unrelated refactoring.

### Corrective work completed

- WinForms host interactions now enforce the registered-root UI-thread boundary before live host-tree traversal or semantic-provider interaction.
- Settings page initialization now tracks in-flight initialization tasks so a cancelled initialization can be retried when the user navigates back before the previous operation has unwound.
- Host composition disposal no longer synchronously waits on the async reconfiguration gate.
- Host composition publication is state-gated so disposal cannot publish a late candidate, and a cancelled/unpublishable candidate is disposed.
- Focused regression coverage was added for the UI-thread boundary, Settings navigation retry, active composition disposal, and late candidate cleanup.

### Review disposition

The earlier ExpectedHostVersion review observation was rechecked against the authoritative architecture. The V1 contract explicitly makes host version evidence optional, and the current neutral adapter has no generic host-version authority from which it could safely enforce optimistic concurrency. No public-contract change was made.

### Required handoff

Example to run: existing UI Foundation / representative CRUD and Settings surfaces — Hive.Example.WinForms
Tests to run: HiveUiPolishTests.cs; HiveWinFormsHostIntegrationTests.cs; HiveHostCompositionTests.cs; then the full Hive.Tests suite.

No build, test, or manual UI verification has been performed by the agent.
