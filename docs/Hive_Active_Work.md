# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.7 — Event Log, Snapshots & Transactional Outbox**

Phase 0 — Foundations and Phase 1.1 through Phase 1.6 are complete and verified.

Do not introduce 1.8 or later Phase 1 slices until 1.7 is complete.

## Objective

Add the durable event/snapshot/outbox boundary required by the roadmap:

- append-only event log;
- deterministic snapshot fold/reconstruction for supported base events;
- atomic event + snapshot + transactional outbox persistence.

The implementation must reuse the existing event envelope/schema-version contracts, resource/version/provenance boundaries, and Hive.Persistence transaction infrastructure rather than introduce parallel representations.

## Phase 1.6 completion

Phase 1.6 — Base Agent Work Protocols is complete and verified.

Developer verification:
- Hive.Example.WinForms `Agents / Base Agent / Base Agent Work Protocols` completed successfully.
- Full `Hive.Tests` execution: **112 tests passed, 0 failed, 0 skipped in 3 seconds**.
- Objective lifecycle, WorkItem binding/provenance, runtime-scoped memory, Question/Answer transport, deterministic Understanding Gate, delegation, and RuntimeInstance isolation were exercised successfully by the Example and focused/full automated tests.
- The 1.6 completion gate is satisfied.

## Architecture / dependency boundary

The durable event boundary remains below later execution and cognitive behavior:

```text
Hive.Core event contracts
        │
        ▼
Hive.Persistence
  ├─ append-only event log
  ├─ snapshot fold
  └─ transactional outbox

Later:
  ├─ outbox processing
  └─ first real Agent execution
```

Event persistence records state transitions; it does not become a second orchestration engine.

## Verification

Required for completion of 1.7:

1. append-only event records preserve event type and payload schema version;
2. supported base events can be folded into deterministic snapshots;
3. event + snapshot + outbox persistence commits atomically;
4. rollback leaves neither the event nor its corresponding outbox record;
5. replay of supported base events is deterministic;
6. invalid serialization/schema cases return typed failures without partial durable state;
7. concurrent operations preserve the existing resource/version invariants;
8. focused automated coverage exists for normal, invalid, rollback, replay, and concurrency cases;
9. public Example Host verification demonstrates the externally usable durable event boundary;
10. broader `Hive.Tests` execution.

No verification claim is recorded until it has actually been performed.

## Constraints

- No 1.8 or later outbox poller implementation.
- No 1.9 MAF Agent execution integration.
- No CognitiveAgent implementation or adaptive cognitive behavior.
- No new cognitive Goals, Beliefs, Dreams, adaptive Question generation, or learning.
- No Management settings/configuration UI.
- No provider transport changes.
- Reuse existing event envelope/schema-version, resource/version/provenance, Result/Error, and persistence contracts.
- Keep the persistence boundary inside `Hive.Persistence`.
- Do not add a second orchestration/workflow engine.
- Preserve transactional semantics and deterministic replay boundaries.

## Implementation checkpoint

The authorized 1.7 implementation is present:
- durable SQL event log keyed by ResourceReference and per-stream ResourceVersion;
- versioned JSON snapshots with atomic replacement;
- transactional outbox rows linked to the triggering event;
- serializable expected-version concurrency boundary;
- deterministic EventSnapshotFolder reducer contract with existing event upcasting;
- migration to schema version 4;
- focused unit/integration coverage;
- public Example Host scenario under Persistence / Events.

Developer verification is still pending. No build, test, or manual Example result is recorded here yet.

## Verification handoff

Example to run: Persistence / Events / Event Persistence / Event Log + Snapshot + Outbox — Hive.Example.WinForms

Tests to run: tests/Hive.Tests/EventPersistenceIntegrationTests.cs and tests/Hive.Tests/EventSnapshotFolderTests.cs; broader Hive.Tests execution is required by the 1.7 completion gate.
