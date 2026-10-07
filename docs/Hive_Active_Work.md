# Hive — Active Work

Status: VERIFICATION PENDING

Phase: **1.18A — Embedded Persistence Profile**

Slice: **1 — Backend-Neutral Persistence Boundary**

Authorization: Explicit user authorization on 2026-10-07 via `Hive: Start Phase 1.18A`. Per the 1.18A plan, only Slice 1 is active; later slices require successful developer verification of this slice.

Repository checkpoint after same-slice remediation: `53dc0d7d2131208571013bbeeaa998fb96559d2f` (`main`).

## Authorized scope

- Extend the single authoritative `HivePersistenceConfiguration` contract with explicit `SqlServer` and `Embedded` backend selection.
- Add backend-appropriate Embedded configuration state without making SQL Server fields required for Embedded configuration.
- Preserve immutable configuration/effective-snapshot semantics through the existing immutable configuration record.
- Keep SQL Server configuration, bootstrap-credential behavior, and SQL connection construction unchanged.
- Establish a backend-aware host composition boundary that selects by the authoritative backend configuration while keeping Embedded activation deferred to Slice 2.
- Add focused automated coverage for Embedded configuration construction/validation, JSON load/save round-trip, backend separation, and SQL Server composition regression.

## Latest verification failure

Developer verification on 2026-10-07 reported 721 tests run: 720 passed, 1 failed. The failing test was `Hive.Tests.HiveConfigurationTests.EmbeddedPersistenceConfiguration_RequiresStoragePath`.

The assertion expected exception parameter name `storagePath`, but `HivePersistenceConfiguration` reported `embeddedStoragePath`. The remediation aligned the authoritative constructor validation parameter with the public storage-path parameter name: `storagePath`.

## Completed same-slice remediation

Developer-reported Slice 1 compile failures and the subsequent verification assertion failure have been remediated:

- corrected the malformed raw/interpolated JSON test string in `tests/Hive.Tests/HiveConfigurationTests.cs`;
- corrected the missing closing parenthesis in `src/Hive.Persistence/Database/HiveDatabaseOptions.cs`;
- aligned the Embedded persistence storage-path validation parameter name in `src/Hive.Core/Configuration/HiveConfigurationContracts.cs`.

## Verification gate

Developer re-verification is now required. Rerun exactly:

`HiveConfigurationTests; HivePersistenceOptionsTests; HiveHostCompositionTests; broader Hive.Tests suite after focused verification.`

No verification result is claimed yet. Later 1.18A slices remain unauthorized until Slice 1 passes its gate.
