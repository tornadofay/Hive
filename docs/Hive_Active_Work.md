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

Developer reported the required broader `Hive.Tests` verification failed after the Slice 2 nullable/catch-filter remediation: **736 tests run, 636 passed, 100 failed, 0 skipped**, in approximately 51.4 seconds. The reported failures span Provider Persistence, WorkItem Management, and Hive Persistence integration tests, with the common failure surface `Hive database migration failed. DbUp did not provide a migration error message.` The focused Slice 2 developer verification result was not separately supplied.

## Completed same-slice remediation

The failure was traced to a Slice 2 resource-isolation regression: adding Embedded SQLite migration `.sql` files to the `Hive.Persistence` assembly also made them visible to the existing SQL Server DbUp call `WithScriptsEmbeddedInAssembly(_migrationAssembly)`, which loads all embedded `.sql` resources by default. The SQL Server migrator was therefore attempting to execute the SQLite foundation migration against SQL Server. The remediation changed the SQL Server DbUp registration to accept only resources under `.Migrations.Scripts.`, leaving the Embedded SQLite migration resources exclusively to the Embedded migration catalog. This remains within the Slice 2 failure boundary and preserves the explicit SQL Server behavior requirement.

Repository checkpoint: `c4017103ca4c9d0871e2bb8231b263423d1fc99c4`.

Developer verification is **VERIFICATION PENDING**. Rerun the focused `EmbeddedPersistenceFoundationTests` suite, followed by the full `Hive.Tests` suite. The broad suite must demonstrate that the SQL Server migration regression is eliminated and that the Slice 2 foundation tests still pass.
