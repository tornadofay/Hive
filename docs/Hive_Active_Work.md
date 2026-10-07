# Hive — Active Work

Status: IMPLEMENTATION ACTIVE

Phase: **1.18A — Embedded Persistence Profile**

Slice: **2 — Embedded Persistence Foundation**

Authorization: Explicit user authorization on 2026-10-07 via 'start slice 2', following successful developer verification of Slice 1 (721/721 tests passed).

Repository checkpoint: `59fe9bcbc916a417938bd0024a97b99a3f4ab7c0` (`main`).

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

The Slice 2 gate must cover clean initialization, schema/version behavior, reopen/recovery, corruption/incompatibility handling, path/permission failures, locking/busy behavior, rollback, cancellation, and disposal. No verification result is claimed until the developer reports the runs.
