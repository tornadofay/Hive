# Hive — Active Work

Status: **IMPLEMENTATION IN PROGRESS**

## Active corrective slice

**Maintenance — Test Database Lifecycle & Cleanup**

Started: 2026-10-10

### Checkpoint

- Repository branch: `main`
- Starting commit: `05950472e4725506de6d568e6fd1bd35f882b2be`
- No source changes have been made in this slice yet.
- Reported defect: SQL integration-test databases with the `Hive_Test_` prefix accumulate because the shared test database helper creates/resets databases without deleting them; some migration-test cleanup also suppresses SQL exceptions.
- The existing legacy databases are out of scope for automatic deletion unless the new ownership/recovery scheme can positively identify them as created by this test infrastructure. No database will be deleted manually as part of this implementation.

### Authorized scope

Correct the existing `Hive.Tests` SQL Server test-database lifecycle so database creation, ownership, test teardown, parallel test isolation, and interrupted-run recovery have one explicit test-only owner.

- Give each test-owned SQL database a reserved, collision-resistant physical name and explicit ownership evidence.
- Make successful test teardown dispose/drop the databases created for that test and all temporary source/destination databases created by migration tests.
- Keep SQL Server integration tests isolated under parallel execution; do not replace isolated databases with a single shared mutable database.
- Surface cleanup failures with database identity and diagnostic details, without empty catches and without leaking or deleting unrelated databases.
- Add conservative recovery for stale databases only when the reserved test naming format, age threshold, and ownership evidence agree. Do not automatically delete arbitrary legacy `Hive_Test_*` databases.
- Update the relevant test-harness architecture guidance and add regression coverage for ownership, cleanup, stale-recovery safety, and isolation.

### Boundaries and exclusions

- This is test-infrastructure maintenance in `tests/Hive.Tests`; no production persistence behavior or public Hive API changes.
- Do not delete existing database-server contents as an incidental implementation step. Existing legacy test databases require a separately verified, narrowly scoped cleanup.
- Do not disable test parallelism to hide shared-state races.
- Do not add external dependencies.
- Do not claim builds or tests ran unless the developer reports the results.

### Required review / verification

- Audit every SQL Server database creation path in `Hive.Tests`, including `PersistenceTestDatabase`, phase/parity helpers, and full-data migration source/destination databases.
- Verify per-test cleanup after success and failure, ownership checks before deletion, uniqueness under parallel runs, marker/name/age checks for stale recovery, and failure diagnostics.
- Developer verification target: focused SQL persistence/migration tests, then full `Hive.Tests` with Visual Studio Treat Warnings as Errors enabled.
- Keep status open until actual developer verification results are received. Once closed, archive evidence, update Current Status and the verification index, and clear this file back to the exact no-active-work state.

Example to run: Not applicable; this is test infrastructure.

Tests to run: focused SQL persistence and data-migration tests; then the full `Hive.Tests` suite.
