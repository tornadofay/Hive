# Hive — Active Work

Status: **VERIFICATION PENDING**

## Active corrective slice

**Maintenance — Test Database Lifecycle & Cleanup**

Started: 2026-10-10

### Checkpoint

- Repository branch: `main`
- Starting commit: `05950472e4725506de6d568e6fd1bd35f882b2be`
- Implementation checkpoint: `66f6d074163713fc5ff932d9b993653f9295bc4e`
- Source implementation and regression coverage are in place; the assistant has not run builds or tests. Developer verification is required before closure.
- Reported defect: SQL integration-test databases with the `Hive_Test_` prefix accumulated because the shared helper created/reset databases without deleting them; some migration-test cleanup suppressed SQL exceptions.
- Existing legacy databases are deliberately excluded from automatic recovery. The new reaper only considers the strict `Hive_TestOwned_` physical naming format, a matching database-level ownership marker, a 24-hour minimum age, and a successful exclusive lease-lock acquisition. It will not bulk-delete databases merely because they begin with `Hive_Test_`.

### Authorized scope

Correct the existing `Hive.Tests` SQL Server test-database lifecycle so database creation, ownership, test teardown, parallel test isolation, and interrupted-run recovery have one explicit test-only owner.

### Implementation review

- Added `SqlTestDatabaseLifecycle`: each lease gets a generated physical database name in the reserved `Hive_TestOwned_` namespace, a database-level extended-property ownership marker (not a user table), disabled SQL connection pooling, and a shared SQL application lock held for that individual database lease.
- Normal cleanup verifies both the ownership marker and the still-held lease before dropping a database, then releases the lease. Cleanup failures propagate; stale recovery reports candidate failures without swallowing SQL exceptions.
- Stale recovery only considers correctly formatted generated names older than 24 hours, verifies the marker against the name, acquires the matching exclusive per-database lease lock, and rechecks the marker before dropping. This protects parallel and long-running tests without a process-wide lock lingering after successful test runs.
- Added `PersistenceTestDatabase.CreateMigratedAsync` so database setup and migration failures dispose the lease, and migrated the returning fixture helpers to it.
- Converted SQL integration tests to `using` / `await using` ownership, including Phase 1.18 fixtures, provider/event integration fixtures, parity backends, and all SQL source/destination/round-trip databases in the full-data migration tests.
- Removed the data-migration tests' local `DropSqlDatabaseAsync` helper, which swallowed `SqlException`; cleanup now uses the same ownership-aware lease. Embedded temporary-directory cleanup reports errors rather than silently ignoring them.
- Added `PersistenceTestDatabaseLifecycleTests` covering strict ownership-name and age rules, marker mismatch refusal, unique lease creation and successful drop, active lease protection, and end-to-end stale recovery of a marked abandoned database.
- Updated the persistence test strategy in `docs/architecture/foundations.md`.
- Audited the remaining direct `Hive_Test_` references in tests. The plain-text input-selection occurrence names a temporary directory, and the already-cancelled migration test throws before migration work starts; neither creates an unleased SQL database.

### Boundaries and exclusions

- This is test-infrastructure maintenance in `tests/Hive.Tests`; no production persistence behavior or public Hive API changes.
- Do not delete existing database-server contents as an incidental implementation step. Existing legacy test databases require a separately verified, narrowly scoped cleanup.
- Do not disable test parallelism to hide shared-state races.
- Do not add external dependencies.
- Do not claim builds or tests ran unless the developer reports the results.

### Required verification

- Run focused tests: `PersistenceTestDatabaseLifecycleTests`, `HivePersistenceDataMigrationTests`, `EmbeddedPersistenceParityTests`, and `Phase118DurableBaseAgentWorkStateTests`.
- Then run the full `Hive.Tests` suite with Visual Studio Treat Warnings as Errors enabled.
- Review any compile/analyzer/test failure as a verification gate before remediation. Confirm the resulting SQL Server database inventory contains no newly created test-owned databases after the passing suite.
- Keep the slice open until actual developer verification results arrive. On closure, archive evidence, update Current Status and the verification index, and clear this file back to the exact no-active-work state.

Example to run: Not applicable; this is test infrastructure.

Tests to run: focused SQL persistence/migration lifecycle tests listed above; then the full `Hive.Tests` suite.
