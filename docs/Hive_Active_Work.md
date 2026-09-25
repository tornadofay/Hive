# Hive — Active Work

## Current slice

**Phase 1.14 — Dual Business-App Integration Contract**

Authorization: explicit user request — **Hive: Start Phase 1.14**.

Mode: verification remediation required; **re-verification pending**.

Implementation checkpoint before this remediation: `89b3a994cde0fbfdd3d12f89a5592b1a53660276`.

### Objective

Establish the Hive-owned neutral V1 host-integration contract family and reusable bounded WinForms integration infrastructure so a host application can integrate through a small explicit contract surface without exposing private host controls, data/business framework, SQL, credentials, arbitrary reflection, or unrestricted invocation to Hive.

### Authorized scope

- Core-defined host integration registration/context contracts;
- semantic control, field, data-surface, row-identity, lookup, bounded UI interaction, and API/UI business-operation capability contracts;
- reusable bounded WinForms adaptation for standard native controls and explicit host-provided/custom semantic adapters;
- explicit parent/child data-surface relationships supplied by the host;
- V1 stable primary-key ID row identity with row position remaining positional only;
- generated and computed field semantics;
- bounded dependent lookup request/result behavior;
- separation of UI interaction capabilities from business-operation semantics;
- API-only, UI-only, and API+UI capability composition under one logical operation correlation identity, without executing the later consequential business-write pipeline;
- Management-owned authorization/orchestration over Core-defined host ports;
- provenance, cancellation, lifecycle/disposal, stale-state, and supported concurrency evidence;
- deterministic reference-host/fixture implementation and focused `Hive.Tests` coverage;
- matching `Hive.Example.WinForms` scenario using only public contracts and deterministic host-owned fixtures;
- required UI/example guidance updates needed to keep implemented behavior from being described as merely planned.

### Explicit exclusions

- Phase 1.15, 1.16, 1.17, or any later roadmap slice;
- consequential business-app write implementation, durable business-operation receipts, or first-class Review lifecycle;
- image/spreadsheet input routing or structured extraction/validation;
- universal UI automation;
- direct host-database/SQL access;
- arbitrary reflection or method invocation;
- private host classes, private control-library dependencies, credentials, or raw host object exposure through Core contracts;
- reopening or replacing the Phase 1.13 read-only host-context boundary;
- unrelated Management, Persistence, Provider, Agent, or UI refactoring/dependency upgrades.

### Verification remediation state

**VERIFICATION FAILED / REMEDIATION REQUIRED.** The post-remediation developer run reported one remaining in-scope defect in `HiveWinFormsHostIntegrationTests.Capture_AdaptsStandardWinFormsControlsAndBoundDataSurface`: the default WinForms data-surface descriptor exposed zero fields when the bound `DataGridView` had not yet materialized generated columns, causing `grid.Fields[0]` to fail with `ArgumentOutOfRangeException`. Remediation is limited to robust field metadata discovery from the bound WinForms data source while preserving column metadata when available.

Previous in-scope nullable compiler diagnostics were also remediated within this same slice. Historical verification and remediation details are archived under `docs/verification/phase-1/`.

The first developer verification attempt exposed an in-scope Phase 1.14 defect and the subsequent nullable compiler diagnostics were remediated within the same authorized slice. Historical verification and remediation details are archived under `docs/verification/phase-1/`.

### Verification gate

**VERIFICATION FAILED / REMEDIATION REQUIRED — re-verification required after remediation.**

Required developer verification:

Example to run: **Host / WinForms Integration / Dual Business-App Integration Contract** — `Hive.Example.WinForms`

Tests to run: **`HiveHostIntegrationContractTests.cs` and `HiveWinFormsHostIntegrationTests.cs`; then the full `Hive.Tests` suite**

Manual verification must confirm the deterministic reference-host scenario demonstrates semantic controls/fields, stable hidden primary-key identity, generated/computed fields, parent/child data surfaces and combined operation semantics, bounded dependent lookup, UI capability versus Hive authorization, and API-only/UI-only/API+UI composition without exposing raw controls or SQL.

After remediation, do not close this slice or activate a later phase until the developer supplies the required verification results.

Last updated: 2026-09-25
