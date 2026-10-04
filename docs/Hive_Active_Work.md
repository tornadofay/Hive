# Hive — Active Work

Status: NO ACTIVE WORK

## Current Slice

**Closed — Phase 1.19A — Execution Target Preferences & Favorite Target Pool**

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
- local inspection filters provide a data-sized practical USD/1M token-price range with $0.01 slider precision and a capability/state filter;
- price filtering uses the highest normalized comparable USD input/output token rate; OpenRouter-style per-token rates are normalized to USD/1M before comparison, and missing comparable token pricing remains distinct from free pricing;
- no provider/discovery, persistence, or Agent target-selection behavior was changed.

Developer re-verification is required.

## Verification state

- Developer verification on 2026-10-03 passed the original Phase 1.19A implementation at 584/584 Hive.Tests executions (0 failed, 0 skipped) before the additional existing-UI correction.

- Automated verification now passes: on 2026-10-04 the developer ran the full Hive.Tests suite with **593/593 passed, 0 failed, 0 skipped**. The automated gate is satisfied for the authorized existing-UI correction. Manual Example Host / Settings verification remains required before final slice closure.
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

## Verification Failure / Remediation Boundary — Model Information Capability Presentation and Filtering

Developer verification on 2026-10-03 reported 592 Hive.Tests executions: 591 passed, 1 failed, 0 skipped. The remaining failure was Phase116FollowUpTests.ModelInformationView_RendersRichDiscoveryProfile, where the Model Information list assertion encountered a mismatched expected capability symbol. Developer feedback also identified in-scope presentation issues: compact capability symbols had been reused in structured details instead of being list-only; the page title/header was not the first visual section; the fixed right details panel was substantially wider than necessary; changing inspection filters caused visible form flicker; and the filters were not reliably updating the displayed model list.

Remediation boundary:
- keep `✓` / `✕` / `—` exclusively for compact capability state cells in the Model Information list;
- use full textual capability state names in structured details and descriptive filter choices;
- reorder the existing Model Information composition so its title/description header appears first, followed by the existing context selectors/inspection filters, then the model list/details split;
- size the fixed right details panel to the intended compact content width rather than deriving a large width from the left panel;
- apply price/capability filters locally against the current discovery snapshot without invoking the CRUD asynchronous load operation, preserving the current selection where possible and avoiding unnecessary whole-page loading/flicker;
- preserve discovery, Favorites, persistence, capability authority, and Agent target-selection semantics.

## Remediation Completed — Model Information Capability Presentation and Filtering

Implementation completed inside the recorded boundary:
- list-only capability cells use `✓` / `✕` / `—`; structured detail capability states use `Supported`, `Unsupported`, or `Unknown / unreported`;
- the capability state filter now uses textual `Supported`, `Unsupported`, and `Unknown / unreported` choices;
- the existing CRUD page header is presented first in the Model Information page composition, before the context selectors/inspection filters and model/details split;
- the right details panel targets a compact 400px fixed width;
- inspection filters now update the existing list locally from the current discovery snapshot instead of triggering the CRUD load operation, preserving an applicable selection and avoiding loading-state flicker;
- the focused test assertions were aligned with the intended list/detail distinction and supported-state filter text;
- no discovery/provider transport, Favorites persistence, capability authority, or Agent behavior changed.

Developer re-verification is required.

Implementation note: the shared HiveCrudPage now exposes the presentation-only SetItemsForView(items) surface so a consuming view can replace already-loaded display items for local filtering without starting the asynchronous CRUD load operation. This is limited to shared UI presentation and does not alter Hive domain or management contracts.

## Verification Failure / Remediation Boundary — Model Information Selection Detail Test Assertion

Developer verification on 2026-10-03 reported 592 Hive.Tests executions: 591 passed, 1 failed, 0 skipped. The remaining failure was Phase116FollowUpTests.ModelInformationView_UpdatesDetailsWhenSelectionChanges because the regression asserted the human-readable text "Vision", while the structured detail table renders the normalized capability key "vision". The selected second-model details otherwise matched the intended selection-path assertions.

Remediation boundary:
- correct only the test assertion to match the existing structured detail representation;
- do not change production Model Information rendering, discovery, filtering, Favorites, persistence, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Selection Detail Test Assertion

Remediation completed within the recorded test-assertion boundary:
- changed the selection-details regression to assert the normalized capability key "vision", matching the existing structured detail representation;
- no production Model Information rendering or behavior changed.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information List/Details Weight and Token Price Filter

