# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.4 — Persistence bootstrap**

Phase 0.3 is complete and verified by the developer. Do not begin 0.5 or any later slice until 0.4 is complete and its verification has actually been performed.

## Objective

Establish the Hive-owned SQL Server persistence boundary required by later durable state, with LocalDB support for development, DbUp migrations, schema-version compatibility checks, and the initial database indexing strategy.

The slice is limited to:
- Hive-owned SQL Server database boundary;
- LocalDB development connection strategy;
- DbUp migration execution with ordered versioned scripts;
- durable schema-version tracking;
- migration compatibility checks for unsupported future schema versions;
- required initial indexes for the Phase 0 foundation tables;
- deterministic migration error handling without partial successful migration claims.

## Implementation progress

The 0.4 implementation is committed to Hive.Persistence:

- SQL Server persistence options with automatic database creation enabled by default;
- LocalDB development configuration using the same persistence boundary;
- SQL Server database creation support for development when configured;
- DbUp SQL Server 7.2.0 migration runner with transaction-per-script execution;
- Microsoft.Data.SqlClient 7.1.0 connectivity;
- Hive-owned singleton schema-version metadata;
- DbUp migration journal isolated under the Hive-owned database;
- future-schema rejection before normal migration execution;
- structured migration success/failure results without exposing connection credentials;
- bootstrap migration creating the schema-version table and its required primary/unique indexes;
- focused persistence option tests and SQL Server/LocalDB integration tests;
- copyable public API example in docs/examples/Phase04_Persistence.md.

No 0.4 verification pass is recorded yet because the SQL integration suite has not been run in the developer environment.

## 0.3 completion record

Phase 0.3 was completed after the developer pulled the correction, rebuilt the full solution, launched the application successfully, and ran the complete test suite.

- Build result: **PASS — developer reports full-solution rebuild succeeds.**
- Runtime result: **PASS — developer reports the solution runs successfully.**
- Test result: **PASS — 36 tests, 36 passed, 0 failed, 0 skipped, 1.5 seconds.**
- Verification result: **PASS for the 0.3 completion gate based on the developer-run test suite.**
- Ready commit before starting 0.4: `7c6db0f85da8e96a93e771a4f2c52d1d79af559d`
- Next slice: **0.4 — Persistence bootstrap**

## Dependency direction

```
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → public platform contracts + Host.WinForms + Host.WinForms.UI
Tests → projects under test

Coordination may depend on Core + Agents + MAF contracts where required.
No core/platform project may depend on Example.WinForms.
```

Additional constraints:

- WinForms-specific types remain outside Hive.Core.
- Provider transport remains outside Hive.Core.
- The ReaLTaiizor package is **not introduced in 0.1**; it is added and verified in 0.6, and only Hive.Host.WinForms.UI may reference it.
- Hive.Example.WinForms must not reference xUnit runner internals.
- Do not create compatibility/legacy projects or duplicate architecture paths.

## Out of scope for 0.4

- provider implementation;
- Agent/Hive behavior;
- execution orchestration;
- WinForms UI/UX;
- V1 image/document pipeline;
- CognitiveAgent/CognitiveHive;
- event log, snapshots, and transactional outbox beyond any schema-version foundation needed by persistence bootstrap;
- production deployment automation.

## 0.4 Verification

The implementation requires verification of:

1. a clean development database can be initialized successfully from an empty database;
2. rerunning migrations is idempotent and produces no duplicate schema/version state;
3. a deliberately failing migration does not advance the recorded schema version;
4. an unsupported future schema version is detected and rejected before normal migration/execution proceeds;
5. required initial indexes exist after a clean migration;
6. migration history/schema-version state is deterministic and inspectable;
7. database connection configuration supports both SQL Server and LocalDB development without provider-specific logic leaking into Hive.Core.

Verification is currently pending. No 0.4 pass claim is recorded yet.

## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice:
## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice:
