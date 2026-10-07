# Hive — Active Work

Status: ACTIVE

## Phase
1.18 — Durable Base-Agent Work State

## Authorization
Explicit user authorization: Hive: Start Phase 1.18

## Repository checkpoint
Authorized from main commit 8224a74845d8789b297e5850c21f64811dfd98db.

## Authorized scope
- Make the existing Base-Agent Objective state durable across runtime lifetimes.
- Make existing WorkItem binding/version/provenance durable where the existing work mechanisms carry that binding.
- Make existing Question/Answer state durable, including terminal transitions, deterministic expiry, cancellation-aware waiting, and runtime restart recovery.
- Make base-Agent memory/work-state durable with deterministic retrieval and explicit actual/simulated evidence classification.
- Make existing delegation request state durable where required by the phase, without adding scheduling/orchestration.
- Add the minimal runtime-work persistence composition needed to attach a new runtime incarnation to an existing RuntimeId.
- Preserve ownership, access scope, provenance, resource version, optimistic concurrency, lifecycle, cancellation, and cross-runtime isolation.
- Add focused automated coverage for persistence/restart/concurrency/cancellation/recovery semantics and the required Example Host scenario.
- Add the required SQL migration using the established Hive.Persistence event/state/outbox architecture.

## Explicit non-goals
- No CognitiveAgent beliefs, goals, learning, Dream, Risk/Fear/Confidence, or other cognitive semantics.
- No vector storage/retrieval implementation (Phase 1.19).
- No new orchestration/workflow engine or delegation scheduler.
- No unrelated UI or provider changes.
- No host business-data persistence.

## Verification boundary
Required developer verification after implementation:
- Focused Phase 1.18 tests covering SQL durable state contracts, restart/recovery, ownership/scope isolation, optimistic concurrency/stale state, Question terminal transitions/expiry/cancellation, deterministic memory retrieval, delegation durability, and cross-runtime isolation.
- Broader Hive.Tests run.
- Example Host scenario for Persistence / Agent Work State.
- Manual Example Host verification as applicable.

Status remains ACTIVE until implementation is complete; then transition to VERIFICATION PENDING with exact rerun targets. No future roadmap slice is authorized.
