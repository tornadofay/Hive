# Hive — Active Work

Status: VERIFICATION PENDING

Phase: **1.18A — Embedded Persistence Profile**

Slice: **2 — Embedded Persistence Foundation**

Authorization: Explicit user authorization on 2026-10-07 via 'start slice 2', following successful developer verification of Slice 1 (721/721 tests passed).

Repository checkpoint: `7be96ecb66f1581a0c02be4dacbc771e0c81b963` (`main`).

## Authorized scope

- Add the SQLite provider dependency through `Microsoft.Data.Sqlite` in `Hive.Persistence`.
- Establish one application-owned Embedded SQLite storage abstraction behind the existing backend-neutral persistence boundary.
- Implement bounded local path validation/normalization and parent-directory creation without moving storage details into Core or Management.
- Implement Embedded connection/lifecycle handling with bounded SQLite busy/locking behavior, cancellation-aware opening/operations, deterministic disposal, and no credential-bearing connection-string leakage across public boundaries.
- Implement Embedded schema/version infrastructure for the foundation-only bootstrap schema, with a Hive-owned schema-version singleton and deterministic migration journal.
- Implement ordered, deterministic Embedded foundation migrations with transactional schema-version advancement and fail-closed detection of future/inconsistent schema state.
- Implement initialization and non-destructive status/reopen behavior for clean local stores.
- Add focused Slice 2 automated coverage for initialization, schema/version behavior, reopen/recovery, invalid/corrupt storage, path failures, locking/busy behavior, rollback, cancellation, and disposal boundaries.
- Preserve all existing SQL Server behavior and keep full Embedded parity, data migration, Settings UI, and end-user first-run activation deferred to later slices.

## Explicit exclusions

- No full Embedded resource-store parity from Slice 3.
- No SQL Server ↔ Embedded data migration from Slice 4.
- No Persistence Settings UI or first-run default from Slice 5.
- No Example Host migration/setup workflow; Slice 2 exposes only the reusable persistence foundation needed by later slices.
- No vector storage/search.
- No changes to the logical resource model, Management authorization model, host business state, or SQL Server implementation unless required to preserve shared backend-neutral contracts.

## Verification gate

Required developer verification after implementation:

`EmbeddedPersistenceFoundationTests` focused run, followed by the broader `Hive.Tests` suite.

The Slice 2 implementation is complete within the authorized boundary. No verification result is claimed until the developer reports the runs.

The implementation includes the SQLite provider dependency, application-owned file lifecycle, bounded path handling, per-connection durability/foreign-key configuration, WAL initialization, deterministic foundation migrations and journal/version metadata, fail-closed metadata validation, stable SQLite/storage errors, cancellation boundaries, reopen recovery, bounded busy/locking behavior, non-destructive foreign-storage rejection, rollback, and disposal coverage.

Required developer verification: `EmbeddedPersistenceFoundationTests` focused run, followed by the broader `Hive.Tests` suite. The gate must cover clean initialization, schema/version behavior, reopen/recovery, corruption/incompatibility handling, path/permission failures, locking/busy behavior, rollback, cancellation, and disposal.


## Latest verification failure

Developer reran the required broader `Hive.Tests` suite after the SQL Server migration-resource isolation fix: **736 tests run, 725 passed, 11 failed, 0 skipped**, in approximately 1.3 minutes.

The remaining failures are confined to `EmbeddedPersistenceFoundationTests`. The reported failures are:
- `CancellationBeforeInitialization_IsHonoredWithoutCreatingStorage`: `Assert.False` expected no storage but observed storage was created.
- Ten other Embedded foundation tests fail during cleanup because `hive.db` remains locked by another process when `DeleteDatabaseFiles` attempts to delete it. The affected tests include initialization/idempotency, inconsistent metadata, rollback, busy/locking, corruption, reopen/recovery, parent-directory initialization, future-schema handling, disposal, and non-Hive-table rejection.

This is a Slice 2 verification failure. Remediation is required before Slice 2 can close and is limited to the Embedded foundation storage lifecycle/cancellation boundary recorded by these failures. No later-slice work is authorized.

## Completed same-slice remediation

The prior SQL Server migration-resource isolation remediation remains in place at repository checkpoint `c4017103ca4c9d0871e2bb8231b263423d1fc99c4`.

The remaining Embedded failures were traced to two Slice 2 issues:
1. `EmbeddedPersistenceDatabase` configured Microsoft.Data.Sqlite connection pooling. On Windows, disposed pooled connections could keep the SQLite database file open after the test-owned connection was disposed, causing later cleanup attempts to fail with a file-in-use IOException. Pooling is now disabled for the application-owned Embedded foundation so connection disposal deterministically releases the file handle.
2. The cancellation regression test used `CreatePath`, whose helper intentionally creates the parent directory before the test begins. The implementation correctly honored cancellation before storage access, but the test then asserted that the already-existing parent directory did not exist. The test now uses an uncreated path helper so the assertion checks the intended precondition.

These changes remain strictly within the Slice 2 connection/lifecycle and cancellation verification boundary.

Repository checkpoint: `3526ef7fb1179a67fe2526b105de47ac3f61ebb9`.

Developer verification is **VERIFICATION PENDING**. Rerun the focused `EmbeddedPersistenceFoundationTests` suite, followed by the full `Hive.Tests` suite.