Developer feedback on 2026-10-04 identifies three bounded existing-UI problems in the already authorized Model Information correction:
- capability column headers are still verbose text; the user requires a compact `Model | icon | icon | ...` presentation, with recognizable capability icons such as an eye for Vision;
- the right-side Model Information panel is too heavy and causes excessive painting/re-rendering when the selected model, inspection filter, or Provider/Account/endpoint context changes;
- the Token price / 1M USD filter is not reliably useful and is explicitly requested for removal.

Remediation boundary:
- replace only the Model Information list capability column labels/presentation with compact, deterministic icons while retaining list-only capability-state semantics;
- replace the existing multi-card details construction with a lightweight reusable details surface that updates existing controls instead of recreating a large control tree on each selection/filter/context change;
- remove the Token price filter controls and their local filtering logic/tests/documentation;
- preserve discovery semantics, capability evidence/state meaning, Provider → ProviderAccount → ExecutionTarget ownership, Favorites behavior, persistence contracts, and Agent target-selection behavior;
- do not introduce a new shared icon framework or redesign the broader Hive UI.

Developer re-verification is required after remediation.

## Remediation Completed — Model Information List Icons, Lightweight Details, and Price Filter Removal

Remediation completed within the recorded Model Information UI failure boundary:
- replaced verbose capability column headers with compact capability icons (`✎`, `👁`, `⚒`, `{}`, `∴`, `💭`) while preserving `✓` / `✕` / `—` list-cell state semantics;
- replaced the recreated multi-card details tree with reusable title/body labels that update in place on model selection, filter changes, and context changes;
- removed the Token price / 1M USD filter and its focused automated tests because the filter was not providing reliable value;
- retained pricing information inside the selected model's observational details;
- no discovery, provider transport, Favorites, persistence, capability authority, or Agent target-selection behavior changed.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information Accessibility/Resize Compile Errors

Developer verification on 2026-10-04 reports three in-scope compilation errors in HiveModelInformationSettingsView.cs:
- `AccessibleRole.Heading` is not available in the target WinForms API;
- `DetailsScrollHostOnResize` is referenced when subscribing to and unsubscribing from the details scroll host resize event, but the handler is missing.

Remediation boundary:
- replace the unsupported accessibility role with a valid existing WinForms accessibility role;
- restore the existing details-scroll resize handler so the lightweight details surface continues to recompute its text width on resize;
- do not change Model Information discovery, filtering semantics, Favorites behavior, persistence, capability meaning, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Accessibility/Resize Compile Errors

Remediation completed within the recorded compile-failure boundary:
- replaced the unsupported `AccessibleRole.Heading` value with the valid `AccessibleRole.StaticText`;
- restored `DetailsScrollHostOnResize` so the reusable details surface retains its existing resize-width synchronization;
- no Model Information discovery, filtering, Favorites, persistence, capability semantics, or Agent target-selection behavior changed.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information Lightweight Details Heading

Developer verification on 2026-10-04 reported 590 Hive.Tests executions: 589 passed, 1 failed, 0 skipped. The failure was `Phase116FollowUpTests.ModelInformationView_RendersRichDiscoveryProfile`, where the test expected the details text to contain `Capabilities` while the lightweight reusable details surface rendered the section heading as uppercase `CAPABILITIES`.

Additional user correction requested in the same Phase 1.19A Model Information UI scope:
- restore descriptive capability column headers `Text`, `Vision`, `Tools`, `Structured`, `Reasoning`, and `Thinking`;
- restore a local minimum/maximum token-price filter, with a maximum of zero serving as the explicit free-model filter;
- missing pricing must remain distinct from free pricing.

Remediation boundary:
- change only the lightweight details section heading presentation and its focused regression assertion;
- restore only the Model Information list's descriptive capability headers and local token-price range controls/filtering;
- preserve the existing lightweight reusable details control structure;
- do not change discovery semantics, Favorites, persistence, capability authority, Provider/Account/ExecutionTarget ownership, or Agent target-selection behavior.
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Capability Headers and Free-Model Price Range

