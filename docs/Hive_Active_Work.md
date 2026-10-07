# Hive — Active Work

Status: **IMPLEMENTATION ACTIVE**

Phase: **1.18A — Embedded Persistence Profile**

Slice: **3 — Embedded Persistence Parity**

Authorized by the user on **2026-10-07** with: “Hive: Continue start next slice 3”.

Authorization boundary: implement the Embedded backend for the complete current SQL Server-backed durable persistence surface identified by the 1.18A catch-up baseline. The current repository baseline is schema version **15** and includes Provider, ProviderAccount, ExecutionTarget, ExecutionTarget favorites, AgentDefinition, WorkItem and immutable attachments, durable events/snapshots/outbox state, structured-extraction persistence, Base-Agent durable work state, and the Hive Secret Store.

Required responsibilities within this slice:

- add Embedded SQLite implementations of every applicable current persistence contract;
- preserve logical identities, ownership/scope, provenance, lifecycle, versions/concurrency semantics, deterministic ordering, cancellation, and typed error behavior;
- bring Embedded schema/migrations to the current logical persistence baseline without implementing Slice 4 migration;
- reuse the existing Management/public persistence contracts rather than introducing an Embedded-specific resource model;
- add focused and shared behavioral parity coverage comparing SQL Server and Embedded behavior where practical;
- preserve SQL Server implementation and regression behavior.

Verification gate:

- shared behavioral coverage demonstrates SQL Server and Embedded satisfy the same applicable persistence contracts;
- representative end-to-end durable scenarios persist and reload correctly through Embedded;
- SQL Server remains regression-safe;
- developer verification is required before Slice 4 may be activated.

Verification status: **PENDING**

Focused verification target: Embedded persistence parity tests plus the broader Hive.Tests suite.

Developer verification failure received on 2026-10-08: developer reran the full Hive.Tests suite and reported 739 tests total, 737 passed, 2 failed, 0 skipped. Same-slice remediation aligned the existing EmbeddedPersistenceFoundationTests fixtures with the authorized Slice 3 schema-15 baseline: the initialization test now expects all current migrations to apply, and the rollback test supplies a complete contiguous 1..15 injected catalog so the test reaches the intended migration-execution failure without violating the production catalog invariant. No build or test execution was performed by the agent.

Exact rerun targets: rebuild/compile `Hive.Persistence` and `Hive.Tests`, run `EmbeddedPersistenceParityTests`, then run the full `Hive.Tests` suite.

Implementation exclusions:

- no SQL Server → Embedded or Embedded → SQL Server logical migration;
- no Persistence Settings UI or first-run default behavior;
- no host backend activation/composition changes owned by Slice 5;
- no vector storage/search/indexing owned by 1.19;
- no unrelated persistence redesign, public-domain model split, or future roadmap work.

Repository checkpoint at activation: cf22e5423b50a095e33063aa44c23aa811046080.

The slice remains active until its verification result is reconciled or the slice is explicitly closed. A failed developer verification must be recorded as **VERIFICATION FAILED / REMEDIATION REQUIRED** before same-slice remediation, then returned to **VERIFICATION PENDING** with exact rerun targets.
