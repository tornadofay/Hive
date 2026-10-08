# Hive — Active Work

Status: **IMPLEMENTATION IN PROGRESS — VERIFICATION PENDING**

## Phase 1.18A — Embedded Persistence Profile
### Slice 4 — Full-Data Migration

Authorized by explicit user instruction on 2026-10-08: **start Phase 1.18A Slice 4**.

### Objective
Implement the Management-owned logical full-data migration operation in both directions:

- SQL Server → Embedded
- Embedded → SQL Server

### Authorized scope
- source quiescence/preflight;
- clean/new destination validation;
- deterministic dependency-ordered transfer of **All Hive Data** represented by the current persistence contracts;
- failure-safe/incomplete migration handling;
- source immutability;
- destination verification;
- migration identity/schema evidence;
- Secret Store re-protection in the destination;
- cancellation, concurrency, lifecycle, determinism, authorization, validation, and typed-error behavior required by the migration boundary;
- focused Hive.Tests coverage for successful migration, invalid/non-empty/incompatible/incomplete destinations, unquiescent sources, rollback/failure safety, source immutability, verification, and secret re-protection.

### Explicit exclusions
- Persistence Settings / Data Migration WinForms UI;
- host recomposition or Embedded first-run activation;
- automatic activation or configuration switching to the destination;
- partial resource-family migration, merge/synchronization, destructive overwrite;
- vector retrieval/indexing/search (Phase 1.19);
- unrelated persistence redesign or cleanup.

### Verification gate
Both directions must be developer-verified against complete representative Hive datasets, including stable identities, relationships, versions/lifecycle state, event/snapshot/outbox consistency, Base-Agent work state, and protected Secret Store records. Invalid/non-empty/incomplete destinations and unquiescent sources must fail safely.

Developer verification reported additional compile failures on 2026-10-08 in `HivePersistenceDataMigrator.cs`: a table/column-definition argument mismatch, a stale `MigrationIncompatibleSourceError` reference, and a local `reader` scope collision. Same-slice remediation corrected all three reported errors: `AddParameters` now receives `table.Columns`, the source compatibility path uses the existing `MigrationIncompatibleSource` helper, and the later secret-fingerprint reader is scoped as `secretReader`. No out-of-scope failure is recorded.

The 2026-10-08 developer run reached 749 tests with 742 passed and 7 failed. Six failures use `HivePersistenceConfiguration.LocalDevelopment(...)`, which points at `(localdb)\\MSSQLLocalDB` while the test database helper uses the configured SQL Server test instance. The seventh failure is the representative event fixture supplying `ResourceVersion.Initial` (version 0) as a snapshot for the first event, whose required snapshot version is 1. Same-slice remediation corrected these recorded failures; no out-of-scope failure is recorded.

Developer verification on 2026-10-08 after the documented Slice 4 remediation reached a compiler error in `tests/Hive.Tests/HivePersistenceDataMigrationTests.cs` line 1060: `CS1503` because `string.LastIndexOf(char, StringComparison)` is not a valid overload. The failure is inside the same Slice 4 migration-test configuration helper and is an in-scope remediation boundary; no out-of-scope failure is recorded.

Same-slice remediation corrected the reported `CS1503` by using the valid `string.LastIndexOf(string, StringComparison)` overload for comma-separated SQL server/port parsing. No unrelated production or test boundary was changed. Developer re-verification remains required.

Developer verification on 2026-10-08 reported 749 tests with 746 passed and 3 failed. The failures are confined to the Slice 4 migration-test boundary: `Migration_RejectsPartialSqlDestinationMetadata` expected `hive.persistence.data-migration.schema-incomplete` but received `hive.persistence.data-migration.backend-direction-invalid`; `Migration_RejectsFutureEmbeddedDestinationSchema` expected `hive.persistence.embedded.future-schema` but received the generic destination-schema-incompatible migration error; and `FullDataMigration_RoundTripsAllCurrentDurableStateAndReprotectsSecrets` failed while appending the representative event because the WorkItem stream was already at version 1 while the fixture expected version 0. Same-slice remediation is authorized only for these recorded failures.

### Example / verification handoff

Example to run: **Persistence / Data Migration / Full-Data Migration / SQL Server ↔ Embedded** — Hive.Example.WinForms
Tests to run: **HivePersistenceDataMigrationTests**; broader **Hive.Tests** suite for Slice 4 closure


Developer verification on 2026-10-08 reported 749 tests with 748 passed and 1 failed: `FullDataMigration_RoundTripsAllCurrentDurableStateAndReprotectsSecrets` failed at its Embedded event-stream assertion because the migrated WorkItem stream contains 2 legitimate events (`work-item.created` version 1 plus `migration.representative` version 2), while the test still expected a single event. Migration destination fingerprint verification itself succeeded. This remained an in-scope Slice 4 migration-test assertion boundary and authorized same-slice remediation.

Same-slice remediation updated the migration test to assert both persisted WorkItem events and their versions/types, and aligned the stored `EventSnapshotVersion` seed value with the representative snapshot at stream version 2. No production migration behavior was changed. Developer re-verification remains required.


Developer verification on 2026-10-08 reported 749 tests with 748 passed and 1 failed: `FullDataMigration_RoundTripsAllCurrentDurableStateAndReprotectsSecrets` reached the second migration (Embedded → SQL Server), but the migration failed during destination fingerprint verification with `hive.persistence.data-migration.verification-failed`: "The migration destination does not exactly match the source logical dataset." The first SQL Server → Embedded migration fingerprint verification succeeded in the same run. The failure is confined to the Slice 4 cross-backend fingerprint/round-trip verification boundary and is therefore an in-scope same-slice remediation.

Same-slice remediation aligned the SQL-side Secret Store fingerprint query with the existing cross-backend canonical ordering rule by routing its OrderByColumns through `BuildOrderByExpression`. This fixes the second-direction (Embedded → SQL Server) verification mismatch without weakening fingerprint verification or changing migration data semantics. No out-of-scope production behavior was changed. Developer re-verification remains required.