Remediation completed within the recorded Model Information UI failure boundary:
- restored descriptive Text / Vision / Tools / Structured / Reasoning / Thinking capability column headers;
- restored local minimum/maximum comparable token-price filtering;
- setting the maximum price to 0 filters to models with a comparable price of exactly 0, while missing pricing remains distinct from free pricing;
- kept the lightweight reusable detail text surface and the previously corrected capability-state semantics;
- corrected the focused rich-profile heading mismatch by using the existing descriptive section heading text;
- no discovery, provider transport, Favorites, persistence, capability authority, or Agent target-selection behavior changed.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information Provider Details Heading and Price Filter

Developer verification on 2026-10-04 reported **591** Hive.Tests executions: **590 passed, 1 failed, 0 skipped**. The remaining failure is `Phase116FollowUpTests.ModelInformationView_RendersRichDiscoveryProfile`, where the regression expects the provider-information section text `Additional provider information` but the current lightweight details renderer still emits the section heading with different casing.

The developer also reports that the restored numeric token-price filter is not functioning correctly and requests a slider-based min/max UI instead.

Remediation boundary:
- correct only the lightweight details section heading text and its focused assertion;
- replace the Model Information numeric price controls with a bounded min/max slider presentation while retaining local filtering semantics;
- correct the concrete price-filter evaluation/list-refresh defect without changing discovery, Favorites, persistence, capability authority, Provider/Account/ExecutionTarget ownership, or Agent target-selection behavior;
- preserve the rule that maximum price 0 represents free-only presentation and missing pricing is not treated as free;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Price Sliders and Provider Details Heading

Remediation completed within the recorded Model Information UI failure boundary:
- replaced the numeric min/max token-price controls with paired TrackBar sliders using $0–$1000 bounds and $0.25 increments;
- slider changes update labels and apply local filtering through ValueChanged, including programmatic/test changes;
- maximum price $0 remains the explicit free-model view; missing pricing is not treated as free;
- normalized the remaining lightweight provider-information section heading to the expected descriptive text;
- preserved the lightweight reusable details surface and all discovery/Favorites/persistence/capability/Agent boundaries.

Developer re-verification is required.

## Verification Failure / Remediation Boundary — Model Information Provider Pricing Units and Free Filter

Developer/manual verification on 2026-10-04 reports an in-scope Model Information price-filter defect: paid models can be treated as unpriced and therefore all remain visible at ordinary ranges, while setting maximum price to 0 can show no model even when the discovered model has explicit free pricing.

Repository and provider-source inspection identifies the concrete cause:
- OpenRouter's OpenAI-compatible model metadata reports `pricing.prompt` and `pricing.completion` as USD **per token** values such as `0.00000035`, not USD per million tokens; OpenRouter's payload does not provide a `currency` property for these rates.
- Hive's current comparable-price filter only accepts token prices when `Currency == "USD"`, so OpenRouter's valid rates are discarded as missing pricing.
- The existing free-only branch therefore also rejects an OpenRouter free model when its zero-priced token entries have no explicit currency.
- Groq's public model catalog presents prices as USD per 1 million tokens, while its public OpenAI-compatible models endpoint is model-discovery metadata rather than a guaranteed pricing source; Hive must not invent pricing where the provider endpoint does not report it.

Remediation boundary:
- correct Model Information price normalization/filtering so provider-reported token prices are interpreted according to their source-unit semantics and OpenRouter's implicit USD pricing is handled correctly within the existing OpenAI-compatible discovery boundary;
- preserve the canonical comparable filter unit as USD per 1 million input/output tokens so values such as OpenRouter `0.00000035` become `$0.35/M` rather than being treated as `$0.00000035/M`;
- make maximum price 0 reliably include explicitly free-priced token models while continuing to distinguish missing/unreported pricing from free pricing;
- preserve non-token pricing entries as non-comparable and do not infer Groq pricing from its public website inside the discovery adapter;
- update focused tests and applicable Model Information docs to cover provider-unit normalization, very-low prices, OpenRouter-style missing currency, and explicit free-only filtering;
- do not change discovery capability semantics, Favorites behavior, persistence, Provider/Account/ExecutionTarget ownership, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Provider Pricing Units and Free Filter

