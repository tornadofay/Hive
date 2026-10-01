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

## Implementation Checkpoint

Slice 4 implementation is complete on `main`.

Implemented integration:
- Settings/configuration selection surfaces now use `HiveComboBox` where the interaction is selection-oriented, including provider onboarding, provider/account and execution-target selectors, model selection, capability state selection, Agent target/generation selection, Persistence authentication selection, and the shared CRUD status filter;
- the Example Host configured-agent selector now uses `HiveComboBox`;
- the editable/autocomplete Provider transport field remains native because its existing native autocomplete semantics do not fit the first-class HiveComboBox contract;
- no existing native production TabControl consumer was found, so `HiveTabControl` remains a reusable foundation with no migration;
- Settings Overview and Advanced Provider Configuration Overview now use `HiveScrollHost` for their intrinsically-sized content;
- focused regression coverage was updated for the migrated controls and the new scroll-host integrations;
- UI control and Example Host guidance documents now describe the Slice 4 integration boundary.


## Implementation Corrections

Post-handoff corrections within Slice 4:
- `HiveScrollHost.Synchronize()` now clamps transiently negative viewport dimensions to zero before constructing `HiveScrollState`, preventing layout-time `ArgumentOutOfRangeException` failures;
- the five Example Host pages that used native `AutoScroll` now use `HiveScrollHost` instead, so their visible scrollbars are Hive-owned;
- the Controls / CRUD example's scroll content was changed to intrinsic sizing for reliable `HiveScrollHost` measurement;
- regression coverage now verifies the migrated Example Host pages have no native `AutoScroll` and are hosted by `HiveScrollHost`.

## Verification Remediation

The reported Slice 4 verification failures were remediated:
- `Hive.Tests` now references `Hive.Example.WinForms`, and the Example Host exposes its existing internal views to `Hive.Tests` through `InternalsVisibleTo`; no production view visibility was widened.
- `HiveScrollHostTests.ZeroSizedHost_DoesNotThrowDuringSynchronization` now asserts the normalized zero-sized viewport state instead of applying `Assert.NotNull` to the value-type `HiveScrollState`.
- `HiveUiPolishTests.HiveAdvancedOverview_UsesHiveScrollHost` no longer treats the non-disposable `HiveThemeManager` as an `IDisposable` resource.

## Verification Remediation

The reported automatic-target verification failure was remediated:
- `HiveComboBox.Text` now preserves the control's base text state before synchronizing the visible field editor, suppresses the intermediate base notification, and raises one reliable `TextChanged` notification after the editable text is committed.
- This restores deterministic model-selection state synchronization when an editable model changes from a discovered model to custom text, allowing the execution-target editor to clear stale discovery-managed capabilities.
- `HiveComboBoxTests.TextChange_RaisesTextChangedForProgrammaticEditableText` provides focused regression coverage for the underlying control contract.

## Verification State

Status: VERIFICATION PENDING

Developer handoff:

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: HiveExecutionTargetDiscoverySettingsTests.cs; HiveComboBoxTests.cs; HiveUiPolishTests.cs; Phase116FollowUpTests.cs; ProviderSettingsIntegrationTests.cs; broader-suite requirement: full Hive.Tests suite after focused coverage passes.

Agent has not run the build or tests. Developer verification is required.
