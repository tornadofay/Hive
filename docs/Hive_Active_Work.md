# Hive — Active Work

Status: **IN PROGRESS**

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

### Initial inspection checkpoint

No Slice 6 source edits, builds, automated tests, database operations, or Example Host launches have been performed by the assistant at activation. The next step is source/test inspection to identify concrete, prioritized gaps before implementation.

Example to run: `Persistence / Data Migration / Full-Data Migration / SQL Server ↔ Embedded` — `Hive.Example.WinForms`

Tests to run: `EmbeddedPersistenceFoundationTests`, `EmbeddedPersistenceParityTests`, `HivePersistenceDataMigrationTests`, `HivePersistenceIntegrationTests`, `HiveHostCompositionTests`, `SecretPersistenceIntegrationTests`, `HivePersistenceErrorTests`; broader-suite requirement: full `Hive.Tests` and a zero-warning developer build.
