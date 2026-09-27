# Hive — Active Work

Status: OPEN — IMPLEMENTATION

Slice: Maintenance — Review Finding Corrections (Five Remaining Production Findings)

Opened: 2026-09-27
Checkpoint: main @ bee92782a6aedd44448b5e786e7ade8068318afe

## Authorized scope

Correct the five concrete findings from the immediately preceding Workflow Review, without advancing the roadmap or adding new capability:

1. Agent execution lifecycle: handle failure/cancellation while persisting the initial `agent.execution.started` event so the execution cannot be left as an unrepresented in-memory Running execution.
2. Host composition replacement lifecycle: make replacement/disposal failure behavior deterministic and preserve graph ownership/status invariants, including cleanup of the reconfiguration synchronization resource.
3. Database migration cancellation: remove the `Task.Run` masking pattern and provide the existing synchronous DbUp work with a bounded cancellation-aware execution boundary without claiming cancellation can interrupt DbUp itself.
4. DPAPI bootstrap credential file replacement: make concurrent resolution/replacement safe on Windows without exposing plaintext credential material.
5. WorkItem listing scalability: add bounded persistence-side paging while preserving the existing public Management/WorkItem semantics and deterministic ordering.

## Required engineering depth

Include focused regression coverage for each finding and any directly necessary supporting lifecycle, cancellation, concurrency, persistence, or resource-ownership behavior. Preserve existing public contracts unless a narrowly scoped compatibility-preserving pagination API is required; no roadmap phase work or unrelated refactoring.

Example impact: no new externally usable capability is introduced, so no new Example Host scenario is required.

## Verification gate

Status after implementation: VERIFICATION PENDING.

Developer must run:
- exact focused regression tests added/updated for all five findings;
- full `Hive.Tests` suite.

Do not start further implementation-affecting changes until actual developer verification results are recorded here as either passing or `VERIFICATION FAILED / REMEDIATION REQUIRED`.
