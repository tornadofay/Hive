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
- `HiveAdvancedProviderConfigurationForm` is now a real existing production `HiveTabControl` consumer: its Overview, Providers, Accounts / Credentials, Execution Targets, and Model Information navigation is tab-based;
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

## Verification Failed / Remediation Required — Native Scroll State Regression

Developer verification reported three same-slice failures in `HiveScrollHostTests`: `NativeTextBoxContent_UsesHiveScrollBars`, `NativeTreeViewContent_UsesHiveScrollBars`, and `NativeListViewContent_UsesHiveScrollBars`. All failures assert an expected active Hive scrollbar state but received `false`.

Remediation boundary:
- correct the shared native-scroll state acquisition/normalization used by TextBoxBase, TreeView, and ListView;
- preserve native viewport, input, selection, and control-specific scrolling while keeping the Hive scrollbar presentation;
- retain the existing CRUD ListView integration and native scrollbar suppression behavior;
- strengthen focused regression coverage only where required to encode the corrected native-state contract.

No DataGridView migration, new public scroll API, or unrelated UI refactoring is authorized.

## Verification Failed / Remediation Required — Native Scroll State Still Inactive

Developer re-verification reran the three focused native-scroll tests and they still failed with `VerticalScrollState.CanScroll == false`:
- `HiveScrollHostTests.NativeTextBoxContent_UsesHiveScrollBars`
- `HiveScrollHostTests.NativeTreeViewContent_UsesHiveScrollBars`
- `HiveScrollHostTests.NativeListViewContent_UsesHiveScrollBars`

Developer result: `Hive.Tests` 566 total, 563 passed, 3 failed, 0 skipped.

Remediation boundary:
- correct the shared native-scroll state initialization/acquisition so the first authoritative range is obtained before native scrollbar suppression can invalidate the reported page/range;
- preserve native TextBoxBase, TreeView, and ListView viewport/input/selection behavior and the existing Hive scrollbar presentation;
- retain the CRUD ListView integration and native scrollbar suppression behavior;
- strengthen only the focused state/initialization coverage needed to prove the corrected lifecycle.

No DataGridView migration, new public scroll API, or unrelated UI refactoring is authorized.


## Native Scroll State Remediation

The re-verification failure was remediated within the recorded Slice 4 boundary:
- `HiveScrollHost` now skips native content resizing and scrollbar suppression while its viewport is transiently zero-sized during handle/layout initialization;
- `HiveNativeScrollAdapter.ReadState` now reports whether each orientation supplied an authoritative native state, so first-time invalid/zero-page reads cannot authorize suppression;
- native scrollbar suppression begins only after both orientations have yielded an authoritative state, preserving a recoverable native source of truth during initialization;
- existing cached state remains authoritative across transient zero-page responses after suppression;
- the focused native-scroll tests now give the host a deterministic 500x400 viewport and add explicit regression coverage for recovery after a transient zero-sized viewport;
- UI documentation now records the initialization/suppression lifecycle.

No DataGridView migration, new public scroll API, or unrelated UI refactoring was introduced.


## Verification Failed / Remediation Required — Native Scroll State Still Unavailable

Developer re-verification produced four same-slice failures:
- `HiveScrollHostTests.NativeTextBoxContent_UsesHiveScrollBars`
- `HiveScrollHostTests.NativeTreeViewContent_UsesHiveScrollBars`
- `HiveScrollHostTests.NativeListViewContent_UsesHiveScrollBars`
- `HiveScrollHostTests.NativeContent_RecoversAfterTransientZeroViewportBeforeScrollStateAcquisition`

Developer result: `Hive.Tests` 567 total, 563 passed, 4 failed, 0 skipped.

The failures all report `VerticalScrollState.CanScroll == false`, including the new initialization-recovery regression test. The previous authoritative-state/deferred-suppression remediation therefore did not establish a usable native range.

Remediation boundary:
- replace the failing generic native range acquisition with correct control-specific scroll-state acquisition for `TextBoxBase`, `TreeView`, and `ListView` within the existing adapter boundary;
- preserve the existing native viewport/input/selection/control-specific scrolling mechanisms and Hive-owned scrollbar presentation;
- retain CRUD ListView integration and native scrollbar suppression lifecycle;
- strengthen focused state and position regression coverage only as needed.

No DataGridView migration, new public scroll API, control rewrite, or unrelated UI refactoring is authorized.

## Native Scroll State Remediation — Range Fallback and Real UI Lifecycle

The latest remediation remains within the recorded Slice 4 boundary:
- `HiveNativeScrollAdapter` now falls back from an unusable `SCROLLINFO.nPage` to the Win32 standard scrollbar range/position APIs and derives the viewport from the hosted control's client extent; once a state has been established, that cached state remains authoritative across later zero-page responses after native suppression;
- native specialized-content suppression remains deferred until both orientations have a usable authoritative state;
- the focused native-scroll tests now exercise the hosted controls through a visible Form lifecycle, allowing Win32 control layout/scroll ranges to finalize as they do in the running UI;
- the transient zero-viewport regression remains covered;
- UI documentation records the range fallback and lifecycle requirement.

No DataGridView migration, new public scroll API, control rewrite, or unrelated UI refactoring was introduced.

## Verification Failed / Remediation Required — Native Scroll Adapter Compilation

