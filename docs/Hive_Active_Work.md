# Hive — Active Work

Status: IN PROGRESS

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
- the Provider Settings Favorites page lets the user choose active targets by Provider and Provider Account;
- Agent target editing can narrow targets by Provider and Account and uses the favorite pool when one exists.

Verification state:
- NOT VERIFIED — implementation pending developer build/test/manual verification.

No future roadmap slice is authorized by this work item.