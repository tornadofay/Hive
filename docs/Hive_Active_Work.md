# Hive — Active Work

Status: **VERIFICATION FAILED / REMEDIATION REQUIRED**

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

The 2026-10-08 developer run reached 749 tests with 742 passed and 7 failed. All seven failures are within the Slice 4 migration test boundary: five use a SQL configuration helper that defaults to `(localdb)\\MSSQLLocalDB` instead of the test instance, one representative event fixture supplies an invalid zero snapshot version, and the remaining failures are downstream manifestations of those two setup issues. Same-slice remediation is authorized only for these recorded failures.

### Example / verification handoff

Example to run: **Persistence / Data Migration / Full-Data Migration / SQL Server ↔ Embedded** — Hive.Example.WinForms
Tests to run: **HivePersistenceDataMigrationTests**; broader **Hive.Tests** suite for Slice 4 closure
