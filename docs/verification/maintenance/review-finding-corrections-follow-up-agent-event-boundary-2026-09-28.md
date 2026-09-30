# Maintenance — Review Finding Corrections Follow-Up: Agent Start Persistence and Event Persistence Boundary

Date: 2026-09-28

## Status

Complete and verified after developer re-verification of the bounded corrective slice.

## Scope completed

1. Reconciled ambiguous initial `agent.execution.started` persistence by EventId before terminal compensation, using the reconciled stream version and correct causation when the started event is durable.
2. Added a stable terminal EventId so an ambiguous terminal append is reconciled rather than duplicated.
3. Replaced the empty initial-recovery catch with bounded error handling and diagnostics while preserving the original execution-start failure/cancellation result.
4. Hardened the raw event persistence boundary by making `IEventPersistenceStore` and `SqlEventPersistenceStore` internal infrastructure types.
5. Added the opaque `HiveEventPersistenceComposition` / `HiveEventPersistence.CreateSql(...)` composition boundary for trusted Hive wiring and kept `Hive.Management` as the application-facing authorization boundary.
6. Updated Host and public Examples to use the hardened persistence composition boundary; removed the raw Event Persistence Example that would have exposed the internal store contract.
7. Added focused regression coverage for initial lifecycle reconciliation, ambiguous terminal persistence reconciliation, and the public persistence boundary.

## Developer verification

Developer reported:

- Full `Hive.Tests` suite: 400 tests passed
- 0 failed
- 0 skipped
- 49.7 seconds

Example Host scenarios were also manually confirmed successfully:

- `Agents / Base Agent / First Real Agent Execution`
  - Execution status: `Succeeded`
  - Started and terminal event identities were reported.
  - Correlation identity was reported.
  - Migration applied at schema 13.
- `Persistence / Events / Outbox Poller / Transactional Outbox Poller`
  - First delivery exceeded the lease and was successfully covered by renewal before the simulated failure.
  - Retry delivery succeeded with event identity preserved.
  - Idempotent side effects remained at 1.
  - Outbox was empty after processing.
  - Migration applied at schema 13.

The reported test run used xUnit/VSTest with .NET 10.0.1.

## Review outcome

The two authorized review-finding corrections are complete and verified. The slice is closed.

No Phase 1.16+ implementation was performed or authorized. No new event/business capability, provider change, UI change, dependency change, or schema migration was introduced by this slice.