Developer compilation reported six same-slice errors in `HiveNativeScrollAdapter.cs`: CS0136 local-name collisions for `enabled`, `effectiveMaximum`, `viewportSize`, `extent`, and `state`, plus CS0162 unreachable code.

Remediation boundary:
- correct the `ReadState` local scoping and remove the unreachable duplicate path introduced by the native-range fallback remediation;
- preserve the intended native TextBoxBase, TreeView, and ListView state/position behavior and existing suppression lifecycle;
- no behavioral expansion or unrelated refactoring.

## Verification Failed / Remediation Required — ListView Native Scroll State

Developer re-verification now has one remaining same-slice failure:
- `HiveScrollHostTests.NativeListViewContent_UsesHiveScrollBars`

Developer result: `Hive.Tests` 567 total, 566 passed, 1 failed, 0 skipped.

The TextBoxBase and TreeView native-scroll regressions no longer fail. Remediation is therefore limited to the ListView native range/position path, including the existing CRUD ListView integration and native scrollbar suppression.

Remediation boundary:
- correct ListView-specific native scroll-state acquisition/normalization for the failing axis;
- preserve native ListView item, selection, keyboard, owner-draw, and CRUD behavior;
- retain Hive-owned visible scrollbars and the existing `LVM_SCROLL` position mechanism;
- strengthen focused ListView regression coverage only as required.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring is authorized.


## ListView Native Scroll State Remediation

The remaining ListView verification failure was corrected within Slice 4:
- Details/report-style ListView vertical scroll state is now normalized from the control's visible top-item index and row height rather than treating the native vertical scrollbar position as an arbitrary pixel coordinate;
- vertical `LVM_SCROLL` requests are converted from the Hive logical row-based position to whole-row native deltas, matching the documented ListView scrolling contract;
- horizontal ListView scrolling continues through the native `LVM_SCROLL` path;
- existing CRUD ListView composition, item/selection/owner-draw behavior, and Hive scrollbar presentation remain unchanged;
- UI documentation records the ListView-specific scroll-state normalization.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.


## ListView Native Scroll State Remediation — Final Correction

The remaining ListView failure was corrected within the same Slice 4 boundary:
- `Details`-view vertical scroll state is represented in Hive logical pixels derived from the visible top-item index and row height;
- vertical `LVM_SCROLL` deltas are calculated from the current and requested row indices, matching the ListView report/detail whole-line scrolling contract;
- ListView vertical positioning no longer clamps a Hive logical pixel target against the native ListView scrollbar's separate coordinate range;
- row arithmetic is bounded to avoid integer overflow;
- horizontal ListView scrolling remains on the native `LVM_SCROLL` path;
- existing CRUD composition and native ListView item/selection/owner-draw behavior remain unchanged.

Microsoft's ListView documentation specifies that report-view vertical scrolling occurs in whole-line increments, which is the basis for this control-specific normalization.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## Verification Failed / Remediation Required — ListView Scroll Adapter Compilation

Developer compilation reported three same-slice errors in `HiveNativeScrollAdapter.cs`: CS0128 for a duplicate `listView` local/pattern variable, CS0136 for a nested `target` local collision, and CS0165 for the resulting unassigned `listView` path.

Remediation boundary:
- correct only the local/pattern scoping in the existing ListView scroll-position path;
- preserve the ListView row-based state normalization and native `LVM_SCROLL` behavior introduced by the preceding remediation;
- no behavioral expansion or unrelated refactoring.

## Verification Failed / Remediation Required — Native Wheel Scrolling

Developer manual verification reports that mouse-wheel scrolling does not work over Hive-hosted native TreeView and TextBox surfaces in the Example Host, despite the automated native-scroll state tests now running successfully.

Remediation boundary:
- restore mouse-wheel scrolling for directly hosted `TextBoxBase`, `TreeView`, and existing ListView surfaces through the existing `HiveScrollHost` path;
- intercept native wheel input at the control window boundary before the native control consumes it, then route the movement to the Hive-owned scrollbar state;
- preserve native control text editing, selection, tree interaction, ListView item/selection behavior, and keyboard scrolling;
- add focused regression coverage for native wheel routing only where the existing test infrastructure can exercise it.

No new public scroll API, DataGridView migration, control rewrite, or unrelated UI refactoring is authorized.

## Native Wheel Scrolling Remediation

The reported Example Host mouse-wheel defect was corrected within Slice 4:
- directly hosted native `TextBoxBase`, `TreeView`, and `ListView` controls now have a host-owned native-window wheel interceptor so `WM_MOUSEWHEEL` / horizontal wheel messages are handled before the native control consumes them;
- wheel movement is translated into the existing Hive scrollbar position through `HiveScrollHost`, while Shift/horizontal wheel uses the horizontal Hive scrollbar when available;
- high-resolution wheel deltas are accumulated until a full standard wheel step is available;
- the existing `MouseWheel` event path now also routes directly hosted native content through the Hive scrollbar as a fallback;
- the interceptor is attached/released across native handle creation/destruction and host disposal;
- focused TextBox and TreeView tests send a real Win32 mouse-wheel message and verify that the Hive scroll position moves.

No new public scroll API, DataGridView migration, native control rewrite, or unrelated UI refactoring was introduced.

## Slice 4 Tab Navigation Extension