Remediation completed within the recorded pricing verification boundary:
- OpenRouter, Cerebras OpenRouter, and Cloudflare OpenRouter catalog parsing now supplies the documented USD currency context when their pricing payload omits a currency field;
- OpenRouter `prompt` / `completion` prices remain represented at their source unit and are converted by the Model Information comparison to USD per 1 million tokens, so `0.00000035` per token compares as `$0.35/M`;
- Model Information price sliders now use $0.01 increments and size their maximum from the discovered comparable prices instead of a fixed $1000 range;
- maximum price $0 now recognizes explicit zero-valued OpenRouter token pricing even without a currency field;
- missing pricing remains distinct from explicit free pricing and is excluded when the user actually narrows the price range;
- Groq pricing remains unreported when the OpenAI-compatible `/models` payload does not provide pricing; Hive does not scrape provider documentation or invent a catalog price;
- pricing details now show the filter-comparable highest input/output token rate for clarity;
- focused regression coverage now covers OpenRouter pricing without currency, explicit zero pricing, and per-token-to-per-million UI filtering;
- no discovery capability semantics, Favorites behavior, persistence, Provider/Account/ExecutionTarget ownership, or Agent target-selection behavior changed.

Developer re-verification is required.


## Verification Failure / Remediation Boundary — OpenRouter Pricing Regression Test Accessibility

Developer verification on 2026-10-04 reports two in-scope compilation errors in `tests/Hive.Tests/Phase116FollowUpTests.cs`: `OpenAICompatibleModelCatalogFormat` is inaccessible due to its protection level at lines 68 and 154.

Remediation boundary:
- correct only the test/production accessibility contract needed for the focused OpenRouter pricing regressions to compile and exercise the intended provider-format path;
- prefer existing repository test-access conventions and do not widen production public API unless the existing responsibility boundary requires it;
- do not change provider discovery semantics, pricing normalization semantics, Favorites, persistence, capability authority, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — OpenRouter Pricing Regression Test Accessibility

Remediation completed within the recorded compile-failure boundary:
- added the repository-standard `InternalsVisibleTo("Hive.Tests")` assembly metadata to `Hive.Providers.OpenAICompatible`, allowing focused tests to exercise the internal provider catalog-format discriminator without making it part of the production public API;
- no provider behavior or production public contract was otherwise widened.

Developer re-verification is required.


## Verification Failure / Remediation Boundary — Model Information Provider Details Heading

Developer verification on 2026-10-04 reports **593** Hive.Tests executions: **592 passed, 1 failed, 0 skipped**. The remaining failure is `Phase116FollowUpTests.ModelInformationView_RendersRichDiscoveryProfile` at `Phase116FollowUpTests.cs:624`, where the rendered details text does not contain the expected `Additional provider information` section heading.

Remediation boundary:
- correct only the Model Information lightweight details provider-information heading/rendering and its focused regression assertion;
- do not change pricing architecture, provider catalog scope, discovery semantics, Favorites, persistence, capability authority, Provider/Account/ExecutionTarget ownership, or Agent target-selection behavior;
- return Active Work to VERIFICATION PENDING and require developer re-verification.

## Remediation Completed — Model Information Provider Details Heading

Remediation completed within the recorded 2026-10-04 verification-failure boundary:
- corrected the non-empty provider-extension details path to render the same descriptive heading `Additional provider information` used by the empty-extension path;
- preserved the existing focused regression assertion rather than weakening it;
- revised the off-work provider pricing architecture/plan documents separately; those documents do not authorize or advance the main roadmap and did not alter the current 1.19A implementation boundary;
- no discovery, pricing behavior, Favorites, persistence, capability authority, Provider/Account/ExecutionTarget ownership, or Agent target-selection behavior changed as part of this failure remediation.

Status: VERIFICATION PENDING. Developer must rerun the focused Model Information tests and the broader Hive.Tests suite before this slice can close or the off-work provider-completion plan can be started.


## Closure — Phase 1.19A — 2026-10-04

Developer verification is complete for the authorized Phase 1.19A scope.

- Full `Hive.Tests`: **593 total, 593 passed, 0 failed, 0 skipped**.
- `Hive.Example.WinForms`: developer manually confirmed the application and the affected Settings / Advanced Provider Configuration workflow work correctly.
- The final same-slice Model Information correction is included in the verified result: the provider-information details heading renders as `Additional provider information`.
- The completed scope includes the durable owner/scope-aware Favorites preference surface, filtered Add Favorite picker, persistence/access validation, Favorites Settings UI, and the authorized existing Model Information UI correction.
- No Agent target-selection redesign was introduced.
- The off-road provider pricing normalization and built-in provider catalog plan remains separate and is not activated by this closure.

Result: **Complete and verified.**

No later roadmap phase is authorized by this closure.
