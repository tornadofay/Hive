# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.6 — Base Agent Work Protocols**

Phase 0 — Foundations and Phase 1.1 through Phase 1.5 are complete and verified.

Do not introduce 1.7 or later Phase 1 slices until 1.6 is complete.

## Objective

Add only the reusable base Agent mechanisms required by the V1 boundary:

- Objective lifecycle;
- WorkItem binding and provenance;
- explicit memory storage/retrieval infrastructure;
- Question/Answer transport;
- deterministic Patience / Understanding Gate;
- delegation interfaces and provenance;
- isolation of these mechanisms across independent RuntimeInstance objects.

These mechanisms remain non-cognitive. They must not autonomously form or revise Goals or Beliefs, select Dreams, generate adaptive Questions, reinterpret experience, or learn from outcomes.

Simulation/Dream execution and Agent-owned Hive creation are architecturally supported mechanisms but are not part of this slice unless a concrete V1 boundary proves they are required.

This slice must reuse the existing typed identity/resource, Result/Error, Agent/Runtime/Execution, WorkItem, and provenance contracts rather than introduce parallel representations.

## Phase 1.5 completion

Phase 1.5 — Base Agent & AgentFactory is complete and verified.

Developer verification:
- Hive.Example.WinForms `Agents / Base Agent / AgentFactory / Runtime Isolation` completed successfully.
- Full `Hive.Tests` execution: **103 tests passed, 0 failed, 0 skipped in 2.3 seconds**.
- Agent generation remained explicit at creation; two RuntimeInstance identities and their Execution state were isolated.
- The 1.5 completion gate is satisfied.

## Architecture / dependency boundary

The base protocol boundary remains below later cognitive behavior:

```text
Agent
  ├─ Objective lifecycle
  ├─ WorkItem binding / provenance
  ├─ Memory infrastructure
  ├─ Question / Answer transport
  ├─ Patience / Understanding Gate
  └─ Delegation interfaces

Later CognitiveAgent:
  └─ adaptive interpretation/revision over these mechanisms
```

Base mechanisms may provide explicit state, transport, storage, waiting, and delegation primitives. They must not autonomously become cognitive strategy.

## Verification

Required for completion of 1.6:

1. Objective create/update/complete lifecycle and invalid-transition cases;
2. WorkItem binding preserves identity and provenance and rejects invalid ownership/binding cases;
3. memory storage/retrieval preserves explicit scope/ownership and does not silently cross runtime boundaries;
4. Question/Answer transport supports ownership, waiting, timeout, completion, and invalid transitions;
5. Patience / Understanding Gate blocks consequential work until its minimum required information/confirmation is satisfied;
6. delegation preserves requester/delegate ownership and provenance;
7. multiple RuntimeInstance objects remain isolated while using these mechanisms;
8. focused automated coverage exists for normal, invalid, boundary, timeout, and isolation cases;
9. public Example Host verification demonstrates the externally usable base protocols;
10. broader `Hive.Tests` execution.

No verification claim is recorded until it has actually been performed.

## Constraints

- No 1.7 or later event log/snapshot/outbox implementation.
- No MAF execution integration.
- No CognitiveAgent implementation or adaptive cognitive behavior.
- No cognitive Goals, Beliefs, Dreams, adaptive Question generation, or learning.
- No Agent-owned Hive creation unless a concrete V1 boundary proves it is required.
- No Management settings/configuration UI.
- No provider transport changes.
- No changes to the SQL Server/DPAPI persistence boundary.
- Reuse existing Agent/Runtime/Execution, WorkItem, identity, resource, provenance, and typed-error contracts.
- Keep protocol state isolated by explicit Agent/Runtime ownership.
- Do not add a second orchestration/workflow engine.

## Verification handoff

Example to run: <exact 1.6 Example Host path once the authorized example is implemented> — Hive.Example.WinForms

Tests to run: <exact 1.6 focused test class/file once implemented>; broader Hive.Tests execution is required by the 1.6 completion gate.