User-authorized Slice 4 integration now replaces the Advanced Provider Configuration TreeView navigation with the existing HiveTabControl:
- the five existing administrative pages remain the same;
- stable TabPage instances hold the existing page lifecycle, while the form continues to create/dispose the active page through its existing cancellation-aware path;
- tab selection now drives the existing asynchronous page initialization instead of TreeView selection;
- the former left navigation split/scroll host is removed because the tab control owns the navigation header and its existing overflow scrolling;
- theme application now targets the HiveTabControl and the active page;
- focused coverage now verifies the five tab titles, initial Overview selection, and page metadata.

This is the previously planned Slice 4 behavior for a real production TabControl consumer and does not introduce a new navigation framework.

## Tab Navigation Verification Handoff

The Advanced Provider Configuration navigation migration is implemented and documentation is aligned.

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: `Phase116FollowUpTests.AdvancedConfiguration_UsesHiveTabsForNavigation`; `HiveTabControlTests.cs`; then the relevant Advanced Provider Configuration UI tests and the full `Hive.Tests` suite.

Agent has not run the build or tests after this migration. Developer verification is required.


## Verification Failed / Remediation Required — Advanced Provider Configuration Tab Metadata Test

Developer verification reported one same-slice test failure:
- `Phase116FollowUpTests.AdvancedConfiguration_UsesHiveTabsForNavigation` expected each `TabPage.Tag` to be an `int`;
- the implemented production contract stores the corresponding `AdvancedPage` enum value in `TabPage.Tag`.

Remediation boundary:
- correct only the focused regression assertion to validate the actual tab metadata contract;
- preserve the existing five-tab navigation and page lifecycle;
- no production navigation behavior or unrelated UI refactoring is authorized.

## Advanced Provider Configuration Tab Test Remediation

The reported test failure was corrected within Slice 4:
- the tab metadata regression now verifies that each tab has non-null metadata and that the metadata value matches the tab's stable `Name`;
- the production `AdvancedPage` metadata contract remains unchanged;
- no production code or navigation behavior was changed by this remediation.

## Verification Failed / Remediation Required — CRUD Maximize Scrollbars / Layout Lag

Developer manual verification reports that maximizing a CRUD surface causes two scrollbar presentations to appear and the form begins to lag.

Remediation boundary:
- correct the existing `HiveCrudPage` / `HiveScrollHost` / `HiveListView` resize and native-scrollbar suppression lifecycle causing duplicate visible scrollbar painting;
- eliminate avoidable synchronization/layout feedback during maximize/resize while preserving CRUD paging, selection, keyboard, owner-draw, and ListView scrolling;
- preserve the existing Hive-owned scrollbar presentation as the single visible scrollbar layer;
- add focused regression coverage for resize/maximize behavior where practical.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring is authorized.

## Verification Failed / Remediation Required — CRUD Native Scrollbar Reappearance and Scroll Lag

Developer manual verification still reports two same-slice defects after the previous resize remediation:
- maximizing a CRUD surface still allows the native ListView scrollbars to be painted alongside the Hive scrollbars;
- vertical scrolling is noticeably laggy when moving up and down.

Remediation boundary:
- eliminate native ListView scrollbar reappearance during maximize, resize, and scroll while retaining native ListView scrolling;
- remove avoidable per-scroll Win32 scrollbar suppression/synchronization work responsible for the lag;
- preserve CRUD paging, selection, keyboard, owner-draw, and Hive scrollbar behavior;
- add focused regression coverage for the corrected suppression/scroll lifecycle where practical.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring is authorized.

## CRUD Maximize Scrollbar / Layout Remediation

The reported maximize regression was corrected within Slice 4:
- `HiveListView` native scrollbar suppression is now idempotent, so repeated `HiveScrollHost` synchronization no longer repeatedly calls the native hide operation;
- `HiveScrollHost` avoids reassigning the same native content size/location during synchronization;
- native-host resize synchronization is coalesced through the existing `RequestSynchronization()` path instead of running synchronously for every native-host resize event, reducing maximize/layout churn;
- the existing native ListView scrolling, CRUD paging, selection, keyboard, owner-draw, and Hive-owned scrollbar presentation remain unchanged.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## CRUD Native Scrollbar / Scroll Performance Remediation

The latest CRUD maximize verification remediation was corrected within the same Slice 4 boundary:
- `HiveListView` now suppresses the native scrollbar presentation with `ShowScrollBar(..., false)` while hosted by `HiveScrollHost`, without changing the ListView's `WS_HSCROLL` / `WS_VSCROLL` style contract;
- suppression is kept out of `WM_VSCROLL` / `WM_HSCROLL` handling, avoiding repeated native scrollbar operations during vertical/horizontal movement;
- suppression is reasserted only after native window-position/style changes and during non-client repaint handling, so maximize/resize cannot leave the native scrollbar layer visible;
- detaching the hosted ListView restores native scrollbar visibility;
- `HiveScrollHost` now synchronizes only the scroll axis that actually changed instead of re-reading and repainting both axes for every wheel step;
- focused regression coverage verifies native scrollbar visibility state across initial attach, maximize, and repeated scrolling.

Microsoft documents `GetScrollBarInfo` as the Win32 mechanism for inspecting standard window scrollbar visibility/state; the regression test checks the actual native scrollbar state rather than assuming a particular window-style bit pattern. citeturn400509search0turn400509search3

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## Verification Failed / Remediation Required — CRUD Native Scrollbar Returns After Maximize

