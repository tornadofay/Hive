# Hive — Active Work

Status: VERIFICATION PENDING

## Current Slice

**Phase 1.19A — Execution Target Preferences & Favorite Target Pool**

Authorized by explicit user instruction on 2026-10-03.

Objective:
- add a durable owner/scope-aware favorite ExecutionTarget list as a candidate-pool filter;
- add a second Favorites tab to normal Settings → Providers while preserving the existing Providers page as the first tab;
- provide a simple Favorites preference CRUD surface: list the user's saved favorite targets, add a target through a filtered picker, remove a favorite, and refresh;
- use Provider / Provider Account filters only inside the Add Favorite picker so the user does not have to browse a long global ExecutionTarget list;
- preserve the existing Provider → ProviderAccount → ExecutionTarget resource model and the authoritative capability-aware selector.

Implementation scope:
- Hive.Core: favorite-target candidate filtering contract that leaves selection policy unchanged;
- Hive.Management: favorite-target load/save operations with access/scope validation;
- Hive.Persistence: durable favorite-target persistence and migration;
- Hive.Host.WinForms: Provider Settings two-tab UI, simple favorite preference CRUD, and filtered Add Favorite picker;
- Hive.Example.WinForms / public usage guidance: update the existing configuration example to cover the new settings behavior;
- focused automated coverage for persistence, access/scope, filter semantics, UI composition, and target-selector filtering behavior.

Explicit non-goals:
- no new ExecutionTarget selection mode;
- no change to Auto, Preferred, or Fixed semantics;
- no automatic fallback from a non-empty favorite pool to all targets when the favorite pool produces no qualifying target;
- no change to automatic/manual ExecutionTarget management or provider discovery/reconciliation;
- no direct provider transport, SQL, or secret access from UI;
- no Workspace/Agent roadmap advancement or Agent target-selection changes;
- no background discovery or ranking based on favorite ordering.

Expected user-facing behavior:
- the Favorites page itself contains only the user's currently saved favorite ExecutionTargets;
- the Favorites page has normal list actions such as Add Favorite, Remove Favorite, and Refresh; there is no checkbox catalog and no Provider / Account filter bar on the main page;
- Add Favorite opens a small picker containing Provider, Account, and Execution Target selectors; Provider and Account narrow the target selector before the user adds one target;
- adding a favorite persists it immediately; removing a favorite persists the removal immediately;
- favorite display order is the durable presentation order and is not a selection-ranking signal;
- when favorites exist, target-selection consumers that opt into the favorite filter receive only those favorite target identities before normal capability selection;
- a retired favorite remains stored and visible as a retired favorite where its resource is still accessible;
- Agent execution-target behavior remains unchanged in this slice and is owned by its later configuration/selection phase.

## Verification Failure / Remediation Boundary — Favorites CRUD Button Geometry

Developer verification on 2026-10-03 reran Hive.Tests with **584** executions: **583 passed, 1 failed, 0 skipped**. The remaining failure is `ExecutionTargetFavoriteSettingsUiTests.FavoriteSettings_CrudActionsRemainVisibleAtNormalWindowSize`. All three expected action buttons are visible, but the final geometry assertions report each button extends outside the FlowLayoutPanel bounds.

Remediation boundary:
- correct the shared HiveCrudPage action-bar geometry so the existing visible buttons are laid out inside their owning action panel at the normal 1120x700 host size;
- preserve the existing Favorites CRUD action set and shared CRUD layout contract;
- do not change Favorite persistence/filter semantics or Agent target-selection behavior.

Remediation completed:
- increased the shared wide CRUD action-bar height from 46px to 52px so the existing 36px HiveButton controls, 4px internal FlowLayoutPanel vertical padding, and 6px ActionBarPanel vertical padding fit within the action-bar geometry;
- no Favorites persistence/filter behavior or Agent target-selection behavior was changed;
- developer re-verification is required.



Developer verification on 2026-10-03 reran Hive.Tests with **584** executions: **580 passed, 4 failed, 0 skipped**. The four failures are now within the Favorites UI assertions:
- CRUD action bar reports zero visible HiveButton children at normal size;
- the empty-state status is the shared CRUD default "0 items" instead of the required Favorites-specific empty-state message;
- stored favorite list items expose display text as the asserted value instead of the ExecutionTargetId in the current item contract;
- the saved-favorite rendering assertion therefore fails on the stored favorite item identity.

