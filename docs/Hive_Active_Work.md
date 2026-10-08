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

Developer verification reported additional compile failures on 2026-10-08 in `HivePersistenceDataMigrator.cs`: a table/column-definition argument mismatch, a stale `MigrationIncompatibleSourceError` reference, and a local `reader` scope collision. These are recorded as the current same-slice remediation boundary. No out-of-scope failure is recorded.

Required verification is now **PENDING developer results**. Re-run the affected solution/build target, then `HivePersistenceDataMigrationTests`, followed by the full `Hive.Tests` suite. No execution is claimed by the agent.

### Example / verification handoff

Example to run: **Persistence / Data Migration / Full-Data Migration / SQL Server ↔ Embedded** — Hive.Example.WinForms
Tests to run: **HivePersistenceDataMigrationTests**; broader **Hive.Tests** suite for Slice 4 closure
