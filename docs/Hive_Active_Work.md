# Hive — Active Work

Status: **VERIFICATION PENDING**

## Phase 1.18A Slice 6 — Cross-Backend Hardening, Full Regression & Closure

Authorized by explicit implementation instruction:

`Hive: Continue Start Phase 1.18A Slice 6`

Repository checkpoint at activation: `ada5298aa5b18cedae1de73474e822ecf06da746`.

Previous slice closure:
- Phase 1.18A Slices 1–4 were developer-verified and archived.
- Slice 5 closed on 2026-10-09 after the developer reported `Hive.Tests` 777/777 passing, a successful Treat Warnings as Errors / zero-warning build, successful Example Host Persistence workflow, and a successful real-endpoint migration with a satisfactory outcome.
- Slice 5 closure evidence: [verification record](verification/phase-1/1.18A-slice-5-host-composition-settings-ui-first-run-default-closure-2026-10-09.md).
- The former chronological Slice 5 Active Work log was preserved as [historical snapshot](verification/phase-1/1.18A-slice-5-active-work-history-2026-10-09.md).

### Objective

Finish Phase 1.18A by hardening and verifying the combined SQL Server and Embedded persistence profiles against the current durable Hive persistence surface, then close the phase only from actual developer build/test/manual evidence.

### Authorized scope

- Inspect cross-backend persistence implementation and existing automated coverage for concrete gaps in restart/reopen, crash/failure recovery, schema compatibility, transaction rollback, deterministic state, and storage error boundaries.
- Harden concurrency, optimistic/version behavior, cancellation, operation admission, and resource disposal where source/test evidence demonstrates in-scope gaps.
- Verify parity for the current schema-15 durable persistence surface through shared logical contracts and reusable test-side abstractions where contracts are common.
- Harden full-data migration in both directions: SQL Server → Embedded and Embedded → SQL Server. Preserve all current Hive-owned durable resource/state families, identities, relationships, versions, lifecycle/provenance, events/snapshots/outbox state, WorkItems/attachments, favorites, Agent work state, and Secret Store semantics.
- Preserve migration guarantees: explicit independent SOURCE and DESTINATION configurations; clean/current destination; deterministic transfer; source immutability and source re-verification; destination verification before completion; incomplete/failed/cancelled migration safety; quiescence of an active source graph; Secret Store material re-protection under the destination boundary; SQL bootstrap credential non-migration; no implicit destination activation.
- Verify safe host graph replacement and rollback, normal-user Embedded first-run behavior, developer/test LocalDB behavior, and existing SQL Server compatibility.
- Complete the required consumer-facing `Hive.Example.WinForms` Persistence scenario using public Management contracts and deterministic/reproducible fixtures. Do not create a competing service graph or expose secret material.
- Update focused tests, required architecture/UI/example guidance, Current Status, and historical verification records to match actual behavior and actual developer evidence.

### Explicit exclusions

- No Phase 1.19 vector storage, indexing, or search work.
- No third persistence backend, live synchronization, merge migration, partial resource-family migration, or destructive overwrite.
- No new domain capability, unrelated refactor, speculative abstraction, dependency upgrade without a demonstrated need, or roadmap advancement beyond closing Phase 1.18A.
- Do not claim builds, tests, migrations, or manual UI checks were executed by the assistant when they were not.

### Verification boundary

Slice 6 closes only when all of the following are evidenced:

- The full `Hive.Tests` suite passes with zero failures/skips as required by the final run, including focused coverage for all changed cross-backend boundaries.
- All affected projects build successfully with Visual Studio **Treat Warnings as Errors** enabled and zero warnings.
- SQL Server and Embedded persist/reload the current durable state across runtime/application recreation; failure/cancellation, rollback, locking, disposal, ownership/scope, and concurrency/versioning boundaries remain deterministic and safe.
- Both migration directions pass against representative real endpoint datasets, preserving identities/relationships/durable state and protected-secret readability; source is unchanged, destination verification succeeds, and destination activation is explicit.
- Invalid endpoints, unsuitable destinations, concurrent source changes, cancellation/failure, and unquiesced active operations fail safely without presenting partial migration as complete.
- The required Example Host scenario is manually exercised for Embedded first-run initialization, reopen/restart durability, Database Setup, Data Migration in both directions, full-data verification, failure/preflight behavior, secret safety, explicit backend activation, and retained SQL Server configuration.
- Current Status, Active Work, the relevant phase plan/architecture guidance if needed, and a dated verification archive accurately reflect the final developer evidence. Phase 1.18A is not marked complete before this gate passes.

### Implementation and review checkpoint — 2026-10-09

The first Slice 6 implementation pass makes these bounded changes:

- Full-data migration now uses `CancellationToken.None` for each explicit destination rollback path. Caller cancellation remains effective for transfer work, but cannot also cancel an explicit rollback attempt.
- Strengthened `Migration_RollsBackDestinationWhenTransferFails`: the deterministic Embedded trigger now fails on the ExecutionTarget insert, after Provider and ProviderAccount rows have been inserted in the same destination transaction. The regression asserts all migration data tables remain empty after failure.
- Added `EmbeddedDatabase_ReopenPreservesCurrentDurableState`. It closes one Embedded database instance and reopens the same file through a new instance, then verifies the current schema and durable Provider / ProviderAccount / ExecutionTarget relationships, favorites, AgentDefinition target binding, WorkItem attachment bytes, event/snapshot/outbox state, and Base-Agent objective/memory.
- Added an async retirement lifecycle for published host graphs. Online recomposition now awaits the previous graph's `DisposeAsync`; the graph acquires an exclusive retirement lease, stops admission before waiting, drains active Management operations, rejects queued migrations against the retired graph, then disposes Management and its owned persistence resources. Both SQL Server and Embedded graph factories pass the same per-graph operation gate to graph ownership. The synchronous shutdown path remains non-blocking and uses immediate disposal.
- Added focused regressions for Management-gate retirement / queued-migration rejection and for host composition waiting to dispose old graph resources until registered operations drain.
- Updated persistence and host lifecycle architecture guidance before the structural lifecycle changes.

### Developer verification update — 2026-10-09

Current state: **VERIFICATION PENDING**.

Developer-reported results after the first Slice 6 implementation pass:
- Full `Hive.Tests`: **780/780 passed, 0 failed, 0 skipped** in approximately 2.6 minutes on .NET 10.0.1.
- All affected projects were reported built successfully with zero errors under the standing Treat Warnings as Errors configuration.
- The Example Host reported a successful full-data migration. Its supplied run log records schema 15 → 15, 11 records, source verified unchanged, destination verified, destination activation false, quiescence acquired/released, credential reference and target/favorite identities preserved, AgentDefinition/WorkItem/attachment preservation, and Secret Store re-protection/readability without printing the secret.
- Dated evidence: [Slice 6 verification checkpoint](verification/phase-1/1.18A-slice-6-verification-2026-10-09.md).

The supplied migration log does not explicitly identify its destination backend/direction. The bidirectional feature heading alone is not proof that both real-endpoint directions were manually exercised.

### Developer-reported migration failure — 2026-10-09

Verification status: **VERIFICATION FAILED / REMEDIATION REQUIRED** for the Embedded → SQL Server real-endpoint migration path.

The developer reported renaming their local SQL Server database, then running Embedded → SQL Server full-data migration and receiving this UI message:

`Migration failed: Hive persistence data migration could not be completed safely.`

The available message is too generic to identify the underlying cause. The failure boundary is limited to the same-slice Embedded → SQL Server migration error path and safe diagnostics: preserve expected typed preflight/conflict errors, provide actionable non-secret failure details for endpoint/SQL failures, and add regression coverage proving the user-visible diagnostic is useful without emitting credentials or raw connection strings. Do not infer that the database rename itself is the root cause until the failure path is distinguishable. The failure is recorded before implementation changes.

### Same-slice diagnostic remediation — 2026-10-09

The reported failure was traced to the error-reporting boundary: unexpected SQL Server exceptions were reduced to a generic message by the persistence migrator and then sanitized again by Management. That diagnostic gap is corrected in the same Slice 6 migration-hardening scope.

