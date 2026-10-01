# Hive — Active Work

Status: IN PROGRESS

## Authorized Slice

**Phase 1 Custom UI Components — Slice 4: Existing Hive UI Integration, Consistency & Hardening**

Authorization: explicit user command to start the next UI slice in `docs/plan/Phase1/Custom-UI-Components.md` following the completed Slice 3.

Repository checkpoint before implementation: 8f804f2a8554ee1c5bb823b00312268240930649 on `main`.

## Scope

Implement only Slice 4 from the linked plan:

- migrate appropriate existing Hive native ComboBox consumers to the first-class `HiveComboBox` where the semantic behavior fits, prioritizing settings/configuration, provider configuration, model/target selection, and capability-related selection controls;
- preserve existing domain validation, business logic, host-integration metadata, and ordinary native ComboBox support outside migrated Hive-owned surfaces;
- treat `HiveTabControl` as a reusable foundation; migrate no native TabControl consumer unless repository evidence identifies a real existing Hive consumer;
- apply `HiveScrollHost` to appropriate existing Hive-owned scrollable surfaces where the established synchronization contract is reliable, prioritizing shared surfaces over one-off forms;
- preserve Light/Dark/System consistency across integrated controls and surrounding ListView/TreeView/DataGridView surfaces;
- preserve applicable selection, focus, popup/query, scroll, and active-tab state across theme changes and component refreshes;
- remove obsolete per-form styling workarounds only when the shared controls fully replace their responsibility;
- add focused regression coverage for affected integrations, theme/state preservation, scroll integration, and existing native ListView/TreeView/DataGridView behavior where changed;
- update applicable UI documentation and Example Host guidance;
- manually verify representative integrated settings/configuration surfaces through the existing Example Host workflow.

## Explicit Exclusions

Do not implement later roadmap work, redesign Provider/Agent/Workspace/business behavior, replace every WinForms control, replace DataGridView/ListView/TreeView internals, introduce a universal renderer, introduce another theme manager, introduce a new navigation framework, or perform unrelated UI refactoring.

Do not migrate Example Host fixture/example controls merely because they are native when they intentionally demonstrate ordinary WinForms behavior.

## Verification State

Status: IMPLEMENTATION IN PROGRESS

Required handoff after implementation:

Example to run: Settings / Providers / Advanced Provider Configuration / representative integrated settings controls — Hive.Example.WinForms
Tests to run: focused Slice 4 integration/regression tests; broader-suite requirement: full Hive.Tests suite after focused coverage passes.

Agent has not run the build or tests. Developer verification is required after implementation.