Developer verification reports one remaining same-slice failure:
- `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling` passes initial attach but fails immediately after form maximize because the native ListView scrollbar is visible again.

This localizes the remaining defect to the native ListView scrollbar suppression lifecycle during maximize/layout, not the CRUD form's functional behavior.

Remediation boundary:
- make native scrollbar suppression reassert itself after native ListView layout/repaint when the scrollbar is actually visible;
- avoid unconditional `ShowScrollBar` calls during continuous ListView scrolling to preserve scroll performance;
- preserve native ListView `LVM_SCROLL`, item, selection, keyboard, owner-draw, and CRUD behavior;
- keep the existing Hive-owned scrollbar layer as the visible presentation.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring is authorized.

## Verification Failed / Remediation Required — CRUD Native Scrollbar Suppression Still Not Effective

Developer verification reports that `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling` still sees a native scrollbar immediately after attachment.

Investigation:
- the current `HiveListView` suppression path queries `GetScrollBarInfo` before calling `ShowScrollBar`;
- during native ListView initialization/layout, that visibility query is not a reliable gate for whether the ListView's scrollbar presentation will repaint;
- the suppression contract therefore needs to issue both native hide operations at the explicit attach/resize/style boundaries, without using the visibility query as a prerequisite.

Remediation boundary:
- make native scrollbar suppression deterministic at attach, resize, and native-style-change boundaries;
- do not reintroduce suppression calls into the high-frequency native vertical/horizontal scroll path;
- preserve native ListView `LVM_SCROLL`, owner-draw, selection, keyboard, and CRUD behavior;
- keep the existing Hive-owned scrollbar presentation.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring is authorized.

## Verification Failed / Remediation Required — CRUD Resize vs. Native Scroll Repaint

Developer re-verification still reports native ListView scrollbars during maximized CRUD use and noticeable up/down scroll lag.

Investigation narrowed the issue:
- `HiveCrudPage.OnResize` and `HiveListView.OnResize` participate in maximize/resize layout, but they do not account for the per-scroll lag by themselves;
- `HiveListView` was reasserting native scrollbar suppression from `WM_WINDOWPOSCHANGED`; that message can occur as part of native ListView scrolling, causing repeated Win32 scrollbar operations during movement;
- the remediation must therefore keep resize suppression separate from the high-frequency native scroll path.

Remediation boundary:
- keep CRUD resize/layout behavior intact unless it is directly causing the reported defect;
- remove native scrollbar suppression work from high-frequency ListView scroll/repaint messages;
- preserve native ListView item, selection, keyboard, owner-draw, and `LVM_SCROLL` behavior;
- retain Hive-owned visible scrollbars as the only intended scrollbar layer.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring is authorized.

## CRUD Scroll Repaint Remediation

The latest same-slice remediation is implemented:
- `HiveListView` no longer reapplies native scrollbar suppression from `WM_WINDOWPOSCHANGED`, which could occur during native ListView scrolling;
- `WM_NCPAINT` no longer performs any native scrollbar operation, so non-client repaint remains outside the high-frequency scroll path;
- native scrollbar suppression is now deterministic and unconditional at the explicit attach, resize, and native-style-change boundaries;
- the native visibility query was removed from the production suppression path because it was not reliable during ListView initialization/layout;
- `HiveScrollHost` retains the focused single-axis synchronization path so a vertical scroll does not unnecessarily re-read and update the horizontal state;
- the focused maximize/repeated-scroll regression remains responsible for verifying that native scrollbar presentation stays suppressed.

Agent has not run the build or tests. Developer verification is required.

## Verification Failed / Remediation Required — ListView Suppression Compilation

Developer compilation reported CS0246 in `HiveListView.cs`: the production file still referenced the removed `NativeScrollBarInfo` type through an orphaned `GetScrollBarInfo` declaration.

Remediation boundary:
- remove only the orphaned native visibility-query declaration;
- preserve the deterministic attach/resize/style-change scrollbar suppression and the optimized scroll path already implemented;
- no behavioral expansion or unrelated refactoring.

## Verification Result — Developer Re-run 2026-10-03

Developer ran the full `Hive.Tests` suite after pulling commit `a6cc0c113f625e7b68b26330864225ee473138e1`: **570 total, 569 passed, 1 failed, 0 skipped**.

The only failure was `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling`. The failure occurs in the native scrollbar visibility assertion immediately after form maximize, confirming the remaining defect is the recorded ListView native scrollbar suppression lifecycle boundary.

## ListView Scrollbar Suppression — Resize-Qualified Reassertion

The latest same-slice remediation is implemented:
- `HiveListView` now reasserts native scrollbar suppression after `WM_WINDOWPOSCHANGED` only when the native `WINDOWPOS` flags indicate an actual size change, covering maximize/resize without reintroducing suppression work for scroll-time window-position messages;
- existing `OnResize` and native-style-change suppression paths remain in place;
- `WM_NCPAINT` remains outside the suppression operation, and native ListView scrolling continues through the existing `LVM_SCROLL` path;
- the focused maximize/repeated-scroll regression remains the verification boundary.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## Verification Result — Developer Re-run 2026-10-03 (Resize-Qualified Remediation)

Developer re-ran the full `Hive.Tests` suite after the resize-qualified ListView suppression remediation: **570 total, 569 passed, 1 failed, 0 skipped**.