- SQL Server exceptions now produce a curated, non-secret diagnostic with the source/destination role, configured server/database, SQL error number/state/class, and fixed guidance for common renamed/missing database, access/permission, authentication, duplicate/schema, timeout, and endpoint-resolution failures.
- Raw SQL exception text, SQL statements, connection strings, and credentials remain omitted. Management preserves the explicitly curated SQL diagnostic and, after the follow-up below, a fixed-format exception-type-only fallback for unexpected schema-migration failures; other unexpected technical errors remain generic.
- The initial UI pass exposed copyable inline diagnostic text with the stable error code; the later follow-up below replaces that textbox with a themed HiveMessageBox dialog.
- Added diagnostic-mapping and Management sanitization regression tests. Architecture/UI guidance and a separate dated remediation record were updated.
- The original SQL failure's root cause is still **unknown**. No build, test, host launch, or database migration was run by the assistant after these changes.

The earlier 780/780 test result predates this remediation and does not verify the current source. Return to this gate only after the developer runs:
- Focused `HivePersistenceDataMigrationTests` and `HiveUiPolishTests`.
- The full `Hive.Tests` suite.
- All affected builds with Visual Studio **Treat Warnings as Errors** enabled and zero warnings.
- The reported Embedded → SQL Server migration against the intended renamed database. If it still fails, copy the expanded diagnostic from the HiveMessageBox error dialog; it now includes curated SQL error metadata and endpoint context without credentials or raw connection strings.

### Developer-reported compile failure — 2026-10-09

The developer reported two CS0122 compile errors in `tests/Hive.Tests/HivePersistenceDataMigrationTests.cs` because the new regression directly referenced the internal `HivePersistenceMigrationManagementService` type. This was a same-slice regression introduced by the diagnostic remediation. The correction is recorded below: grant `Hive.Tests` test-only internal access in `Hive.Management`, retaining the service's internal visibility and leaving the public/runtime API unchanged. No public type visibility was widened.

### Compile correction — 2026-10-09

Added `src/Hive.Management/AssemblyInfo.cs` with `InternalsVisibleTo("Hive.Tests")`, following the established test-access pattern used by other implementation assemblies. This enables the focused internal sanitization test without making `HivePersistenceMigrationManagementService` public or changing the runtime/public API. Correction evidence: [compile-correction record](verification/phase-1/1.18A-slice-6-compile-correction-2026-10-09.md).

**Current state: VERIFICATION PENDING.** This correction has not been compiled or tested by the assistant. The targeted tests, full test suite, and affected warnings-as-errors builds listed below must be run against the current checkpoint; then repeat the reported Embedded → SQL Server migration using Copy details if it still fails.

### Developer-reported migration failure and UI feedback — 2026-10-09

The developer reports:
- Full `Hive.Tests` suite: **783/783 passed, 0 failed, 0 skipped**, in 2.8 minutes.
- The real Embedded → SQL Server migration still reports `Error code: hive.persistence.migration-unexpected` and the sanitized generic message `Hive persistence data migration could not be completed safely.`
- The developer requests that migration errors appear through `HiveMessageBox` rather than only in the diagnostic textbox below the migration workspace.

Source inspection found the direct reason the previous diagnostics did not catch this case: `HiveDatabaseMigrator.MigrateCoreAsync` catches SQL Server exceptions and returns the generic `hive.persistence.migration-unexpected` Result. Embedded → SQL Server destination preflight/creation invokes this migrator, so the original `SqlException` never reaches `HivePersistenceDataMigrator`'s SQL-specific catch. Management then safely replaces that generic external error message again. This establishes the diagnostic loss path, but does not yet identify the underlying database/SQL error.

Same-slice remediation boundary:
- Preserve safe SQL-native number/state/class and endpoint identity from exceptions encountered by the SQL schema migrator, including wrapped SQL exceptions. Continue omitting raw exception messages, connection strings, and credentials.
- Allow Management to preserve only explicitly curated SQL migration diagnostics and keep other unexpected errors generic.
- Show returned migration failures in a themed `HiveMessageBox` error dialog instead of presenting them only in the inline diagnostic textbox. Keep a concise footer status; use the dialog's details/copy affordance for technical details where appropriate. Do not disclose raw exception or credential material.
- Add focused tests for the previously swallowed SQL error and preserve test-access boundaries.

The 783/783 automated result is accepted for the checkpoint immediately before this new remediation. The new failure and requested UI change are recorded before source modification. The actual underlying SQL failure remains unverified until the corrected UI yields a concrete diagnostic or the migration succeeds.

### Same-slice SQL migration exception and modal error remediation — 2026-10-09

The developer subsequently reported that `Hive.Tests` passed **783/783** (0 failed, 0 skipped) in 2.8 minutes, but the real Embedded → SQL Server migration still displayed:

```text
Error code: hive.persistence.migration-unexpected
Hive persistence data migration could not be completed safely.
```

Inspection located the additional sanitization gap in `HiveDatabaseMigrator.MigrateCoreAsync`: it caught exceptions during SQL database/schema setup and converted every exception to `hive.persistence.migration-unexpected`, so the outer full-data migrator never received the underlying SQL exception. The actual SQL error has not yet been established.

Same-slice correction now committed to `main`:
- Added shared `HiveSqlServerFailureDiagnostics` mapping for the known SQL Server failure categories. It uses only fixed guidance, configured endpoint/server/database identity, and SQL error number/state/class; it does not surface provider exception text, SQL text, connection strings, or credentials.
- `HiveDatabaseMigrator` now finds a nested `SqlException` and returns `hive.persistence.migration-sql-failure` with the safe diagnostic. If the exception is not a SQL exception, it preserves only the exception type in the existing `hive.persistence.migration-unexpected` diagnostic, never the exception message.
- Management preserves only the two curated SQL-diagnostic codes and the exact fixed-format type-only fallback; unrelated errors still become the generic safe message.
- The migration/settings view now opens a themed `HiveMessageBox` dialog for endpoint preflight, invalid-configuration, and migration failures. The dialog has a concise message and expanded/copyable safe details. The inline diagnostic textbox and footer Copy details button have been removed, so errors no longer appear only in a textbox below the workspace.
- Added focused SQL-schema-diagnostic tests, updated the endpoint-preflight UI regression for modal error details, and added `HivePersistenceDataMigration_ShowsReturnedFailureInHiveMessageBox` to cover the returned Management migration-failure path through an injected test presenter.
- Updated UI and host/management architecture documentation, and reconciled the stale `docs/ui/examples.md` statement that Slice 5 verification was still pending with its recorded closure.

**Current state: VERIFICATION PENDING.** The developer's 783/783 result predates this latest change and does not verify it. The assistant has not run a build, test suite, launch, or real migration after the correction. Required local verification is listed below. If the migration still fails, copy the details from the modal HiveMessageBox; expected outcome is either a native SQL error code with guidance or, for a non-SQL exception, its type name only. Do not infer the underlying SQL cause until that diagnostic or a successful rerun is observed.

### Developer-reported SQL Server file collision — 2026-10-09

The latest real Embedded → SQL Server migration now exposes the native SQL Server failure instead of the former generic error:

```text
Error code: hive.persistence.migration-sql-failure
SQL Server endpoint: localhost\\MSSQLSERVER01
Database: Hive-Hive.Example.WinForms
SQL error: 5170, state 4, class 16
```

SQL Server error 5170 indicates that SQL Server cannot create a database file because the target physical file path already exists. Given the developer's earlier database rename, the likely cause is a stale physical data/log filename retained by the renamed database while Hive attempts to create the configured old database name. That explanation is a strong diagnosis, not direct proof of which existing database owns the path, because raw server text and physical paths are deliberately omitted.

Same-slice remediation boundary:
- Add an explicit safe 5170 guidance branch to the shared SQL Server migration diagnostic mapper and regression coverage for both schema-migration and full-data-migration diagnostic paths.
- Tell the operator to inspect ownership of the conflicting file path and not delete database files. Recommend a different unused destination database name or a deliberate SQL Server-managed physical-file relocation/rename after ownership and backups are confirmed.
- Keep raw SQL text, actual physical file path, connection strings, and credentials out of Hive's UI diagnostic. Do not add destructive/automatic file cleanup or alter the migration's explicit destination configuration.
- Update the historical diagnostic record, Current Status, and Active Work after the bounded correction.

The developer's current report establishes SQL error 5170; it does not verify a successful migration. Record this failure before changing implementation. After the same-slice correction, return to **VERIFICATION PENDING** and require focused tests, the full suite, warnings-as-errors builds, and another safe migration attempt.

### Same-slice SQL Server 5170 diagnostic correction — 2026-10-09

After the last migration retry supplied native SQL error 5170 / state 4 / class 16 for destination database `Hive-Hive.Example.WinForms` at `localhost\\MSSQLSERVER01`, the error-mapping gap was recorded as a verification failure before source changes. SQL Server error 5170 means a database file cannot be created because the physical file path already exists. The previous database rename makes retained original data/log filenames a likely explanation, but the owner of the conflicting path has not been inspected, so that part remains an informed hypothesis.

The bounded correction now committed to `main`:
- `HiveSqlServerFailureDiagnostics` maps error 5170 to specific, fixed guidance that explains the physical-file collision and possible retained filenames after database rename.
- The guidance directs the operator to inspect registered file ownership, explicitly warns not to delete a conflicting file manually, and suggests a different unused destination name or deliberate SQL Server-managed file relocation after ownership and backup are confirmed.
- Added `5170` regression cases to both full-data-migration and SQL schema-migration diagnostic coverage. The diagnostic still excludes raw provider text, physical paths, SQL statements, connection strings, and credentials.
- No database files are altered automatically, no destination is renamed implicitly, and clean-destination / explicit-activation migration behavior is unchanged.
- Dated evidence: [SQL Server 5170 diagnostic remediation](verification/phase-1/1.18A-slice-6-sql-file-collision-5170-remediation-2026-10-09.md).

**Current state: VERIFICATION PENDING.** The earlier 783/783 result predates the 5170-specific mapping and its regression cases. The assistant has not run a build, tests, Example Host, or migration after this correction. Run `HivePersistenceDataMigrationTests` and `HiveUiPolishTests`, the full `Hive.Tests` suite, and all affected builds with Treat Warnings as Errors enabled and zero warnings. Then resolve the SQL Server file collision safely and retry Embedded → SQL Server. The remaining Slice 6 bidirectional real-endpoint, graph-retirement, Embedded first-run/restart, source-immutability, secret-readability, and explicit activation gates remain required.

### Developer verification after SQL Server 5170 correction — 2026-10-09

The developer reports that the real Embedded → SQL Server full-data migration succeeded after correcting the destination database name. Visual Studio **Treat Warnings as Errors** was enabled. The full `Hive.Tests` suite passed **786/786**, 0 failed, 0 skipped, in 2.5 minutes on .NET 10.0.1.

This verification follows the 5170-specific diagnostic and test correction, so the prior SQL error is no longer blocking the reported migration attempt. The developer's update did not include a separate affected-project build/zero-warning summary, nor explicit details for source-unchanged verification, destination verification, protected-secret readability, or destination activation; do not claim those acceptance checks as established by this report. Dated evidence: [successful retry and 786-test verification](verification/phase-1/1.18A-slice-6-sql-file-collision-verification-2026-10-09.md).

**Current state: VERIFICATION PENDING.** The automated suite now has a developer-reported passing result after the latest diagnostic correction, and the immediate Embedded → SQL Server real-endpoint attempt reportedly succeeded. Slice 6 remains open until the remaining manual/bidirectional gates below have evidence. No build, test, Example Host launch, or migration was executed by the assistant.

### Remaining closure evidence

The current developer report closes the immediate SQL Server 5170 failure scenario and reports the full automated test suite passing. Before Slice 6 can close, confirm all remaining acceptance gates with actual developer evidence:

- Provide a separate affected-project build result with Visual Studio **Treat Warnings as Errors** enabled and zero warnings (the latest report says the setting was enabled but does not explicitly report the affected-project build/zero-warning outcome).
- Explicitly verify both **SQL Server → Embedded** and **Embedded → SQL Server** full-data migrations against representative real endpoints. For each direction confirm source immutability, destination verification, preservation and readability of protected secrets, and that destination activation remains false until explicit user action. The latest report confirms the successful Embedded → SQL Server attempt but did not report these detailed assertions for that retry.
- Verify persistence graph replacement while an operation is in flight: the active operation drains before old stores are disposed and queued migration work on a retired graph is rejected safely.
- Verify Embedded first-run and close/reopen/application-restart durability, normal Settings behavior after graph replacement, preflight/failure safety, and retained SQL Server setup/connectivity.

Example to run: `Persistence / Data Migration / Full-Data Migration / SQL Server ↔ Embedded` — `Hive.Example.WinForms`

Tests to run: The developer reports the full `Hive.Tests` suite passed **786/786** after the current diagnostic correction, including focused migration-diagnostic and UI regressions. No automatic tests are currently reported failing. For final closure, supply the separate affected-project warnings-as-errors / zero-warning build evidence and the remaining manual acceptance evidence above. Phase 1.19 remains unauthorized until Phase 1.18A is closed.
