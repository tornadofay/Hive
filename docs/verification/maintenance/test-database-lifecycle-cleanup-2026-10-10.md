# Hive — Maintenance Verification: Test Database Lifecycle & Cleanup — 2026-10-10

## Scope

Bounded test-infrastructure maintenance in `tests/Hive.Tests`. This slice corrected SQL Server integration-test database ownership, teardown, and interrupted-run recovery. It did not change production persistence behavior or public Hive APIs.

## Corrective implementation reviewed

- Added `SqlTestDatabaseLifecycle` to generate unique physical names in the reserved `Hive_TestOwned_` namespace and register an ownership marker as a database-level extended property.
- Disabled SQL connection pooling for owned test databases and held a per-database SQL application-lock lease while a test owns its database.
- Normal disposal checks the active lease and matching ownership marker before dropping a database; cleanup errors remain observable.
- Stale recovery is restricted to strictly formatted generated names older than 24 hours, a matching marker, and an obtainable exclusive lease lock, followed by a marker recheck. Legacy databases are not deleted automatically merely because they use the old `Hive_Test_` prefix.
- Migrated SQL persistence, migration, provider, event, management, Agent work-state, and parity test fixtures to disposable ownership. Removed the data-migration tests' local `DropSqlDatabaseAsync` helper that swallowed `SqlException`.
- Added lifecycle regression tests for strict naming/marker/age rules, lease isolation, refusal when ownership changes, and stale recovery.
- Embedded parity fixtures dispose the Embedded database and delete temporary directories, collecting and surfacing cleanup failures. There is no separate embedded server-database inventory analogous to SQL Server's; this defect did not require a second server-style cleanup mechanism for Embedded.

The repository's main branch was reviewed at implementation/documentation checkpoint `939e7068d7d455a83858a1bd5bd5462894892019` before this closure documentation commit.

## Automated verification reported by the developer

The developer reported the following complete test run on 2026-10-10:

```
========== Starting test run ==========
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v3.1.5+1b188a7b0a (64-bit .NET 10.0.1)
[xUnit.net 00:00:00.54]   Starting:    Hive.Tests
[xUnit.net 00:03:09.32]   Finished:    Hive.Tests
========== Test run finished: 793 Tests (793 Passed, 0 Failed, 0 Skipped) run in 3.2 min ==========
```

Result: **793/793 passed, 0 failed, 0 skipped**, in approximately 3.2 minutes. The assistant did not execute this test run. The report did not include a separate build-success result or an explicit zero-warning Error List, so neither is claimed.

## SQL Server inventory cleanup reported by the developer

The developer reported removing approximately 500 accumulated legacy SQL Server test databases and completing SQL Server database cleanup. The assistant did not connect to the developer's SQL Server instance and did not independently query the inventory.

The implementation intentionally does not auto-delete those legacy databases. Future test-created databases use the reserved `Hive_TestOwned_` form and ownership marker, and ordinary lease disposal is responsible for prompt cleanup; the guarded stale reaper is a recovery path for interrupted runs.

## Parallelization status

`tests/Hive.Tests/AssemblyMarker.cs` still disables xUnit test parallelization at the assembly level. This setting was not changed by this slice. Repository history shows it was toggled on and off on 2026-10-05, but those commit messages do not identify the particular failing test. The exact historic failure cannot be named from the repository evidence reviewed here. Re-enabling parallel execution should be considered only in a separate investigation that reproduces the failure and identifies/isolate shared state; it is not a prerequisite for using filtered test profiles.

## Closure outcome

**VERIFIED AND CLOSED**, based on the developer-reported full-suite result and SQL Server cleanup confirmation. Embedded test temporary-directory cleanup is covered by the reviewed fixtures; no separate embedded server-database cleanup change was identified as necessary for this defect.

No new implementation slice is authorized by this closure.

Example to run: Not applicable; this is test infrastructure.

Tests to run: `PersistenceTestDatabaseLifecycleTests`, `HivePersistenceDataMigrationTests`, `EmbeddedPersistenceParityTests`, and `Phase118DurableBaseAgentWorkStateTests` are included in the reported complete `Hive.Tests` run; the full suite passed 793/793.
