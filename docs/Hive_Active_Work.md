# Hive — Active Work

Status: **NO ACTIVE WORK**

## Last closed corrective slice

**Maintenance — Test Database Lifecycle & Cleanup** is complete and verified on 2026-10-10.

Closure evidence: [Maintenance verification record](verification/maintenance/test-database-lifecycle-cleanup-2026-10-10.md).

The developer reported:
- The full `Hive.Tests` suite passed **793/793** (0 failed, 0 skipped) in approximately 3.2 minutes.
- Approximately 500 accumulated legacy SQL Server test databases were removed, and SQL Server database cleanup was completed.
- The assistant did not run the tests/build or query the developer's SQL Server instance. No separate build-success or zero-warning report was supplied.

The corrective work gives SQL Server integration-test databases unique generated ownership, explicit disposable leases, marker-checked normal cleanup, and guarded stale recovery. It removes the previous migration-test cleanup path that swallowed `SqlException`. The existing embedded parity fixtures also dispose Embedded databases and remove their temporary directories while surfacing cleanup failures.

The global xUnit parallelization setting remains disabled in `tests/Hive.Tests/AssemblyMarker.cs`. This slice did not change it. Repository history records parallelization toggled off/on on 2026-10-05, but the commit messages do not identify the specific test that failed. Do not infer a known culprit or re-enable parallelism without a separate, evidence-backed isolation review.

The detailed scope, implementation review, developer-reported verification, and limits of what was independently verified are preserved in the [verification archive](verification/maintenance/test-database-lifecycle-cleanup-2026-10-10.md).

## Authorization boundary

There is no active implementation slice. Do not infer authorization for unrelated maintenance, test-profile implementation, or roadmap advancement from this closure. Establish a new, explicitly bounded slice in this file before beginning additional implementation.