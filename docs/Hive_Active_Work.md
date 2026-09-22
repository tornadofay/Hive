# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.9 — First Real Agent Execution**

Phase 0 — Foundations and Phase 1.1 through Phase 1.8 are complete and verified.

Phase 1.9 is now the authorized active slice.

## Objective

Process committed, unhandled transactional outbox rows after the originating event transaction has committed.

The implementation must reuse the existing Hive.Persistence outbox records and event identity/version contracts. It must not become a distributed broker or duplicate MAF/workflow orchestration.

## Phase 1.7 completion

Phase 1.7 — Event Log, Snapshots & Transactional Outbox is complete and verified.

Developer verification:
- Hive.Example.WinForms `Persistence / Events / Event Persistence / Event Log + Snapshot + Outbox` completed successfully.
- The Example Host demonstrated two committed event versions, snapshot version 2, corresponding outbox data, deterministic fold count 5, and schema version 4.
- Full `Hive.Tests` execution: **120 tests passed, 0 failed, 0 skipped in 3.5 seconds**.
- The 1.7 completion gate is satisfied.

## Phase 1.6 completion

Phase 1.6 — Base Agent Work Protocols is complete and verified.

Developer verification:
- Hive.Example.WinForms `Agents / Base Agent / Base Agent Work Protocols` completed successfully.
- Full `Hive.Tests` execution: **112 tests passed, 0 failed, 0 skipped in 3 seconds**.
- Objective lifecycle, WorkItem binding/provenance, runtime-scoped memory, Question/Answer transport, deterministic Understanding Gate, delegation, and RuntimeInstance isolation were exercised successfully by the Example and focused/full automated tests.
- The 1.6 completion gate is satisfied.

## Architecture / dependency boundary

The outbox poller remains below later execution and cognitive behavior:

```
Hive.Core event contracts
        │
        ▼
Hive.Persistence
  ├─ event log
  ├─ snapshots
  ├─ transactional outbox
  └─ outbox poller
        │
        ▼
Later:
  └─ first real Agent execution
```

The poller processes committed outbox work; it does not become a second orchestration/workflow engine.

## Verification

Required for completion of 1.8:

1. committed outbox entries can be discovered and processed;
2. successful processing does not lose or corrupt the corresponding event identity/version;
3. duplicate delivery is safe and idempotent;
4. a crash/failure before processing completes leaves the outbox entry available for recovery;
5. cancellation and retry boundaries are deterministic;
6. processing failures are observable as typed results/errors rather than silently swallowed;
7. focused automated coverage exists for normal, duplicate, failure/recovery, and concurrency cases;
8. public Example Host verification demonstrates the externally usable poller behavior;
9. broader `Hive.Tests` execution.

No verification claim is recorded until it has actually been performed.

## Constraints

- No 1.9 MAF Agent execution integration.
- No CognitiveAgent implementation or adaptive cognitive behavior.
- No new cognitive Goals, Beliefs, Dreams, adaptive Question generation, or learning.
- No Management settings/configuration UI.
- No provider transport changes.
- Reuse the existing durable event log, snapshot, outbox, event envelope/schema-version, Result/Error, and persistence contracts.
- Keep the persistence boundary inside `Hive.Persistence`.
- Do not add a second orchestration/workflow engine.
- The outbox is not a distributed message broker.

## Phase 1.8 completion

Phase 1.8 — Outbox Poller is complete and verified.

Developer verification:
- Hive.Example.WinForms `Persistence / Events / Outbox Poller / Transactional Outbox Poller` completed successfully.
- Example output confirmed simulated first-delivery failure, retained lease, successful retry, preserved event identity, one idempotent side effect, no remaining outbox row, and migration schema 5.
- Full `Hive.Tests` execution: **124 tests passed, 0 failed, 0 skipped in 2.7 seconds**.
- The 1.8 completion gate is satisfied.

## 1.9 objective

Connect a base Agent to MAF and the Hive provider boundary for one request, with correlation and durable lifecycle events.

Do not implement later Phase 1 slices in this active slice.

## Verification handoff

Example to run: <add the exact 1.9 Example Host path when its implementation exists> — Hive.Example.WinForms

Tests to run: <add the exact 1.9 focused test file when its implementation exists>; broader Hive.Tests execution will be required by the 1.9 completion gate.