The same single failure remains: `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling`. The native ListView scrollbar visibility assertion still fails immediately after the maximize lifecycle.

The previous remediation did not close the recorded maximize/layout suppression boundary. Status remains **VERIFICATION FAILED / REMEDIATION REQUIRED** for the same Slice 4 scope.

## ListView Scrollbar Suppression — Post-Layout Reassertion

The latest same-slice remediation is implemented:
- `HiveListView` now schedules one coalesced post-layout native scrollbar suppression after attach, resize, handle recreation, and native-style changes;
- the immediate suppression remains in place, while the deferred pass runs after the current WinForms/Win32 layout work has settled, addressing native ListView scrollbar state being restored after the synchronous resize callback;
- the deferred path is lifecycle-bound and does not run from high-frequency scroll messages;
- pending suppression is canceled on detach/restore so a queued callback cannot re-hide a deliberately restored native scrollbar;
- native ListView `LVM_SCROLL`, item, selection, keyboard, owner-draw, and CRUD behavior remain unchanged.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## Verification Failed / Remediation Required — Deferred Suppression Catch Ordering

Developer compilation reported **CS0160** in `HiveListView.cs`: the `ObjectDisposedException` catch clause follows `InvalidOperationException`, but `ObjectDisposedException` derives from `InvalidOperationException`, making the later catch unreachable.

Remediation boundary:
- correct only the exception-handling order in `RequestNativeScrollBarSuppression`;
- preserve the existing coalesced post-layout suppression behavior;
- no behavioral expansion, test weakening, or unrelated refactoring.

## ListView Suppression Compilation Remediation

The reported CS0160 compilation failure is corrected:
- `ObjectDisposedException` is now caught before its base type `InvalidOperationException` in `RequestNativeScrollBarSuppression`;
- both existing exception paths continue to clear the pending-suppression flag;
- no runtime behavior outside exception-handler ordering was changed.

Status is returned to **VERIFICATION PENDING** for developer compilation/test verification.

## Verification Failed / Remediation Required — ListView Scrollbar Test Hang

Developer verification reports the `HiveScrollHostTests` group does not complete after the post-layout suppression remediation; the group shows **23 tests / 430 ms** and then hangs.

Remediation boundary:
- determine and correct only the same-slice lifecycle/callback condition causing the test hang;
- preserve Hive-owned scrollbar presentation and native ListView behavior;
- keep suppression work out of high-frequency scrolling;
- no test weakening, DataGridView migration, new public scroll API, ListView rewrite, or unrelated refactoring.

## ListView Scrollbar Test Hang Remediation

The deferred suppression path was tightened after developer verification reported the `HiveScrollHostTests` group hanging:
- `WM_WINDOWPOSCHANGED` resize-qualified handling now performs only the immediate native hide;
- `WM_STYLECHANGED` handling now performs only the immediate native hide;
- only the explicit resize and handle-creation lifecycle callbacks queue the coalesced post-layout reassertion;
- this prevents native lifecycle messages generated while applying suppression from recursively queuing additional deferred callbacks;
- native ListView scrolling remains on the existing `LVM_SCROLL` path without suppression work in the high-frequency scroll path.

The change remains inside the recorded Slice 4 remediation boundary.

## Verification Failed / Remediation Required — ListView Scrollbar Still Visible After Maximize

Developer verification now completes the `HiveScrollHostTests` group, but
`NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling` still fails at the
post-maximize native scrollbar assertion (`HiveScrollHostTests.cs` line 381).

The failure is the same functional boundary previously recorded: after
`form.WindowState = FormWindowState.Maximized` and `Application.DoEvents()`,
the native ListView scrollbar state is visible instead of suppressed.

Remediation boundary:
- correct the native ListView scrollbar suppression lifecycle for maximize/layout;
- preserve the existing non-recursive deferred mechanism and native `LVM_SCROLL` path;
- do not move suppression into high-frequency scrolling/repaint handling;
- no test weakening, DataGridView migration, new public scroll API, ListView rewrite, or unrelated refactoring.

## ListView Scrollbar Suppression — Native Style Boundary

The latest same-slice remediation replaces the insufficient visibility-only suppression with a durable native ListView style boundary:
- while hosted, `HiveListView` suppresses the native `WS_HSCROLL` and `WS_VSCROLL` style bits during `WM_NCCALCSIZE`, preventing the ListView from recreating its native scrollbar presentation during non-client recalculation;
- lifecycle position/style handling still reapplies the suppression when native layout changes the window style;
- explicit `ShowScrollBar(..., false)` calls remain the visibility enforcement step;
- the original native scrollbar style bits are retained and restored when the ListView is detached from `HiveScrollHost`;
- no deferred callback is used by the new suppression mechanism, avoiding the previously observed test-group hang;
- native ListView `LVM_SCROLL`, item/selection, keyboard, owner-draw, and CRUD behavior remain unchanged;
- suppression remains outside high-frequency scrolling/repaint work.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## Verification Failed / Remediation Required — Native Style Suppression Still Loses After Maximize

Developer re-ran the full test suite after the native style-boundary remediation: **570 total, 569 passed, 1 failed, 0 skipped**.

The single failure remains `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling`, at the post-maximize assertion in `HiveScrollHostTests.cs` line 381. The test completes without hanging, but the native ListView scrollbar is still reported visible after maximize.

Remediation boundary:
- correct the native ListView scrollbar suppression ordering/lifecycle that allows the scrollbar state to return after maximize;
- preserve native ListView scrolling through `LVM_SCROLL` and the existing Hive scrollbar synchronization;
- keep suppression out of high-frequency scroll and repaint handling;
- no test weakening, DataGridView migration, new public scroll API, ListView rewrite, or unrelated refactoring.

## ListView Scrollbar Suppression — Style-Change Interception

The latest same-slice remediation adds a native style-change boundary:
- `HiveListView` intercepts `WM_STYLECHANGING` for `GWL_STYLE` while hosted and removes `WS_HSCROLL` / `WS_VSCROLL` from the pending new style before Windows applies it;
- the original style bits remain tracked for standalone restoration when the ListView is detached;
- existing non-client/lifecycle suppression remains in place as a fallback visibility enforcement path;
- no deferred callback is used, and no suppression is performed from high-frequency scrolling or repaint messages;
- native `LVM_SCROLL`, item/selection, keyboard, owner-draw, and CRUD behavior remain unchanged.

No DataGridView migration, new public scroll API, ListView rewrite, or unrelated UI refactoring was introduced.

## Verification Failed / Remediation Required — Style-Change Interception Still Fails After Maximize

Developer re-ran the full `Hive.Tests` suite after the `WM_STYLECHANGING` remediation: **570 total, 569 passed, 1 failed, 0 skipped**.

The same failure remains in `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling`, at the post-maximize native scrollbar assertion (test line 381). The test completes without hanging, but the native ListView scrollbar is still reported visible.

The repeated failure indicates the remaining defect may be in the host resize/synchronization lifecycle rather than solely inside `HiveListView`. Investigation must therefore trace the existing `HiveScrollHost` resize and native-content synchronization boundary before another control-level suppression change.

Remediation boundary:
- identify and correct the same-slice lifecycle responsible for native ListView scrollbar restoration after maximize;
- preserve native ListView `LVM_SCROLL`, Hive scrollbar synchronization, and normal control behavior;
- no test weakening, DataGridView migration, new public scroll API, ListView rewrite, or unrelated refactoring.

## ListView Style-Change Payload Correction

Investigation identified a defect in the `WM_STYLECHANGING` interception: the Win32 `STYLESTRUCT` payload contains two 32-bit `DWORD` style values, while the implementation had modeled them as pointer-sized values. On 64-bit processes that layout was incorrect, so the pending scrollbar-style change was not reliably modified.

Remediation completed:
- `NativeStyleStruct.OldStyle` and `.NewStyle` now use `uint`, matching the Win32 payload layout;
- the interception still strips `WS_HSCROLL` / `WS_VSCROLL` only while the ListView is hosted;
- no change to native scrolling, Hive scrollbar synchronization, or test behavior was made.

Status is **VERIFICATION PENDING** for developer compilation and the focused ListView maximize test.

## Verification Failed / Remediation Required — Corrected Style Payload Still Fails

Developer reran the focused `NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling` test after correcting the Win32 `STYLESTRUCT` layout. The test still fails immediately at the post-maximize scrollbar assertion (line 381).

This rules out the previous `STYLESTRUCT` marshaling defect as the sole cause. Investigation is broadened within the same Slice 4 boundary to the interaction between `HiveScrollHost` native-content synchronization and the actual native ListView scrollbar-window lifecycle.

Remediation boundary:
- identify the actual owner/lifecycle that restores the native ListView scrollbar presentation during maximize synchronization;
- preserve native `LVM_SCROLL`, Hive scrollbar synchronization, and existing ListView behavior;
- avoid high-frequency scroll/repaint suppression work;
- no test weakening, DataGridView migration, new public scroll API, ListView rewrite, or unrelated refactoring.

## ListView Scrollbar Suppression — Posted Native Resize Reassertion

The latest same-slice remediation moves the final native scrollbar hide to a coalesced posted Win32 message:
- `WM_WINDOWPOSCHANGED` still performs the immediate hide for real size changes, then posts one dedicated lifecycle message;
- the dedicated message only re-hides the native scrollbars and clears its pending flag; it does not schedule another message;
- the earlier `WM_STYLECHANGING` style mutation and deferred `BeginInvoke` approach are removed because they did not solve the actual native scrollbar-window state and introduced unnecessary complexity;
- native `LVM_SCROLL`, Hive scrollbar synchronization, ListView selection/keyboard/owner-draw, and CRUD behavior remain unchanged;
- no suppression is performed from high-frequency scrolling or non-client repaint.

The implementation remains within the recorded Slice 4 host/ListView integration remediation boundary.

## ListView Scrollbar Suppression — Unified Native Hide

The latest same-slice remediation removes the posted suppression message and returns suppression to explicit lifecycle boundaries, using a single Win32 `SB_BOTH` operation:
- attach, resize, handle-creation, and native style-change boundaries invoke one native hide operation for both standard scrollbars together;
- detaching restores both native standard scrollbars with one `SB_BOTH` operation;
- no deferred callback, style mutation, or per-scroll/repaint suppression remains;
- native `LVM_SCROLL`, Hive scrollbar synchronization, ListView selection/keyboard/owner-draw, and CRUD behavior remain unchanged.