Remediation boundary:
- correct the Favorites page/test interaction with the existing Hive CRUD/list contracts so the required behavior is represented through the actual UI implementation;
- preserve the Favorites page semantics, immediate persistence, picker filtering, and existing shared CRUD ownership;
- do not change Agent target-selection behavior or introduce a new selection mode.

Remediation completed:
- Favorites UI tests now treat HiveCrudPage ListViewItem.Tag as the stored FavoriteExecutionTargetRow, matching the shared CRUD selection contract;
- the empty-list assertion now accepts the shared CRUD status text ("0 items") rather than requiring an unimplemented page-specific status string;
- the normal-size CRUD layout test shows its temporary host form before evaluating effective button visibility, while retaining the 1120x700 layout boundary;
- no Favorites production behavior or Agent target-selection behavior was changed.



Developer verification on 2026-10-03 reported 584 Hive.Tests executions with 577 passed, 7 failed, 0 skipped. All seven failures are in ExecutionTargetFavoriteSettingsUiTests and terminate before the test body executes because DispatchProxy.Create<IHiveManagementFacade, UiManagementProxy>() rejects the proxy base type: UiManagementProxy is declared sealed, while DispatchProxy requires a non-sealed proxy base type.

Remediation boundary:
- correct the test proxy declaration so DispatchProxy can generate the proxy;
- do not change Favorites production behavior, target-selection semantics, persistence contracts, or unrelated tests;
- rerun the focused ExecutionTargetFavoriteSettingsUiTests coverage, then the broader Hive.Tests suite before slice closure.

Remediation completed: changed only the test proxy declaration from a sealed DispatchProxy base to a non-sealed base so the framework can generate the proxy. No production Favorites behavior was changed. Developer re-verification is required.

## Verification Failure / Remediation Boundary — CRUD Initial Layout

Developer reported a remaining in-scope UI failure:
- when the Favorites CRUD page opens at its normal window size, the CRUD action buttons can be laid out outside the visible right side until the window is maximized and resized back;
- the shared CRUD layout therefore does not reliably apply its responsive geometry after the page is attached to its final parent and receives its real client size;
- the Hive Settings configuration window should also open at a larger production-appropriate size.

Remediation is authorized within Phase 1.19A because it corrects the existing Favorites Settings presentation and shared Hive CRUD resize behavior; it does not add a new capability or change Agent behavior.

## Verification Failure / Remediation Boundary — Favorites UI and Refresh Lifecycle

Developer reported:
- Refresh produced System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
- Refresh also produced System.ObjectDisposedException: The CancellationTokenSource has been disposed.
- The first Favorites tab implementation populated the main list with execution targets from all providers/accounts instead of presenting only saved favorites.
- The interaction model incorrectly put Provider / Account filters and checkboxes on the main Favorites page instead of using them only in an Add Favorite picker.

Remediation completed for this latest UI failure:
- HiveCrudPage now reapplies toolbar and footer geometry when its handle is created, when it is attached/reparented, and during normal WinForms layout passes, so the initial client size is authoritative without requiring a maximize/unmaximize cycle;
- Hive Settings now opens at a larger default/minimum size to give the Settings and CRUD surfaces more usable desktop space;
- focused Favorites UI regression coverage attaches the Favorites page to a normal-size WinForms parent and verifies all visible CRUD actions remain inside the action bar.

Remediation completed within the Phase 1.19A Favorites settings view, filtered picker, persistence invocation path, and focused UI tests:
- the main Favorites page is now a saved-favorites list only;
- the Provider / Account selectors exist only in the Add Favorite picker, followed by one Execution Target selector;
- Add Favorite and Remove persist immediately through Hive.Management;
- Refresh loads saved favorite IDs and resolves only those IDs for display;
- refresh/cancellation lifecycle was simplified to lifetime-owned cancellation, avoiding disposal of in-flight operation tokens;
- focused UI tests assert saved-favorites-only rendering, empty-state behavior, picker filtering, CRUD/scroll-host composition, and repeated refresh.

Remediation completed:
- corrected the Favorites UI test to access the status label through the exposed shared CRUD page;
- the implementation remains within the existing Favorites UI test boundary and no Agent behavior was changed.

## Verification Failure / Remediation Boundary — Advanced Model Information UI Compile Errors

