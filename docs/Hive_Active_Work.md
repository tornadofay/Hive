# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.9 — First Real Agent Execution**

Phase 0 — Foundations and Phase 1.1 through Phase 1.8 are complete and verified.

Phase 1.9 is now the authorized active slice.

## Objective

Connect a Base Agent to Microsoft Agent Framework and the existing Hive provider boundary for one request, with correlation and durable lifecycle events.

The implementation must reuse the existing Agent/Runtime/Execution, ExecutionTarget, OpenAI-compatible provider, and Hive.Persistence event contracts. It must not become a second orchestration/workflow engine.

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

```
Hive.Core
   │
   ├── Hive.Agents
   ├── Hive.Persistence
   └── Hive.Providers.OpenAICompatible
             │
             ▼
      Hive.Coordination
        └─ AgentExecutionService
             │
             ▼
        Microsoft Agent Framework
```

`Hive.Coordination` composes one execution request across the existing Hive Agent/runtime contracts, selected ExecutionTarget, provider adapter, and durable event store. It does not own SQL schema, provider transport, or workflow orchestration.

## Verification

Required for completion of 1.9:

1. a Base Agent can execute one request through MAF and the existing OpenAI-compatible provider boundary;
2. execution start and terminal state are persisted to the Execution event stream;
3. lifecycle events preserve one correlation identity and terminal causation;
4. provider failures become typed Hive errors and persist a failed lifecycle event;
5. caller cancellation becomes a typed cancelled result and persists a cancelled lifecycle event;
6. focused automated coverage exists for success, provider failure, and cancellation;
7. public Example Host verification demonstrates the end-to-end path against a local fake provider;
8. broader `Hive.Tests` execution.

No verification claim is recorded until actual execution has been performed.

## Constraints

- No Phase 1.10 or later implementation.
- No CognitiveAgent implementation or adaptive cognitive behavior.
- No new cognitive Goals, Beliefs, Dreams, adaptive Question generation, or learning.
- No Management settings/configuration UI.
- No new provider transport implementation; reuse the existing OpenAI-compatible adapter.
- No Agent generation promotion/demotion.
- Reuse the existing Agent/Runtime/Execution, ExecutionTarget, event envelope/schema-version, Result/Error, and persistence contracts.
- Keep SQL/persistence ownership inside `Hive.Persistence`.
- Use MAF for the actual agent invocation; do not add a second workflow/orchestration engine.

## Phase 1.8 completion

Phase 1.8 — Outbox Poller is complete and verified.

Developer verification:
- Hive.Example.WinForms `Persistence / Events / Outbox Poller / Transactional Outbox Poller` completed successfully.
- Example output confirmed simulated first-delivery failure, retained lease, successful retry, preserved event identity, one idempotent side effect, no remaining outbox row, and migration schema 5.
- Full `Hive.Tests` execution: **124 tests passed, 0 failed, 0 skipped in 2.7 seconds**.
- The 1.8 completion gate is satisfied.

## Implementation checkpoint

Phase 1.9 implementation is present; developer verification is pending.

Implemented:
- current MAF package boundary in `Hive.Coordination`;
- `IChatClient` bridge over the existing OpenAI-compatible provider adapter;
- Base Agent execution service with active-runtime/target-scope validation;
- durable started/succeeded/failed/cancelled Execution lifecycle events;
- correlation and terminal causation preservation;
- focused integration tests for success, provider failure, and cancellation;
- public Example Host scenario using a local fake provider.

No verification claim is recorded yet.

Developer verification attempt on 2026-09-22:
- Hive.Example.WinForms `Agents / Base Agent / First Real Agent Execution` completed successfully against the local fake provider: execution succeeded, two lifecycle events were observed, correlation/causation were preserved, and schema version 5 was applied.
- The focused `Hive.Tests` target did not compile because five assertions in `AgentExecutionIntegrationTests.cs` passed typed `CausationId` / `ExecutionId` values where underlying `Guid` values are required. Those test-only type errors are corrected in this change; automated verification remains pending.

## Verification handoff

Example to run: Agents / Base Agent / First Real Agent Execution — Hive.Example.WinForms

Tests to run: tests/Hive.Tests/AgentExecutionIntegrationTests.cs; broader Hive.Tests execution is required by the 1.9 completion gate.

