# Hive — Active Work

Status: VERIFICATION PENDING — Phase 1.20 / Slice 1 — Direct LLM Conversation Path

## Authorization
Explicitly authorized by `Hive: Start Phase 1.20` on 2026-10-10. The owning plan is [Phase 1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md).

## Objective
Deliver the first end-to-end Phase 1.20 capability: direct LLM conversation inside the V1 Workspace, with explicit ExecutionTarget selection and a Management-owned execution boundary. This is direct LLM mode, not Agent mode.

## Implemented scope awaiting local verification
- Public Management contracts and implementation to create, list, read, and send messages in conversations.
- Exact active ExecutionTarget resolution, Provider/ProviderAccount consistency checks, Secret Store access, and required effective text-generation capability policy; no target substitution or fallback.
- Conversation messages, execution outcomes, correlation/status, and history persisted through the shared event/snapshot infrastructure for SQL Server and Embedded.
- Owner/Deployment/Tenant/optional Workspace checks, optimistic stream versioning, concurrent-request rejection, cancellation, timeout/failure handling, and abandoned-running-request reconciliation.
- A Direct LLM Workspace view alongside the existing Work Items mode, using Management APIs only.
- Example Host scenario and focused tests for Embedded restart durability, SQL Server persistence, explicit target/model use, owner isolation, target capability rejection, missing-target validation, and Workspace mode/startup behavior.
- The Workspace architecture section now documents the direct conversation boundary and capability policy.

These changes are source-present on `main` but are **not verified** until the developer completes the gates below. No local build or test result is claimed.

## Explicit exclusions
Phase 1.21 Agent-directed Workspace interaction, Agent selection/configuration (including Auto/Favorites), application/form-associated Agents, Hive/Swarm membership, governed Tools/intervention, consequential host-business writes, remaining Phase 1.20 modes/runtime visibility, later V1 pipeline phases, vector retrieval, and unrelated cleanup.

## Verification boundary — required before any next slice
1. In Visual Studio, build the affected projects/solution with Treat Warnings as Errors enabled. The change touches `Hive.Core`, `Hive.Coordination`, `Hive.Management`, `Hive.Host.WinForms`, `Hive.Example.WinForms`, and `Hive.Tests`; include their project dependencies.
2. Run focused tests: `DirectLlmConversationTests.cs` and `HiveWorkspaceLifecycleTests.cs`. The direct-conversation tests exercise both Embedded and SQL Server persistence, so run them in the same configured environment as the existing SQL Server tests.
3. Run the entire `Hive.Tests` suite and report passed/failed/skipped counts.
4. Manually exercise the Example Host scenario below using an active OpenAI-compatible target whose effective text-generation capability is Supported. Verify explicit target/model selection, send/response, refresh and history reload, and application restart durability. Confirm unsupported/unknown text capability is rejected rather than falling back to another target.

Do not start another implementation slice until those results are reported, any failures are corrected within this slice, and this file is updated to close Slice 1.

## Required handoff
- Example to run: **Workspace / Direct LLM / Direct LLM Conversation — Hive.Example.WinForms**
- Tests to run: **DirectLlmConversationTests.cs**, **HiveWorkspaceLifecycleTests.cs**, then the broader **Hive.Tests** suite.
