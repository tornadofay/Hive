# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.12 — Settings, Configuration, and Real Host Consumption**

### Current sub-stage

**1.12-A — Host Configuration and Runtime Composition**

Detailed workload and ordering: `docs/plan/Phase1.12_Settings_Host_Integration.md`

Phase 0 — Foundations and Phase 1.1 through Phase 1.11 are complete and verified.

Phase 1.12 is the authorized implementation slice.

## Objective

Complete the first-class Hive Settings surface so saved configuration actually drives the host application's real persistence/resources/runtime consumption, following the HAgent configuration-consumption pattern while preserving Hive's stronger Management/resource boundaries.

## Scope

The complete Phase 1.12 program is subdivided into bounded sub-stages described in `docs/plan/Phase1.12_Settings_Host_Integration.md`:

- host configuration/runtime composition;
- persistence bootstrap credential boundary;
- Provider/ProviderAccount/ExecutionTarget and AgentDefinition Settings management;
- migration of Settings UI onto `Hive.Host.WinForms.UI` reusable navigation/list/editor controls;
- Settings-driven reload/recomposition;
- real Example Host consumption of configured state;
- focused verification and documentation closure.

Current sub-stage 1.12-A is limited to the host configuration/composition boundary and its required supporting contracts. Do not implement later 1.12 sub-stages in the same run unless a dependency is required to complete 1.12-A.

## Verification gate

1. Management logic remains outside the WinForms shell.
2. Persistence configuration can be saved, reloaded, and validated through the public Management contract.
3. Connection-test success/failure is reported without schema side effects.
4. Database/schema status is distinct from connection success.
5. Credentials are not persisted in plaintext or exposed in diagnostics.
6. Developer manually verifies Provider and Persistence UI flows.
7. Focused automated coverage exists for configuration validation/persistence and security-sensitive behavior.
8. Broader `Hive.Tests` execution.

## Constraints

- Do not implement Phase 1.13 or later.
- Preserve existing Hive.Management and Hive.Persistence boundaries.
- Do not duplicate provider transport, secret storage, SQL connection/migration, or bootstrap logic in the Settings UI.
- Use the existing Example Host pattern for the public example required by the slice.

## Implementation checkpoint

The original Phase 1.12 Settings implementation is present on `main`; verification remains pending. The Settings-to-host consumption gap was identified against the HAgent reference implementation and is now the active 1.12 program. The implementation must proceed through the ordered sub-stages in the plan.

Implemented in the active slice:

- typed SQL Server/LocalDB persistence configuration contracts;
- Management save/load boundary backed by an atomic JSON settings file containing only non-secret fields plus Secret Store identity;
- non-destructive SQL Server persistence connection test reporting database and Hive schema state separately;
- ProviderAccount credential Secret Store reference support;
- Management-mediated provider connection-test boundary and OpenAI-compatible concrete tester;
- first-class public WinForms Settings shell with Provider and Persistence pages;
- Provider setup for Provider, ProviderAccount, ExecutionTarget, credential references, and provider connection testing;
- Persistence settings for server/port/database/authentication/security/initialization policy/timeout;
- public Example Host scenario with copyable, redacted persistence configuration/test output;
- focused configuration, persistence-option, provider-credential-reference, and provider connection-test coverage.

Before coding, inspect:

- `docs/plan/Phase1.12_Settings_Host_Integration.md`;
- current Management contracts and facade;
- existing Secret Store and DPAPI persistence boundary;
- HiveDatabaseOptions/migration/schema bootstrap;
- Provider/ProviderAccount/ExecutionTarget resource contracts and persistence;
- AgentDefinition and first-real-execution composition;
- Host UI conventions and `docs/ui/examples.md`;
- `Hive.Host.WinForms.UI` controls: `HiveNavigationTree`, `HiveListPageLayout`, `HiveListView`, `HiveCrudPage<TItem>`, and `HiveEditorLayout`;
- Example Host discovery/service-composition/output pattern;
- existing configuration/provider/persistence/execution tests.

## Verification handoff

Example to run: **Settings / Configuration / Hive Settings / Provider & Persistence — Hive.Example.WinForms**

Tests to run:
- `tests/Hive.Tests/HiveConfigurationTests.cs`
- `tests/Hive.Tests/HivePersistenceOptionsTests.cs`
- `tests/Hive.Tests/ProviderPersistenceIntegrationTests.cs`
- `tests/Hive.Tests/OpenAICompatibleProviderAdapterTests.cs`
- broader `Hive.Tests` execution is required by the 1.12 completion gate.

Manual Settings checks should cover:

- Persistence Load/Save/reload.
- SQL Server authentication selection and credential redaction.
- non-destructive persistence connection test with database/schema state.
- Provider → ProviderAccount → ExecutionTarget setup.
- provider credential storage through Secret Store.
- provider connection test.
- absence of SQL/transport access from WinForms Settings.
- Example Host output contains no credential material.

## Historical verification

Phase 1.11 completion is recorded in `docs/verification/phase-1/1.11.md`.
