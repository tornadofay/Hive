# Hive — Active Work

Status: IN PROGRESS

## Authorized Slice

**Phase 1 Custom UI Components — Slice 2: HiveComboBox with Filtering**

Authorization: explicit user command `Hive: start the next ui slice` following the completed Slice 1 and `docs/plan/Phase1/Custom-UI-Components.md`.

Repository checkpoint before implementation: `846b7c26b579c6b540a7748f09863b06604c49eb` on `main`.
Current implementation head: `c973f26a19a787f012be2b8723c5f689dcdefa8c` on `main`.

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

## Implementation Checkpoint

Current implementation includes the Hive-owned composite control, explicit host value adapter, theme-manager/editor-layout integration, focused contract coverage, and the deterministic `UI / Foundation / HiveComboBox` Example Host scenario.

## Verification State

Status: VERIFICATION PENDING

Developer-reported compile failure `CS8604` in the `SelectedValue` member path was remediated within the same slice. A subsequent runtime verification failure reported `System.NullReferenceException` from `HiveComboBox.UpdateFieldLayout()` at line 1056; remediation established the child field editor before initial size changes and added a defensive layout guard with focused construction/resize coverage. A developer analyzer failure `xUnit2013` was remediated by using `Assert.Single`. Focused verification then reported two failures: popup keyboard navigation did not advance the highlight through the field key path, and an unbound selection test expected a display/value contract without configuring the corresponding members. Both were remediated within Slice 2: open-popup `Down/Up` now moves the popup highlight, and the selection test now configures `DisplayMember`/`ValueMember` before asserting the intended display/value behavior.

Developer-reported Example Host verification exposed a UI defect: the HiveComboBox popup list opened scrolled to the end instead of its expected initial selection/top position. Remediation now synchronizes the popup scroll host after the popup has a real viewport and positions it at the highlighted item; focused regression coverage asserts an initial value of zero for the first selected item.

No scope expansion was made. Developer rerun is required.

Required developer handoff:

Example to run: UI / Foundation / HiveComboBox — Hive.Example.WinForms
Tests to run: HiveComboBoxTests.cs and HiveWinFormsBaseControlIntegrationTests.cs; broader-suite requirement: full Hive.Tests suite after focused coverage passes.

Agent has not run the build or tests. Developer verification is required after implementation, including the matching Example Host scenario.