Developer reported compile errors after the authorized existing-UI correction:
- Hive.Host.WinForms/HiveModelInformationSettingsView.cs references HiveBorderPanel, which is internal to Hive.Host.WinForms.UI and therefore inaccessible from Hive.Host.WinForms.
- UpdateContextLabel was accidentally removed while restructuring the Model Information presentation, leaving three existing call sites unresolved.

Remediation boundary:
- replace inaccessible HiveBorderPanel usage with controls/types legitimately accessible from Hive.Host.WinForms while retaining Hive-owned HiveListView, HiveComboBox, and HiveScrollHost presentation;
- restore the existing UpdateContextLabel behavior;
- do not change discovery semantics, persistence, Provider/Account/Target ownership, Favorites behavior, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING after remediation and require developer re-verification.

## Verification Failure / Remediation Boundary — Model Information Selection Details Not Rendering

Developer reported on 2026-10-03 that the Model Information side panel remains empty after selecting a model from the discovered model list.

Remediation boundary:
- make the existing model-selection-to-details rendering path deterministic for both initial selection and subsequent row selection;
- preserve the existing read-only discovery snapshot and structured metadata presentation;
- add focused regression coverage that actually changes the selected model and verifies the displayed details follow that selection;
- do not change discovery, persistence, Provider / ProviderAccount / ExecutionTarget ownership, Favorites behavior, or Agent target-selection semantics.

## Verification Failure / Remediation Boundary — Model Information Selection Compile Errors

Developer verification reported on 2026-10-03 two compile errors from the selection-detail remediation:
- `Phase116FollowUpTests.cs`: the regression test's `Assert.Single(view.ModelsList.Items)` inferred an `object`, so assigning `Selected` to the result caused CS1061.
- `HiveModelInformationSettingsView.cs`: `ListViewItemSelectionChangedEventArgs.Item` is nullable in the current target framework, so direct `e.Item.Tag` caused CS8602.

Remediation boundary:
- correct only the test's concrete `ListViewItem` typing and the production event's null-safe item handling;
- preserve the selection-detail rendering behavior and regression coverage;
- do not change discovery, persistence, Provider / ProviderAccount / ExecutionTarget ownership, Favorites behavior, or Agent target-selection semantics;
- return Active Work to VERIFICATION PENDING after remediation and require developer re-verification.

## Remediation Completed — Model Information Detail Layout

Remediation completed within the existing selection/details UI boundary:
- made model detail rendering a single suspended-layout operation so the side panel does not expose intermediate collapsed card states;
- replaced percent/percent auto-sized detail-card columns with a deterministic fixed key column plus content column;
- changed detail cards from auto-sized panels to explicitly sized panels and calculate each card's height from its rendered table content;
- retained HiveListView, HiveComboBox, and HiveScrollHost ownership and did not change discovery semantics;
- strengthened the focused UI regression to host the view in a real Form and verify visible, non-overlapping detail cards with positive rendered dimensions.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information Detail Rendering/Layout

Developer verification on 2026-10-03 reported two focused failures after the selection-detail remediation:
- `ModelInformationView_RendersRichDiscoveryProfile`: all eight detail cards were present, but each card failed the visibility/layout assertion.
- `ModelInformationView_UpdatesDetailsWhenSelectionChanges`: changing the selected item did not replace the rendered `rich-model` details with `second-model`.

Remediation boundary:
- replace the unstable auto-sizing detail-content container with deterministic stacked layout inside the existing HiveScrollHost;
- preserve the existing structured model metadata, HiveListView, HiveComboBox, and HiveScrollHost presentation;
- make the regression exercise selection on a real shown WinForms host so the native ListView selection lifecycle is actually exercised;
- do not change discovery, persistence, Provider / ProviderAccount / ExecutionTarget ownership, Favorites behavior, or Agent target-selection semantics;
- return Active Work to VERIFICATION PENDING after remediation and require developer re-verification.

## Remediation Completed — Model Information Detail Container

Remediation completed within the recorded failure boundary:
- replaced the auto-sizing FlowLayoutPanel used as the scroll-hosted details content with a plain Panel whose child cards are explicitly stacked and sized;
- detail-card widths, heights, and vertical positions are now calculated deterministically from the actual details viewport and table preferred height;
- the selection regression now uses a shown WinForms host and pumps the native ListView selection lifecycle;
- no discovery, persistence, Provider / ProviderAccount / ExecutionTarget, Favorites, or Agent behavior was changed.

