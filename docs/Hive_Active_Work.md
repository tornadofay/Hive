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
- `HiveCrudPage` now hosts its existing `HiveListView` through `HiveScrollHost`, keeping native ListView behavior while using Hive-owned scrollbars;
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

## Revision 2 Verification Remediation

The revision audit finding was remediated:
- `HiveComboBox.FieldEditorOnTextChanged` now synchronizes the native text backend directly from the edited field value instead of routing through the public `Text` setter, which avoids the same-value short-circuit.
- The control now raises one reliable `TextChanged` notification after real editable field changes, while preserving the committed selected item/index until selection is explicitly changed.
- `HiveComboBoxTests.EditableFieldChange_RaisesTextChangedAndSynchronizesBaseText` directly exercises the editable field path; the existing programmatic-text regression and execution-target capability regression remain in place.

## Host/UI Control Boundary Correction

The host-integration base controls are explicitly named `HostTextBox`, `HostComboBox`, `HostCheckBox`, `HostDateTimePicker`, `HostNumericUpDown`, and `HostDataGridView`. The Hive-owned presentation controls keep their existing `Hive*` names, including `HiveComboBox`.

`HiveComboBox` no longer embeds a Hive host-integration base control. Its internal field/filter editors are ordinary WinForms `TextBox` controls so the Hive presentation composite does not accidentally introduce host/AI integration controls into its private implementation hierarchy.

`HostComboBox` is handled by the existing native `ComboBox` value-adapter path. The former `HiveComboBoxValueAdapter` is no longer part of the host-integration layer because the Hive-owned `HiveComboBox` is a presentation control rather than a host-integration base.

## Verification Failed / Remediation Required

Developer visual verification reported two in-scope Slice 4 UI defects:
- `HiveComboBox` appeared visually flat/unfinished, and its displayed text was not vertically centered within the control.
- Hive-owned TextBox and TreeView scrolling surfaces still showed native/unmodified scrollbar presentation instead of the intended Hive scrollbar treatment.

Remediation boundary:
- correct the `HiveComboBox` field visual treatment, including depth/border/interaction presentation and reliable vertical text centering, without changing its public semantic contract;
- correct scrollbar integration for the affected Hive-owned TextBox and TreeView surfaces within the existing custom-scroll infrastructure and Slice 4 scope;
- preserve existing specialized control behavior, selection/focus/query state, and the intentional native host-integration control boundary;
- add focused regression coverage for the corrected presentation/integration behavior;
- update UI documentation only where the actual supported behavior changes.

## Verification Failed / Remediation Required — CRUD List Scroll Flicker / Non-Scrolling

Developer verification reported that scrolling the CRUD ListView causes the native scrollbar to flicker back into view and then disappear, with no effective list scrolling. This is an in-scope failure of the Slice 4 ListView scrollbar integration.

Remediation boundary:
- correct the existing ListView-specific native scroll adapter/host synchronization so the native ListView scrollbar remains suppressed without flicker;
- restore actual vertical/horizontal ListView scrolling through the existing native control mechanism;
- preserve existing `HiveListView` owner-draw, selection, keyboard, paging, and CRUD behavior;
- add or strengthen focused regression coverage for native ListView scrolling and suppression lifecycle.

No DataGridView migration, ListView rewrite, new public scroll API, or unrelated UI refactoring is authorized.

## Verification Failed / Remediation Required — CRUD List Scroll Integration

The current Slice 4 verification boundary identified one remaining same-slice integration gap: `HiveCrudPage` uses `HiveListView`, but the CRUD list surface was still using native ListView scrollbars instead of the shared Hive scrollbar infrastructure.

Remediation boundary:
- host the existing `HiveListView` inside `HiveScrollHost` without replacing ListView item, selection, keyboard, owner-draw, or paging behavior;
- extend the existing `HiveNativeScrollAdapter` only for ListView-specific native scroll synchronization;
- add focused regression coverage proving ListView and `HiveCrudPage` use the shared Hive scroll host;
- update UI documentation to distinguish native ListView behavior from Hive-owned scrollbar presentation.

