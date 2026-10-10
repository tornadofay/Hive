# Hive — Active Work

Status: VERIFICATION PENDING — Phase 1.20 / Slice 1 — Direct LLM Conversation Path

## Authorization
Explicitly authorized by `Hive: Start Phase 1.20` on 2026-10-10. The owning plan is [Phase 1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md).

## Latest remediation
Corrected the two developer-reported compile/analyzer failures:
- Replaced the `Assert.True(string.EndsWith(...))` check with xUnit's `Assert.EndsWith`.
- Replaced the unavailable `AccessibleRole.Heading` with the supported `AccessibleRole.StaticText`.

These source corrections have not been compiled or test-run here. This returns Slice 1 to **VERIFICATION PENDING**; it does not establish successful verification. Do not add streaming, Agent mode, or other new features under this remediation.

## Existing Slice 1 scope
Direct LLM conversations in Workspace with an explicitly selected eligible ExecutionTarget; Management-owned provider/capability/credential resolution; durable shared event/snapshot state for SQL Server and Embedded; ownership, cancellation, safe failure and recovery handling; focused tests and Example Host scenario. See [Phase 1.20 plan](plan/Phase1/1.20.md) and the direct-conversation boundary in [architecture](architecture/v1-host-and-management.md).

## Verification boundary
This status remains **VERIFICATION PENDING** until developer verification is reported. The developer must rebuild the affected projects (the repository already sets Treat Warnings as Errors), rerun `DirectLlmConversationTests.cs` and `HiveWorkspaceLifecycleTests.cs`, run all `Hive.Tests`, and exercise the Example Host scenario. No new slice may start before those results pass and Slice 1 is explicitly closed.

## Required handoff
- Example to run: **Workspace / Direct LLM / Direct LLM Conversation — Hive.Example.WinForms**
- Tests to run: **DirectLlmConversationTests.cs**, **HiveWorkspaceLifecycleTests.cs**, then the broader **Hive.Tests** suite.
