# Hive.Persistence Production Audit Revision — Verification

Date: 2026-09-25

## Scope

Temporary production audit revision for the existing `Hive.Persistence` backend implementation. Phase 1.14 remained inactive and no later roadmap slice was implemented or authorized.

## Verified change

- `SqlAgentDefinitionResourceStore.DeleteAgentDefinitionAsync` now preserves `ConfiguredExecutionTargetId` when constructing the retired `AgentDefinition` returned to the caller, matching the durable row and preserving the complete resource state across the lifecycle transition.

## Regression coverage

- `HiveManagementFacadeTests.RetiringConfiguredAgentDefinition_PreservesExecutionTargetReference` verifies that:
  - an AgentDefinition created with a configured ExecutionTarget retains that target reference when retired through Hive.Management;
  - a subsequent reload preserves the same configured ExecutionTarget reference;
  - the returned and reloaded lifecycle state and version match.

## Verification performed

The developer ran:

```
dotnet test tests/Hive.Tests/Hive.Tests.csproj
```

Result:

```
========== Starting test run ==========
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v3.1.5+1b188a7b0a (64-bit .NET 10.0.1)
[xUnit.net 00:00:00.30]   Starting: Hive.Tests
[xUnit.net 00:00:25.76]   Finished: Hive.Tests
========== Test run finished: 229 Tests (229 Passed, 0 Failed, 0 Skipped) run in 25.8 sec ==========
```

Environment:
- .NET 10.0.1
- xUnit.net VSTest Adapter 3.1.5+1b188a7b0a
- 229 passed
- 0 failed
- 0 skipped
- 25.8 seconds

## Scope audit

- No SQL schema or migration changes.
- No provider transport, orchestration, MAF, host/UI, dependency, or unrelated refactor changes.
- No public API redesign.
- Phase 1.14 remains inactive.

## Closure

This temporary Hive.Persistence production audit revision is developer-verified and closed.