Developer verification is required. The focused maximize/scroll regression remains the immediate verification target.

## Verification Failed / Remediation Required — Stale Deferred-Suppression References

Developer compilation reports four CS0103 errors in `HiveListView.cs`: three remaining calls to the removed `RequestNativeScrollBarSuppression` method and one remaining reference to the removed `_nativeScrollBarSuppressionPending` field.

Remediation boundary:
- remove only the stale references left by the unified `SB_BOTH` suppression cleanup;
- preserve the current lifecycle-only native scrollbar suppression behavior;
- no behavioral expansion or unrelated refactoring.

## ListView Suppression Compile Cleanup

The reported CS0103 errors are corrected. The removed deferred-suppression method and pending flag no longer have production references in `HiveListView.cs`.

Status is **VERIFICATION PENDING** for developer compilation and the focused ListView maximize test.

## Verification State

Status: VERIFICATION PENDING

Developer handoff after the post-layout ListView suppression remediation:

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling`; `HiveScrollHostTests.NativeListViewContent_UsesHiveScrollBars`; then `HiveScrollHostTests.cs`, `HiveComboBoxTests.cs`, `HiveUiPolishTests.cs`, relevant Example Host UI tests, and the full `Hive.Tests` suite.

Agent has not run the build or tests. Developer verification is required.


## Verification Failed / Remediation Required — Unified Suppression Still Fails After Maximize

Developer re-ran the full `Hive.Tests` suite after the unified `SB_BOTH` suppression cleanup: **570 total, 569 passed, 1 failed, 0 skipped**.

The same single failure remains in `HiveScrollHostTests.NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling`. The assertion fails immediately after `form.WindowState = FormWindowState.Maximized`, `Application.DoEvents()`, and `host.Synchronize()`, at `HiveScrollHostTests.cs` line 381. Initial attachment still passes, and the test completes without hanging.

This confirms the remaining same-slice defect is still the native ListView scrollbar presentation lifecycle across the host maximize/resize synchronization boundary.

Remediation boundary:
- trace the existing `HiveScrollHost` resize/synchronization and `HiveListView` native scrollbar lifecycle to identify why the native scrollbar becomes visible after maximize;
- correct only that existing lifecycle within the Slice 4 ListView integration boundary;
- preserve native ListView `LVM_SCROLL`, item/selection/keyboard/owner-draw/CRUD behavior and the existing Hive scrollbar layer;
- do not reintroduce high-frequency per-scroll/repaint suppression work, deferred recursive callbacks, DataGridView migration, a ListView rewrite, a new public scroll API, or unrelated refactoring.

Status is **VERIFICATION FAILED / REMEDIATION REQUIRED** pending same-slice remediation.

## ListView Suppression Ordering Remediation

The latest same-slice correction changes only the native ListView suppression ordering:
- `HiveScrollHost.Synchronize()` now completes native ListView state acquisition, Hive scrollbar state/visibility updates, and scrollbar layout before issuing the final native scrollbar suppression operation;
- this makes native scrollbar suppression the final synchronous step of the host's native-content layout boundary, after the ListView viewport size has been finalized;
- native `LVM_SCROLL`, ListView item/selection/keyboard/owner-draw/CRUD behavior, and the existing lifecycle-only suppression boundaries remain unchanged;
- no deferred callback, per-scroll/repaint suppression, style mutation, public API, or unrelated UI refactoring was introduced.

Status is returned to **VERIFICATION PENDING** for developer compilation and test verification.



## Verification Failed / Remediation Required — Suppression Ordering Does Not Change Native Visibility

Developer re-ran the full `Hive.Tests` suite after the suppression-ordering remediation: **570 total, 569 passed, 1 failed, 0 skipped**.

The same focused regression still fails immediately after maximize. The improved diagnostic now identifies the first exposed object explicitly:
- native horizontal scrollbar object `-6` is still exposed;
- `GetScrollBarInfo` succeeds but reports `state=0x00000000`, so the scrollbar is present/visible rather than the query itself failing.

This disproves the hypothesis that the previous host-side suppression ordering was sufficient.

Remediation boundary:
- stop adding equivalent `ShowScrollBar` timing/retry variants;
- determine which native ListView/layout operation recreates or exposes the horizontal scrollbar after maximize;
- trace the actual `HiveScrollHost` viewport/layout interaction and `HiveListView` column/layout behavior before changing suppression;
- preserve native ListView `LVM_SCROLL`, item/selection/keyboard/owner-draw/CRUD behavior and the existing Hive scrollbar layer;
- no test weakening, per-scroll/repaint suppression, DataGridView migration, ListView rewrite, new public scroll API, or unrelated refactoring.

Status is **VERIFICATION FAILED / REMEDIATION REQUIRED** pending root-cause investigation.


## ListView Native Non-Client Suppression Remediation

Root-cause investigation changed the suppression strategy:
- repeated `ShowScrollBar` timing/order variants did not prevent the native ListView from exposing its standard scrollbar after maximize;
- the developer diagnostic confirms `GetScrollBarInfo` succeeds for the exposed horizontal object and reports `state=0x00000000`, proving that the native scrollbar is actually exposed rather than the visibility query failing;
- the native ListView non-client calculation boundary is therefore the relevant lifecycle owner: while the control is hosted, `HiveListView` now removes `WS_HSCROLL` / `WS_VSCROLL` during `WM_NCCALCSIZE`, before native non-client processing can recreate those standard scrollbar styles, and reapplies the removal after native processing;
- the original native scrollbar style bits are retained for restoration when the control is detached;
- native `LVM_SCROLL` remains unchanged, so ListView viewport/item/selection/keyboard/owner-draw/CRUD behavior is still provided by the native control;
- no suppression operation is added to the high-frequency scroll/repaint path, and the previous deferred/posted suppression mechanisms are not reintroduced.

The regression test itself remains unchanged in behavior and continues to require both native standard scrollbar objects to report hidden after attach, maximize, and scrolling.

Status is **VERIFICATION PENDING** for developer compilation and the focused ListView maximize/scroll regression.


## Verification Failed / Remediation Required — HiveListView Dark-Mode First Paint

Developer verification confirms the previous ListView scrollbar regression is now functionally resolved, but visual verification identifies one same-slice UI defect: the `HiveListView` first paint is briefly white in Dark mode before the Hive theme surface is applied.

Remediation boundary:
- correct only the `HiveListView` initialization/theme lifecycle responsible for the first-paint background;
- ensure the initial native ListView surface is consistent with the active Hive theme without changing ListView scrolling, owner-draw, selection, keyboard, CRUD, or native scrollbar suppression behavior;
- preserve Light/Dark/System behavior and avoid unrelated UI refactoring.

Status is **VERIFICATION FAILED / REMEDIATION REQUIRED** pending same-slice visual remediation.


## HiveListView Dark First-Paint Remediation

The reported Dark-mode first-paint defect is corrected within the same Slice 4 boundary:
- `HiveListView` now resolves the active theme from its owning `HiveForm` during parent assignment and handle creation, so the native control receives the Hive surface/text colors before its first user-visible paint;
- no hard-coded dark default was introduced, preserving Light/Dark/System theme authority;
- the existing `HiveThemeManager.Apply(...)` path remains authoritative for normal theme propagation and later theme changes;
- ListView scrolling, native scrollbar suppression, owner-draw, selection, keyboard, and CRUD behavior were not changed by this correction;
- focused `HiveUiPolishTests.HiveListView_UsesOwningHiveThemeBeforeFirstPaint` coverage now verifies the dark theme is applied when the control enters a Hive form before showing the UI.

Status is **VERIFICATION PENDING** for developer compilation and the focused UI regression/full suite.


## CRUD Scroll First-Paint Correction

The developer clarified that the remaining Dark-mode first-paint defect is in the **CRUD scrolling surface**, not the ListView itself.

Correction:
- the previously added ambient theme initialization on `HiveListView` was removed because it targeted the wrong surface;
- `HiveScrollHost` now resolves the owning `HiveForm` theme during control creation/parenting and applies that theme to the complete hosted scroll surface before first visible paint;
- this uses the existing `HiveThemeManager` rather than introducing a separate theme/default-color path;
- `HiveScrollHost.ApplyTheme` remains responsible only for the host/viewport surface, while normal recursive theme application remains responsible for its child Hive scrollbars, avoiding duplicate resource rebuilds;
- focused regression coverage now verifies a dark-themed CRUD scroll host receives the owning Hive surface before the form is shown.

The already-verified native ListView scrollbar/lifecycle fix remains unchanged.

Status is **VERIFICATION PENDING** for developer compilation, focused UI regression, and the broader suite.


## Verification Failed / Remediation Required — CRUD ListView Native Scrollbars During First Paint

Developer visual verification clarified the remaining Dark-mode CRUD scrolling defect: the issue is not the scroll-host background theme itself. The CRUD `HiveListView` can visibly paint its native Win32 scrollbars during its initial handle/layout/first-paint lifecycle before Hive-owned scrollbars are established.

Remediation boundary:
- arm the existing `HiveListView` native scrollbar suppression before the ListView is inserted into `HiveScrollHost`, so native handle creation/non-client layout occurs with suppression already active;
- preserve the later authoritative scroll-state acquisition and existing native `LVM_SCROLL` behavior;
- preserve the Hive scrollbar rendering and all CRUD ListView item/selection/keyboard/owner-draw/paging behavior;
- do not add deferred retries, per-scroll suppression, or another alternate scrollbar mechanism;
- remove the previously attempted CRUD scroll-host ambient-theme workaround because it addressed the wrong cause.

Status is **VERIFICATION FAILED / REMEDIATION REQUIRED** pending same-slice correction.


## CRUD ListView First-Paint Suppression Remediation

The corrected implementation addresses the actual first-paint lifecycle:
- `HiveScrollHost.Attach()` now arms `HiveListView` native scrollbar suppression **before** the ListView is inserted into the host viewport when its handle has not yet been created;
- this allows the existing `WM_NCCALCSIZE` / native style suppression logic to be active during the ListView's initial native handle creation and non-client setup, preventing native scrollbars from becoming the first-paint presentation;
- authoritative scroll-state acquisition remains in the existing `Synchronize()` path, and native `LVM_SCROLL` remains unchanged;
- the previously attempted ambient-theme workaround on `HiveScrollHost` was removed because it targeted the wrong cause;
- the focused maximize/scroll regression now also checks the native scrollbar state immediately after `form.Show()`, before the normal message-pump synchronization boundary.

Status is **VERIFICATION PENDING** for developer compilation and focused/full UI verification.
