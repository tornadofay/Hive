# Hive — Active Work

## Maintenance — UI

Status: VERIFICATION PENDING

Opened: 2026-09-26

### Authorization

This is an explicitly requested bounded Maintenance — UI pass. With no roadmap slice active, the repository workflow permits a temporary corrective maintenance slice only for restoring, preserving, or hardening existing WinForms UI behavior. This maintenance does not advance Phase 1.15 or any later roadmap slice.

### Scope

Audit and correct the existing Hive WinForms UI boundary, with emphasis on:
- hierarchy, spacing, typography, density, theme and contrast;
- selected, hover, focus, disabled, and read-only states;
- keyboard/focus, validation, loading, empty, error, and success behavior;
- dialogs, CRUD flows, responsiveness, resize/DPI behavior;
- thread affinity, disposal, repaint/layout efficiency;
- reuse of existing Hive UI APIs and correct responsibility ownership.

### Explicit exclusions

- new capabilities or material public-contract expansion;
- Phase 1.15+ work;
- new settings/configuration/resource domains;
- business writes, receipts, Review, persistence changes, or provider changes;
- host integration contract changes;
- unrelated refactoring;
- redesigning already-verified behavior without a concrete maintenance defect.

### Maintenance findings

1. `HiveEditorLayout.ClearFields()` removes field containers without disposing the controls it owns, which can leak WinForms/GDI resources when fields are rebuilt dynamically.
2. `HiveListPageLayout.SetContent()` removes the previous content control without disposing it, contrary to the documented owner-disposal rule for dynamically replaced children.
3. `HiveCrudPage.ExecuteAsync()` can let an older operation's completion path clear the shared busy state after a newer operation has become current if overlapping callers reach the operation boundary; the cleanup should only release busy state for the current operation.

### Corrective work completed

- `HiveEditorLayout.ClearFields()` now disposes the previously owned field containers/editors after removing them.
- `HiveListPageLayout.SetContent()` now disposes the previously hosted content control when it is replaced, while preserving the incoming content control.
- `HiveCrudPage.ExecuteAsync()` now releases the shared busy state only when its operation is still the current operation, preventing stale cleanup from re-enabling the UI for a newer operation.
- Focused regression tests cover editor-field disposal and replaced list-content disposal.
- The UI controls reference now documents the disposal ownership behavior.

### Required handoff

Example to run: existing UI Foundation / representative CRUD and Settings surfaces — Hive.Example.WinForms
Tests to run: `HiveUiPolishTests.cs`; then the full `Hive.Tests` suite.
Manual verification: existing Example Host UI, Light/Dark/System themes, compact/normal resize, representative dialog/CRUD flows, and affected dynamic content/editor replacement behavior.

No build, test, or manual UI verification has been performed by the agent.
