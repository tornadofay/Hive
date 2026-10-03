# Hive — Active Work

Status: VERIFICATION PENDING

## Current Slice

**Phase 1.19A — Execution Target Preferences & Favorite Target Pool**

Authorized by explicit user instruction on 2026-10-03.

Objective:
- add a durable owner/scope-aware favorite ExecutionTarget list as a candidate-pool filter;
- add a second Favorites tab to normal Settings → Providers while preserving the existing Providers page as the first tab;
- make favorite management practical through Provider / Provider Account filtering;
- make existing ExecutionTarget selection easier through Provider / Provider Account filtering and use favorites as the candidate list when favorites exist, while preserving the currently configured target during edit where necessary;
- preserve the existing Provider → ProviderAccount → ExecutionTarget resource model and the authoritative capability-aware selector.

Implementation scope:
- Hive.Core: favorite-target candidate filtering contract that leaves selection policy unchanged;
- Hive.Management: favorite-target load/save operations with access/scope validation;
- Hive.Persistence: durable favorite-target persistence and migration;
- Hive.Host.WinForms: Provider Settings two-tab UI and filtered ExecutionTarget selection UI;
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
- with no favorites configured, existing ExecutionTarget candidate behavior remains unchanged;
- when favorites exist, target-selection consumers that opt into the favorite filter receive only those favorite target identities before normal capability selection;
- a retired favorite remains stored so the same durable target identity can become available again if the target is reactivated;
- the Provider Settings Favorites page lets the user choose active targets by Provider and Provider Account, initially focusing the first available Provider and Account rather than loading every target;
- All Providers / All Accounts remain explicit filters for intentionally viewing the broader target pool;
- Agent target editing can narrow targets by Provider and Account and uses the favorite pool when one exists.

## Verification Failure / Remediation Boundary

Developer reported the following when using the Favorites page:
- Refresh produced System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
- Refresh also produced System.ObjectDisposedException: The CancellationTokenSource has been disposed.
- On first opening the Favorites tab, the target list was populated with execution targets from all providers/accounts instead of presenting the intended focused favorite-target workflow.

Remediation completed within the Phase 1.19A Favorites settings view load/filter lifecycle and focused UI test scope:
- refresh/load cancellation ownership no longer disposes a cancellation source while an older async load still owns it;
- stale reloads check cancellation before committing shared collections/results;
- provider/account collection enumeration uses snapshots across async boundaries;
- the initial Favorites page now selects the first available Provider and first available Account, while retaining All Providers / All Accounts as explicit choices;
- focused UI coverage now asserts the initial Provider/Account focus with multiple providers.

## Verification state

- VERIFICATION PENDING — remediation is complete but developer verification is required.
- Rerun ExecutionTargetFavorite* focused tests and the Provider Settings / Agent selector UI tests.
- Exercise the Example Host path: Overview / Getting Started / Example Configuration — Hive.Example.WinForms.
- Specifically verify opening the Favorites tab, switching Provider and Account filters, checking/unchecking favorites, Save Favorites, and repeated Refresh without exceptions.
- Broader Hive.Tests verification remains required for normal slice closure.

No future roadmap slice is authorized by this work item.
