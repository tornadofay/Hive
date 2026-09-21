# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.1 — Solution & project scaffolding**

This is the current implementation slice. Do not begin 0.2 or any later slice until 0.1 is complete and its verification has actually been performed.

## Objective

Create the complete initial .NET 10 solution structure and dependency direction documented by the architecture and roadmap.

The initial solution must contain:

- `Hive.Core`;
- `Hive.Agents`;
- `Hive.Persistence`;
- `Hive.Coordination`;
- `Hive.Tools`;
- `Hive.Providers.OpenAICompatible`;
- `Hive.Management`;
- `Hive.Host.WinForms`;
- `Hive.Host.WinForms.UI`;
- `Hive.Example.WinForms`;
- `Hive.Tests`.

The projects are scaffolding only in this slice. Do not implement the V1 pipeline, Hive membership, Swarm, cognition, persistence schema, provider behavior, UI foundation, Example navigation, or test-running tools yet.

## Dependency direction

The initial project references must enforce these boundaries:

```
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → public platform contracts + Host.WinForms + Host.WinForms.UI
Tests → projects under test

Coordination may depend on Core + Agents + MAF contracts where required.
No core/platform project may depend on Example.WinForms.
```

Additional constraints:

- WinForms-specific types remain outside `Hive.Core`.
- Provider transport remains outside `Hive.Core`.
- The ReaLTaiizor package is **not introduced in 0.1**; it is added and verified in 0.6, and only `Hive.Host.WinForms.UI` may reference it.
- `Hive.Example.WinForms` must not reference xUnit runner internals.
- Do not create compatibility/legacy projects or duplicate architecture paths.

## Files / projects for this slice

Create the solution and project files required for the eleven projects above, plus the minimum root/project metadata needed for a clean build.

No feature implementation is required beyond the smallest valid project/assembly entry points.

## Verification

The developer must manually verify:

1. the complete solution restores/builds successfully with .NET 10;
2. every expected project is present in the solution;
3. forbidden project references are absent;
4. `Hive.Core` has no WinForms/provider-transport dependency;
5. `Hive.Example.WinForms` does not reference xUnit runner internals;
6. the dependency direction matches `docs/architecture.md` and `docs/roadmap.md`;
7. all eleven projects can participate in the solution build without placeholder dependency errors.

No later slice may be marked active until these checks are complete.

## Out of scope

- ReaLTaiizor package integration;
- Hive UI/theme/token implementation;
- Example navigation shell;
- `IHiveExample` implementation;
- Example test runner;
- V1 Workspace;
- V1 image/document pipeline;
- provider adapter implementation;
- persistence/database schema;
- Agent/Hive implementation;
- Hive membership/Swarm;
- CognitiveAgent/CognitiveHive;
- automated end-to-end behavior beyond the scaffolding build.

## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice:
