# Hive — Active Work

Status: IN PROGRESS

## Authorized Slice

**Phase 1 Custom UI Components — Slice 2: HiveComboBox with Filtering**

Authorization: explicit user command `Hive: start the next ui slice` following the completed Slice 1 and `docs/plan/Phase1/Custom-UI-Components.md`.

Repository checkpoint before implementation: `846b7c26b579c6b540a7748f09863b06604c49eb` on `main`.

## Scope

Implement only Slice 2 from the linked plan:

- evolve the existing public `HiveComboBox` from native `ComboBox` inheritance to a Hive-owned composite control;
- preserve the Phase 1.14 `IHiveWinFormsFieldControl`, `HiveIntegration`, and `HiveField` metadata contract;
- define and test the required public selection/data contract: `Items`, data binding, `DisplayMember`, `ValueMember`, `SelectedIndex`, `SelectedItem`, `SelectedValue`, `Text`, enabled/read-only behavior where applicable, and selection-change notification;
- provide Hive-rendered closed-field presentation with Light/Dark/System theme support, focus/hover/disabled/error-compatible states, DPI-aware sizing, dropdown affordance, and accessible role/name/value behavior;
- provide Hive-rendered popup presentation with filtering/search, deterministic source ordering, stable item identity, selected/hover states, empty/no-match state, bounded sizing, screen-edge placement, Escape/Enter behavior, keyboard navigation, mouse selection, and focus restoration;
- reuse Slice 1 `HiveScrollHost` / `HiveScrollBar` for long popup lists;
- handle popup lifecycle, form deactivation, resize/DPI/theme changes, repeated open/close, item-source replacement, empty sources, and programmatic changes while open;
- update `WinFormsControlValueAdapters` for explicit `HiveComboBox` handling while preserving ordinary native `ComboBox` handling;
- audit and migrate only concrete `HiveComboBox` inheritance-dependent consumers identified by repository evidence;
- replace the obsolete native-inheritance test with focused public-contract tests;
- add deterministic `Hive.Example.WinForms` coverage demonstrating filtering, selection, long-list scrolling, keyboard-only interaction, and Light/Dark/System modes;
- update `docs/ui/controls.md` and applicable UI architecture/usage guidance for the new public contract.

## Explicit Exclusions

Do not implement Slice 3 (`HiveTabControl`), Slice 4 broad existing-UI integration/hardening, later roadmap work, a second filtered ComboBox type, provider/model-specific filtering behavior, unrestricted renderer abstractions, or unrelated UI refactoring.

Existing native `ComboBox` instances in Hive production surfaces remain native unless they are an actual `HiveComboBox` consumer or a migration is required by this slice's concrete inheritance audit. Broad replacement of existing native ComboBox usage belongs to Slice 4.

## Verification State

Status: VERIFICATION PENDING

Required developer handoff:

Example to run: UI / Foundation / HiveComboBox — Hive.Example.WinForms
Tests to run: HiveComboBoxTests.cs and HiveWinFormsBaseControlIntegrationTests.cs; broader-suite requirement: full Hive.Tests suite after focused coverage passes.

Agent has not run the build or tests. Developer verification is required after implementation, including the matching Example Host scenario.