No DataGridView migration, ListView rewrite, new public scroll API, or unrelated UI refactoring is authorized.

## CRUD List Scroll Remediation — Revision

The reported CRUD ListView scroll failure was corrected within the same Slice 4 boundary:
- `HiveListView` now suppresses native scrollbar non-client painting and reapplies suppression around native window-position/scroll/style messages, preventing the default scrollbar from flickering into view;
- `HiveListView` also suppresses native scrollbars when its handle is created;
- `HiveNativeScrollAdapter` keeps ListView scrolling on the native `LVM_SCROLL` path and quantizes report-mode vertical targets to the ListView's row increment, preventing small custom-thumb deltas from rounding to zero and resetting the Hive scrollbar;
- reverse scrolling remains supported by quantizing the requested absolute target rather than constraining it relative to the current position;
- focused ListView regression coverage now verifies actual movement to a row-aligned target and CRUD composition remains hosted by `HiveScrollHost`.

The native ListView item, owner-draw, selection, keyboard, and CRUD paging behavior remains intact. No DataGridView or broader ListView rewrite was introduced.

## Visual / Native Scroll Remediation

The reported visual verification defects were remediated within Slice 4:
- `HiveComboBox` now uses a Hive-rounded field surface with normal, hover, pressed, disabled, and focused border/background states, a separated arrow area, and a centered native text editor sized to its preferred single-line height;
- the editable field editor remains an ordinary WinForms `TextBox`, preserving the corrected text/selection event contract;
- `HiveScrollHost` now has a bounded native-scroll adapter for directly attached multiline `TextBoxBase`, `TreeView`, and `ListView` controls; the native viewport, keyboard input, selection, and control-specific scrolling remain intact while the native scrollbar presentation is hidden and mirrored by HiveScrollBar;
- Hive-owned multiline TextBox surfaces in the Example Test Surface, Example Output, MessageBox technical details, Model Information, and WorkItem rejection dialog now run through `HiveScrollHost`;
- `HiveNavigationTree` instances in the Example Host, Settings, and Advanced Provider Configuration now run through `HiveScrollHost`;
- `HiveCrudPage` now hosts its existing `HiveListView` through `HiveScrollHost`; ListView scrolling uses the existing native control through a bounded ListView-specific adapter path while the visible scrollbars are Hive-owned;
- focused regression coverage was added for `HiveComboBox` field geometry and adapter-backed TextBox/TreeView/ListView scrolling, including direct CRUD composition coverage;
- `docs/ui/controls.md` now documents the bounded native-scroll adapter behavior and the Slice 4 integration boundary.

## CRUD List Scroll Remediation

The remaining Slice 4 CRUD scrollbar gap has been remediated:
- `HiveNativeScrollAdapter` now supports `ListView` and uses the ListView's native `LVM_SCROLL` mechanism for position changes rather than replacing ListView scrolling internals;
- `HiveScrollHost` synchronizes hosted ListView state and coalesces selection-triggered synchronization alongside its existing mouse-wheel/keyboard synchronization paths;
- `HiveCrudPage` now places its `HiveListView` inside `HiveScrollHost`; the existing `HiveListView` public surface and native item/selection behavior remain unchanged;
- focused tests cover both adapter-backed ListView scrolling and the CRUD composition boundary.

## Compilation Remediation

The developer-reported compilation failures were corrected:
- native TextBox/TreeView scroll-state locals were renamed to avoid enclosing-scope conflicts;
- the ComboBox fallback border local was renamed to avoid the themed-branch scope conflict;
- `HiveExampleTestSurface.CreateScrollHost(...)` was restored.

No behavior or scope was expanded by these corrections.

## Verification State

Status: VERIFICATION PENDING

Developer handoff after this remediation:

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: HiveScrollHostTests.cs; HiveComboBoxTests.cs; HiveUiPolishTests.cs; relevant Example Host UI tests; broader-suite requirement: full Hive.Tests suite after the focused tests pass.

Agent has not run the build or tests. Developer verification is required.

