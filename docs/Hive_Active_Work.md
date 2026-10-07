# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

Phase: **1.18A — Embedded Persistence Profile**

Slice: **1 — Backend-Neutral Persistence Boundary**

Authorization: Explicit user authorization on 2026-10-07 via `Hive: Start Phase 1.18A`. Per the 1.18A plan, only Slice 1 is active; later slices require successful developer verification of this slice.

Repository checkpoint at slice start: `aef7fdd0493d3a7d34bd8111c185a30220a3a67f` (`main`).

## Authorized scope

- Extend the single authoritative `HivePersistenceConfiguration` contract with explicit `SqlServer` and `Embedded` backend selection.
- Add backend-appropriate Embedded configuration state without making SQL Server fields required for Embedded configuration.
- Preserve immutable configuration/effective-snapshot semantics through the existing immutable configuration record.
- Keep SQL Server configuration, bootstrap-credential behavior, and SQL connection construction unchanged.
- Establish a backend-aware host composition boundary that selects by the authoritative backend configuration while keeping Embedded activation deferred to Slice 2.
- Add focused automated coverage for Embedded configuration construction/validation, JSON load/save round-trip, backend separation, and SQL Server composition regression.

## Explicit exclusions

- No SQLite/Microsoft.Data.Sqlite implementation or Embedded persistence store.
- No schema/migration implementation for Embedded.
- No persistence-contract parity implementation.
- No SQL Server ↔ Embedded data migration.
- No Persistence Settings UI/backend selector/data-migration UI.
- No normal-user Embedded first-run default change.
- No vector storage/search, cognitive features, configuration import/export, or unrelated refactoring.

## Verification failure

Developer verification reported compile errors in Slice 1:

- `tests/Hive.Tests/HiveConfigurationTests.cs` lines 114–115: malformed C# raw/interpolated string escaping causing CS1056, CS1003, and CS1026.
- `src/Hive.Persistence/Database/HiveDatabaseOptions.cs` line 82: malformed string literal causing CS1026.

## Remediation boundary

Correct only the reported compile defects and any directly necessary syntax consequence. Do not widen the Slice 1 scope.

## Verification gate

After remediation, Active Work must return to **VERIFICATION PENDING** with the exact rerun target:

`HiveConfigurationTests; HivePersistenceOptionsTests; HiveHostCompositionTests; broader Hive.Tests suite after focused verification.`
