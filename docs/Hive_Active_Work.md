# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.12 — HiveSettingsForm & Configuration Pages**

Phase 0 — Foundations and Phase 1.1 through Phase 1.11 are complete and verified.

Phase 1.12 is the authorized implementation slice.

## Objective

Establish the first-class Hive Settings surface as a thin WinForms shell over Hive.Management, with Provider and Persistence configuration pages.

## Scope

- Provider configuration and connection-test flow through the Management/configuration boundary.
- Persistence configuration through the Management boundary.
- SQL Server / LocalDB settings with credentials referenced through Secret Store.
- Non-destructive connectivity testing; database initialization/migration remains separate.
- Persisted settings reload after application restart without plaintext password exposure.
- Settings UI owns navigation/composition only; no direct SQL, provider transport, or migration implementation in WinForms.

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

Phase 1.12 implementation is present on `main`; verification is pending.

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

- current Management contracts and facade;
- existing Secret Store and DPAPI persistence boundary;
- HiveDatabaseOptions/migration/schema bootstrap;
- Provider/ProviderAccount/ExecutionTarget resource contracts and persistence;
- Host UI conventions and `docs/ui/examples.md`;
- Example Host discovery/output pattern;
- existing configuration/provider/persistence tests.

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
