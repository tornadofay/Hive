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

Developer reran the broader `Hive.Tests` suite after the previous Embedded lifecycle and cancellation remediation: **736 tests run, 732 passed, 4 failed, 0 skipped**, in approximately 1.5 minutes.

The remaining failures were:
- `FailedMigration_RollsBackScriptAndSchemaVersion`: cleanup attempted to delete `hive.db` while a test verification connection was still alive.
- `LockedStorage_ProducesBoundedBusyFailure`: cleanup attempted to delete `hive.db` while the intentionally locking test connection was still alive.
- `InitializeAsync_RejectsPreexistingNonHiveTables`: cleanup attempted to delete `hive.db` while a verification connection was still alive.
- `InspectAsync_ReturnsCorruptionAsStablePersistenceError`: the public corruption error message contained the provider name `SQLite`, violating the test's provider-detail redaction boundary.

This is a Slice 2 verification failure. Remediation is required before Slice 2 can close and is limited to the Embedded foundation connection-lifetime and public-error-redaction boundaries exercised by these failures. No later-slice work is authorized.

## Completed same-slice remediation

The three file-lock failures were identified as test-owned lifetime leaks, not a retained production connection: the affected tests used `await using var` for extra SQLite connections, so disposal occurred at method exit, after the `finally` cleanup had already attempted to delete the database. Those connections are now wrapped in explicit scopes so they are disposed before cleanup. The intentionally locked connection in the busy test is likewise released before `DeleteDatabaseFiles` runs. The Inconsistent Metadata test verification connection was also explicitly scoped to keep the same lifecycle invariant.

The corruption error was corrected so its stable public message no longer exposes the SQLite provider name; it now reports only that the storage is corrupt or not a valid database file.

These changes remain strictly within Slice 2 verification/remediation boundaries.

Repository checkpoint: `51261dbbbf9ced666fbbe50134bfd06ce64ced99` plus the public-error wording fix at `fea880c4ebb3f50a65992107670086dc9c331cd2`.

Developer verification is **VERIFICATION PENDING**. Rerun the focused `EmbeddedPersistenceFoundationTests` suite, followed by the full `Hive.Tests` suite.
