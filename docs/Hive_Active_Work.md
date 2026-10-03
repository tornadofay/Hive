# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

## Current Slice

**Phase 1.19A — Execution Target Preferences & Favorite Target Pool**

Authorized by explicit user instruction on 2026-10-03.

Objective:
- add a durable owner/scope-aware favorite ExecutionTarget list as a candidate-pool filter;
- add a second Favorites tab to normal Settings → Providers while preserving the existing Providers page as the first tab;
- provide a simple Favorites preference CRUD surface: list the user's saved favorite targets, add a target through a filtered picker, remove a favorite, and refresh;
- use Provider / Provider Account filters only inside the Add Favorite picker so the user does not have to browse a long global ExecutionTarget list;
- make existing ExecutionTarget selection easier through Provider / Provider Account filtering and use favorites as the candidate list when favorites exist, while preserving the currently configured target during edit where necessary;
- preserve the existing Provider → ProviderAccount → ExecutionTarget resource model and the authoritative capability-aware selector.

Implementation scope:
- Hive.Core: favorite-target candidate filtering contract that leaves selection policy unchanged;
- Hive.Management: favorite-target load/save operations with access/scope validation;
- Hive.Persistence: durable favorite-target persistence and migration;
- Hive.Host.WinForms: Provider Settings two-tab UI, simple favorite preference CRUD, filtered Add Favorite picker, and filtered ExecutionTarget selection UI;
- Hive.Example.WinForms / public usage guidance: update the existing configuration example to cover the new settings behavior;
- focused automated coverage for persistence, access/scope, filter semantics, UI composition, and target-selector filtering behavior.

Explicit non-goals:
- no new ExecutionTarget selection mode;
- no change to Auto, Preferred, or Fixed semantics;
- no automatic fallback from a non-empty favorite pool to all targets when the favorite pool produces no qualifying target;
- no change to automatic/manual ExecutionTarget management or provider discovery/reconciliation;
- no direct provider transport, SQL, or secret access from UI;
- no Workspace/Agent roadmap advancement beyond the selection UI support needed by this slice;
- no background discovery or ranking based on favorite ordering.

Expected user-facing behavior:
- the Favorites page itself contains only the user's currently saved favorite ExecutionTargets;
- the Favorites page has normal list actions such as Add Favorite, Remove Favorite, and Refresh; there is no checkbox catalog and no Provider / Account filter bar on the main page;
- Add Favorite opens a small picker containing Provider, Account, and Execution Target selectors; Provider and Account narrow the target selector before the user adds one target;
- adding a favorite persists it immediately; removing a favorite persists the removal immediately;
- favorite display order is the durable presentation order and is not a selection-ranking signal;
- when favorites exist, target-selection consumers that opt into the favorite filter receive only those favorite target identities before normal capability selection;
- a retired favorite remains stored and visible as a retired favorite where its resource is still accessible;
- Agent target editing can narrow targets by Provider and Account and uses the favorite pool when one exists.

## Verification Failure / Remediation Boundary

Developer reported the following when using the Favorites page:
- Refresh produced System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
- Refresh also produced System.ObjectDisposedException: The CancellationTokenSource has been disposed.
- On first opening the Favorites tab, the target list was populated with execution targets from all providers/accounts instead of presenting the intended focused favorite-target workflow.
- The implemented screen also misunderstood the requested UX: it exposed a target catalog with Provider / Account filters and checkboxes on the main Favorites page instead of providing a simple saved-favorites list with a filtered Add flow.

Remediation is limited to the Phase 1.19A Favorites settings view, its Add Favorite picker, persistence invocation path, and focused UI tests. No unrelated roadmap or selection-policy changes are authorized by this failure.

## Verification state

- VERIFICATION FAILED / REMEDIATION REQUIRED — the previous remediation was insufficient because the main interaction model was still wrong.
- Implement the corrected simple Favorites CRUD surface and filtered Add Favorite picker.
- Then return this document to VERIFICATION PENDING with exact rerun targets.

No future roadmap slice is authorized by this work item.