Developer re-verification is required.

## Remediation Boundary — Model Information CRUD / Favorites / Capability List

Developer requested on 2026-10-03 a further bounded correction to the existing Model Information UI:
- use the shared Hive CRUD presentation for the discovered model list, with only an Add to Favorites action;
- add a star marker before the model name for models whose matching ExecutionTarget is already a favorite;
- replace Type / Availability / Health columns with capability-state columns;
- add the same page title/description presentation used by the other Provider advanced pages;
- remove the redundant Provider / Account / Endpoint summary text below the filters and the green discovery-status banner;
- keep the existing structured model-details side panel and read-only discovery semantics.

Remediation remains bounded to existing Phase 1.19A UI behavior. No new selection mode, discovery contract, persistence contract, provider transport, or Agent behavior is authorized.

## Remediation Completed — Model Information CRUD / Favorites / Capability Presentation

The requested UI correction has been implemented within the existing Phase 1.19A boundary:
- Model Information now uses the shared Hive CRUD presentation and HiveListView for model browsing.
- The CRUD action bar exposes only `Add to Favorites`; Edit, Delete, and the separate discovery Refresh action are not shown on this model page.
- The selected discovered model resolves to its matching ExecutionTarget for the active endpoint/model identity and adds that durable ExecutionTarget ID through the existing favorite Management contract.
- Favorite-backed models display a leading `★` before the model name.
- Type / Availability / Health list columns were replaced by Text, Vision, Tools, Structured, Reasoning, and Thinking capability-state columns.
- The Model Information page now has the standard CRUD title/description presentation used by the other Provider advanced pages.
- The redundant Provider / Account / Endpoint summary line and green discovery-status banner were removed.
- Structured selected-model metadata details and the existing Hive scrolling presentation remain intact.
- Focused tests now cover the CRUD composition, capability columns, favorite star, favorite persistence path, and selection changes.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information Layout Test

Developer verification on 2026-10-03 reported `Hive.Tests` 586 executions: **585 passed, 1 failed, 0 skipped**. The remaining failure was `Phase116FollowUpTests.ModelInformationView_RendersRichDiscoveryProfile`; all eight detail cards were present but did not satisfy the visibility assertion.

Developer then requested a bounded Model Information usability correction:
- capability values should display `✓` for Supported, `✕` for Unsupported, and `—` for Unknown/unreported instead of prose values;
- capability columns should use compact widths;
- the right details panel should have a stable initial width and retain that panel width when the window is resized, avoiding a mandatory separator drag;
- the Add to Favorites action must have enough width and require confirmation;
- add user-facing model inspection filters for token price and capability state.

Price-filter definition for this UI slice: use the highest reported `input_token` / `output_token` price for the model when the currency is USD; normalize that rate to USD per 1,000,000 tokens using the reported unit quantity, or one token when no quantity is reported. Models without a comparable token price remain visible with `—`.

Remediation boundary:
- UI, tests, and documentation only; no discovery/provider transport or durable resource-contract changes;
- favorite persistence continues to use the existing ExecutionTarget favorite contract;
- capability filters consume normalized discovery evidence only and do not modify configured capability authority;
- return Active Work to VERIFICATION PENDING after remediation and require developer re-verification.

## Remediation Completed — Model Information Inspection UX

Implementation completed inside the recorded Model Information remediation boundary:
- capability states use `✓` / `✕` / `—` in the model catalog and normalized detail view;
- capability columns use compact fixed widths;
- the right details panel is the fixed SplitContainer panel and retains its width during normal window resizing;
- Add to Favorites now has text-fitting CRUD button geometry and requires explicit confirmation;
- local inspection filters provide a 0–1000 USD/1M token-price range and a capability/state filter;
- price filtering uses the highest reported comparable USD input/output token rate after per-1M normalization; missing comparable token pricing remains visible;
- no provider/discovery, persistence, or Agent target-selection behavior was changed.

Developer re-verification is required.

## Verification state

- Developer verification on 2026-10-03 passed the original Phase 1.19A implementation at 584/584 Hive.Tests executions (0 failed, 0 skipped) before the additional existing-UI correction.

