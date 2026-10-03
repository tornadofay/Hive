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

## Verification Failure / Remediation Boundary

Developer reported a remaining in-scope UI failure:
- when the Favorites CRUD page opens at its normal window size, the CRUD action buttons can be laid out outside the visible right side until the window is maximized and resized back;
- the shared CRUD layout therefore does not reliably apply its responsive geometry after the page is attached to its final parent and receives its real client size;
- the Hive Settings configuration window should also open at a larger production-appropriate size.

Remediation is authorized within Phase 1.19A because it corrects the existing Favorites Settings presentation and shared Hive CRUD resize behavior; it does not add a new capability or change Agent behavior.

## Verification Failure / Remediation Boundary

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

## Verification state

- VERIFICATION PENDING — corrected implementation is complete; developer verification is required.
- Rerun the focused ExecutionTargetFavorite UI/management/persistence tests and Favorite Settings / Add Favorite picker UI tests.
- Exercise Overview / Getting Started / Example Configuration — Hive.Example.WinForms.
- In Settings → Providers → Favorite Execution Targets, verify the list contains only saved favorites; Add Favorite opens the Provider → Account → Target picker; Add and Remove persist immediately; and repeated Refresh produces no exceptions.
- Verify changing Provider and Account in the Add Favorite picker narrows the Execution Target choices; do not modify Agent target-selection behavior in this slice.
- Broader Hive.Tests verification remains required for normal slice closure.

No future roadmap slice is authorized by this work item.
