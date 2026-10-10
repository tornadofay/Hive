# Hive — Active Work

Status: ACTIVE — Phase 1.20 / Slice 1 — Direct LLM Conversation Path

## Authorization
Explicitly authorized by `Hive: Start Phase 1.20` on 2026-10-10. The owning plan is [Phase 1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md).

## Objective
Begin the first end-to-end Phase 1.20 capability: direct LLM conversation inside the V1 Workspace, with explicit ExecutionTarget selection and a Management-owned execution boundary. This is direct LLM mode, not Agent mode.

## Authorized scope
- Add the public Management contracts and implementation needed to create/read conversation history and submit a user message to one explicitly selected, active ExecutionTarget.
- Resolve target, Provider, ProviderAccount, and credential through the existing Management/Secret Store boundaries. Preserve exact target identity; do not fall back to another target.
- Reuse the shared OpenAI-compatible provider transport and existing generic durable event/snapshot infrastructure; preserve SQL Server and Embedded parity through shared contracts.
- Persist conversation and execution state so history can be restored after the view/process is reopened; preserve ownership, scope, concurrency, cancellation, and safe failure reporting.
- Integrate a focused direct-LLM interaction surface into the existing Workspace without regressing WorkItem visibility/operations.
- Add focused Hive.Tests coverage and the matching Example Host scenario.

## Explicit exclusions
Phase 1.21 Agent-directed Workspace interaction, Agent selection/configuration (including Auto/Favorites), application/form-associated Agents, Hive/Swarm membership, governed Tools/intervention, consequential host-business writes, later V1 pipeline phases, vector retrieval, unrelated UI cleanup, and unrelated documentation cleanup.

## Verification boundary
Implementation changes are not considered verified until the developer reports the affected projects built with Visual Studio Treat Warnings as Errors enabled, focused tests and the broader `Hive.Tests` suite passed, and the direct-LLM Example Host scenario was manually exercised. Do not start the next implementation slice before this slice is verified and Active Work is explicitly updated.

Required handoff:
- Example to run: Workspace / Direct LLM / Direct LLM Conversation — Hive.Example.WinForms
- Tests to run: DirectLlmConversationTests.cs; HiveWorkspaceLifecycleTests.cs; broader-suite requirement: all Hive.Tests tests.