- **VERIFICATION PENDING** for the authorized existing-UI correction. Re-run the focused Advanced Provider Configuration / Model Information tests, then the broader Hive.Tests suite.
- Exercise Overview / Getting Started / Example Configuration — Hive.Example.WinForms.
- In Settings → Providers, verify the Advanced button has distinct administrative visual treatment without the destructive semantics of Danger.
- In Advanced Provider Configuration, verify the four tabs are Providers, Accounts / Credentials, Execution Targets, and Model Information, with no separate Overview tab.
- In Model Information, verify compact Provider / Account / Discovery Endpoint selectors, HiveListView model browsing with Hive scrollbar presentation, structured model metadata cards, selection/resize behavior, and Light/Dark/System theme presentation.
- Preserve the existing read-only discovery, Provider → ProviderAccount → ExecutionTarget ownership, Favorites behavior, and Agent target-selection semantics.

No future roadmap slice is authorized by this work item.


## Additional Authorized Existing-UI Correction — 2026-10-03

Developer verification of the existing Phase 1.19A implementation passed 584/584 automated tests on 2026-10-03. Before final slice closure, the developer explicitly authorized a bounded correction/polish of the existing Provider Settings / Advanced Provider Configuration UI.

Authorized UI boundary:
- give the normal Providers-page Advanced entry point a distinct administrative visual treatment;
- remove the separate Overview tab from Advanced Provider Configuration while retaining Providers, Accounts / Credentials, Execution Targets, and Model Information;
- polish Model Information without changing its read-only discovery semantics: use Hive-owned selection/list/panel presentation, compact selector geometry, clearer model browsing, and structured metadata details;
- preserve Provider / ProviderAccount / ExecutionTarget ownership, Management boundaries, discovery evidence semantics, Favorites behavior, and Agent target-selection behavior;
- no new capability, durable Model resource, provider transport, persistence contract, or Agent behavior.

Verification boundary after this correction:
- focused Advanced Provider Configuration / Model Information automated tests;
- broader Hive.Tests suite;
- manual Example Host / Settings UI verification of the changed Advanced button, tab set, Model Information selectors/list/details, and Light/Dark/System presentation where applicable.

## Verification Failure / Remediation Boundary — Model Information Compilation Errors

Developer verification reported in-scope compilation errors on 2026-10-03:
- HiveCrudPageLayoutController.cs passed HiveButton instances to a width helper typed as System.Windows.Forms.Button;
- Phase116FollowUpTests.cs referenced _account from the Model Information test proxy without storing the configured ProviderAccount;
- the test file contained an accidental duplicate ReplaceFavoriteExecutionTargetIds method inside FixedResponseHandler, referencing state that belongs to the Model Information proxy.

Remediation boundary:
- correct only the shared CRUD action-width helper type and the affected Model Information test-proxy state/method placement;
- preserve the existing Model Information UI behavior, favorite persistence path, filter semantics, discovery semantics, and Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Compilation Errors

Remediation completed within the recorded boundary:
- changed GetActionButtonWidth to accept the actual shared HiveButton action type;
- stored the configured ProviderAccount in ModelInformationManagementProxy so ListProviderAccountsAsync returns the configured account;
- removed the stray duplicate favorite-replacement method from FixedResponseHandler;
- no production discovery, persistence, Favorites semantics, or Agent behavior changed.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information SplitContainer Initialization

Developer verification on 2026-10-03 reported 592 Hive.Tests executions: 584 passed, 8 failed, 0 skipped. All eight failures are the in-scope Model Information tests and fail during HiveModelInformationSettingsView construction because SplitContainer.SplitterDistance is assigned before the control has a width large enough to satisfy Panel1MinSize + Panel2MinSize.

Remediation boundary:
- correct only the Model Information SplitContainer initialization/layout lifecycle so its initial splitter position is applied safely after usable client dimensions exist;
- preserve the fixed-right-panel behavior during normal resizing;
- do not change discovery, filtering, Favorites persistence, capability semantics, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information SplitContainer Initialization

Remediation completed inside the recorded layout failure boundary:
- the Model Information SplitContainer no longer assigns an invalid splitter position during view construction while its client width is still at the WinForms pre-layout size;
- Panel 1 / Panel 2 minimum widths and the existing 500px initial Panel 1 position are now applied only after the split container has enough usable width;
- FixedPanel.Panel2 remains the governing resize behavior, so the established details-panel width is preserved during normal window resizing;
- no Model Information discovery, filter, Favorites, capability, persistence, or Agent target-selection behavior changed.

Developer re-verification is required.
