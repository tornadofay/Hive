# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED — Phase 1.20 / Slice 1 — Direct LLM Conversation Path

## Authorization
Explicitly authorized by `Hive: Start Phase 1.20` on 2026-10-10. The owning plan is [Phase 1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md).

## Remediation boundary
Developer-reported compile/analyzer failures in Slice 1:
- `Hive.Tests/DirectLlmConversationTests.cs:135` — xUnit2009: use `Assert.EndsWith` instead of `Assert.True(string.EndsWith(...))`.
- `Hive.Host.WinForms/HiveDirectLlmConversationView.cs:646` — CS1061: `AccessibleRole.Heading` does not exist.

Authorized remediation is limited to correcting these errors, reviewing the exact resulting changes, and restoring the prior verification gate. Do not add streaming, Agent mode, or other new features under this remediation.

## Existing Slice 1 scope
Direct LLM conversations in Workspace with an explicitly selected eligible ExecutionTarget; Management-owned provider/capability/credential resolution; durable shared event/snapshot state for SQL Server and Embedded; ownership, cancellation, safe failure and recovery handling; focused tests and Example Host scenario. See [Phase 1.20 plan](plan/Phase1/1.20.md) and the direct-conversation boundary in [architecture](architecture/v1-host-and-management.md).

## Verification boundary
After the two corrections, return this status to **VERIFICATION PENDING**. The developer must rebuild the affected projects with the repository's Treat Warnings as Errors setting, rerun `DirectLlmConversationTests.cs` and `HiveWorkspaceLifecycleTests.cs`, run all `Hive.Tests`, and exercise the Example Host scenario. No new slice may start before those results pass and Slice 1 is explicitly closed.

## Required handoff
- Example to run: **Workspace / Direct LLM / Direct LLM Conversation — Hive.Example.WinForms**
- Tests to run: **DirectLlmConversationTests.cs**, **HiveWorkspaceLifecycleTests.cs**, then the broader **Hive.Tests** suite.
