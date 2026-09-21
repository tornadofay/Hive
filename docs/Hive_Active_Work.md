# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.2 — Common infrastructure**

The 0.1 solution/project scaffolding is complete and has been verified locally by the developer. Do not begin 0.3 or any later slice until 0.2 is complete and its verification has actually been performed.

## Objective

Implement the common platform infrastructure shared by every later Hive capability:

- stable identifiers and ID value types;
- immutable common value objects;
- typed errors/results;
- IClock;
- durable event envelope;
- explicit event type and payload schema version;
- correlation and causation IDs;
- event payload upcasting compatibility boundary;
- one JSON serialization stack.

## 0.1 completion record

The initial .NET 10 solution scaffold was merged into main and manually verified by the developer.

- Build result: **PASS — developer reports full solution rebuild succeeds locally.**
- Runtime result: **PASS — Hive.Example.WinForms is the startup project and launches successfully with the current empty placeholder form.**
- Verification result: **PASS for the 0.1 scaffold boundary; all eleven projects are present in the solution and the solution participates in a successful full rebuild.**
- Automated tests: **None in 0.1 by design.** The real xUnit test harness is a later Phase 0.5 slice.
- Commit: 590ca87dfc1c4e1d99308394167ad29202317c69
- Next slice: **0.2 — Common infrastructure**

## Dependency direction

The initial project references enforce these boundaries:

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

- WinForms-specific types remain outside Hive.Core.
- Provider transport remains outside Hive.Core.
- The ReaLTaiizor package is **not introduced in 0.1**; it is added and verified in 0.6, and only Hive.Host.WinForms.UI may reference it.
- Hive.Example.WinForms must not reference xUnit runner internals.
- Do not create compatibility/legacy projects or duplicate architecture paths.

## Out of scope for 0.2

- identity/domain resources beyond shared ID/value infrastructure;
- persistence schema or database migrations;
- provider implementation;
- Agent/Hive behavior;
- UI foundation;
- Example navigation;
- test runner tooling;
- V1 image/document pipeline;
- CognitiveAgent/CognitiveHive;
- automated end-to-end behavior.

## 0.2 Verification

The developer must manually verify:

1. all common ID/value/error/result types compile and satisfy their invariants;
2. event envelopes preserve event type, payload schema version, correlation ID, and causation ID;
3. JSON serialization round-trips supported event payloads;
4. older supported payload versions upcast to the current contract;
5. unsupported future/incompatible payload versions are rejected cleanly;
6. IClock allows deterministic tests and application time access without direct time coupling;
7. no later project boundary or feature is pulled into the common infrastructure slice.

## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice: