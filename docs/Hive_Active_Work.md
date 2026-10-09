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
- Raw SQL exception text, SQL statements, connection strings, and credentials remain omitted. Management only preserves the explicitly curated migration SQL diagnostic; other unexpected technical errors remain generic.
- The Example Host's Copy details field now includes the stable error code.
- Added diagnostic-mapping and Management sanitization regression tests. Architecture/UI guidance and a separate dated remediation record were updated.
- The original SQL failure's root cause is still **unknown**. No build, test, host launch, or database migration was run by the assistant after these changes.

The earlier 780/780 test result predates this remediation and does not verify the current source. Return to this gate only after the developer runs:
- Focused `HivePersistenceDataMigrationTests` and `HiveUiPolishTests`.
- The full `Hive.Tests` suite.
- All affected builds with Visual Studio **Treat Warnings as Errors** enabled and zero warnings.
- The reported Embedded → SQL Server migration against the intended renamed database. If it still fails, use **Copy details** and provide the new diagnostic; it now includes a curated SQL error number and endpoint context without credentials or raw connection strings.

### Developer-reported compile failure — 2026-10-09

The developer reported two CS0122 compile errors in `tests/Hive.Tests/HivePersistenceDataMigrationTests.cs` because the new regression directly referenced the internal `HivePersistenceMigrationManagementService` type. This was a same-slice regression introduced by the diagnostic remediation. The correction is recorded below: grant `Hive.Tests` test-only internal access in `Hive.Management`, retaining the service's internal visibility and leaving the public/runtime API unchanged. No public type visibility was widened.

### Compile correction — 2026-10-09

Added `src/Hive.Management/AssemblyInfo.cs` with `InternalsVisibleTo("Hive.Tests")`, following the established test-access pattern used by other implementation assemblies. This enables the focused internal sanitization test without making `HivePersistenceMigrationManagementService` public or changing the runtime/public API. Correction evidence: [compile-correction record](verification/phase-1/1.18A-slice-6-compile-correction-2026-10-09.md).

**Current state: VERIFICATION PENDING.** This correction has not been compiled or tested by the assistant. The targeted tests, full test suite, and affected warnings-as-errors builds listed below must be run against the current checkpoint; then repeat the reported Embedded → SQL Server migration using Copy details if it still fails.

### Remaining closure evidence

The reported migration failure authorized same-slice remediation. Because remediation changed source after the 780/780 result, the targeted tests, full suite, and affected warnings-as-errors builds must now be rerun as listed above.

Before Slice 6 can close, confirm the remaining manual acceptance boundary after the graph-retirement change:
- Explicit Example Host verification of both **SQL Server → Embedded** and **Embedded → SQL Server** full-data migrations against representative real endpoints. Each direction must leave the source unchanged, verify the destination, preserve protected-secret readability, and keep destination activation explicit.
- Persistence graph replacement with an operation already in flight: the operation must finish before old stores are disposed; queued migration work on the retired graph must be rejected safely.
- Embedded first-run and close/reopen/application-restart durability, normal Settings behavior after graph replacement, failure/preflight safety, and retained SQL Server setup/connectivity.

The new automated coverage for rollback, database reopen, and operation-gate retirement passed in the reported 780-test suite. The assistant has not itself built, run tests, launched the Example Host, or executed a database migration.

Example to run: `Persistence / Data Migration / Full-Data Migration / SQL Server ↔ Embedded` — `Hive.Example.WinForms`

Tests to run: `HivePersistenceDataMigrationTests` and `HiveUiPolishTests`, then the full `Hive.Tests` suite and all affected project builds with Treat Warnings as Errors enabled and zero warnings. After automated verification, repeat the reported Embedded → SQL Server endpoint migration and continue the remaining Slice 6 real-endpoint/lifecycle gates. The prior 780/780 run predates the diagnostic remediation and does not verify the current source.
