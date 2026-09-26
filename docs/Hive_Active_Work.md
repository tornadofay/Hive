# Hive — Active Work

Status: IN PROGRESS

## Authorized slice

**Phase 1.15 — Input Preparation & Routing**

This roadmap slice was explicitly authorized by the `Hive: Start Phase 1.15` request received on 2026-09-26.

### Objective

Prepare supported V1 input sources and route each source through the capability required to produce the common prepared-input boundary that Phase 1.16 will turn into structured candidate data.

### Scope

- `Hive.Core` source-neutral input submission, prepared-input, routing, bounded-input, and per-item failure contracts.
- `Hive.Core` input-preparation engine for the initial V1 image and `.xlsx` spreadsheet paths.
- `Hive.Core` deterministic vision-capability target selection for image inputs using the existing capability-aware ExecutionTarget selection contract.
- `Hive.Management` facade/service integration that obtains eligible execution targets through the existing provider resource boundary and exposes the preparation operation to consumers.
- `Hive.Persistence` provider-resource listing required to obtain all execution targets in one access-scoped query; no new persistence resource/schema.
- `Hive.Example.WinForms` externally usable example covering image routing, spreadsheet worksheet/row mapping, multiple prepared units from one submission, bounded/unsupported input handling, and isolated per-item failures.
- Focused `Hive.Tests` coverage for normal, invalid, boundary, cancellation, routing, spreadsheet parsing/mapping, failure isolation, and authorization/scope behavior for this slice.
- Required architecture/status/Active Work documentation updates.

### Explicit exclusions

- No typed business candidate extraction or validation beyond the prepared-input boundary; that belongs to Phase 1.16.
- No business-operation proposal, host mutation, receipt, Review, or full MAF V1 pipeline; later phases own those boundaries.
- No WorkItem creation redesign or submission persistence model; Phase 1.18 owns submission → WorkItem creation → pipeline composition.
- No new provider transport; image routing selects a vision-capable target but does not perform a provider call in this slice.
- No generic UI automation, cognition, Dreams, Questions, Hive membership, or later roadmap capabilities.
- No unrelated refactoring or dependency additions.

### Verification boundary

Required before marking complete:

- focused deterministic unit tests for input contracts, bounds, routing, workbook/worksheet/row parsing and mapping, cancellation, unsupported input, and failure isolation;
- contract/integration coverage for the access-scoped execution-target listing and Management preparation boundary;
- no real provider/vendor network calls;
- exact Example scenario manual verification in `Hive.Example.WinForms`;
- broader `Hive.Tests` suite result only if actually run by the developer; no unrun verification will be reported as complete.

### Checkpoint

Repository checkpoint at authorization: `5996737f2d94631ed5659a0fb4800e3d9a3fdc31` (`main`).

### Current verification state

**VERIFICATION PENDING.** Implementation has not yet been verified by developer test execution or manual Example-host execution.
