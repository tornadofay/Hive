# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.11 — V1 Workspace & WorkItem Operations**

Phase 0 — Foundations and Phase 1.1 through Phase 1.10 are complete and verified.

Phase 1.11 is the authorized active implementation slice.

## Objective

Implement the V1 operational Workspace over Hive.Management for image submission and governed business-app processing.

## Scope

- submit/attach an image to a WorkItem;
- view WorkItem status and activity;
- view relevant execution/provider status;
- receive WorkItem notifications;
- view PendingApproval;
- Approve / Reject the governed business-app write.

The V1 Workspace works with a single Agent and does not require Hive membership or Swarm state.

## Verification gate

Required for completion of 1.11:

1. image submission creates the correct WorkItem;
2. WorkItem status and activity are visible through the Workspace;
3. relevant execution/provider status is visible;
4. PendingApproval state is visible;
5. Approve / Reject changes the authoritative WorkItem state correctly;
6. stale approval is rejected;
7. the Workspace does not create hidden Hive/Swarm behavior;
8. focused automated coverage exists for normal, invalid, authorization/stale, persistence, and approval paths;
9. a public Example Host scenario demonstrates the externally meaningful Workspace behavior;
10. broader `Hive.Tests` execution.

## Constraints

- No Phase 1.12 or later implementation.
- No Hive membership, Swarm, multi-agent coordination, cognitive-generation, or configuration-portability work.
- Preserve Hive.Management as the application-facing boundary.
- Host business state remains host-owned.
- Do not move SQL/database access into the Workspace UI layer.
- Do not make image extraction or provider transport responsibilities part of Workspace.
- Preserve WorkItem identity, lifecycle, provenance, authorization, cancellation, and concurrency semantics.
- Do not introduce hidden Hive/Swarm behavior or require Hive membership for the V1 single-Agent Workspace.

## Implementation checkpoint

Phase 1.11 implementation has not started.

Before coding, inspect:

- existing WorkItem contracts and lifecycle/provenance rules;
- Hive.Management public boundary;
- current Agent execution and lifecycle persistence;
- approval/state-transition ownership;
- image/input contracts already present;
- Host.WinForms and Host.WinForms.UI boundaries;
- Example Host patterns and `docs/ui/examples.md`;
- existing WorkItem/execution/event/approval tests and fixtures.

## Verification handoff

No verification handoff yet; implementation has not started.

## Historical verification

Phase 1.10 completion is recorded in `docs/verification/phase-1/1.10.md`.
