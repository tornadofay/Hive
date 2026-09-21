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

## Implementation progress

The 0.2 implementation set is committed to `Hive.Core`:

- common technical IDs: `EventId`, `CorrelationId`, `CausationId`;
- typed `Error`, `Result`, and `Result<T>`;
- `IClock` and `SystemClock`;
- `EventType`, `EventPayloadVersion`, and `EventEnvelope`;
- `IEventUpcaster`, `EventUpcasterRegistry`, and sequential version upcasting;
- System.Text.Json event serialization with stable converters for common event values;
- typed `EventSerializationException` for malformed/future/incompatible event payloads.

The `Result` implementation was corrected after the initial compilation failure: the invalid record-constructor-body syntax was replaced with an explicit constructor while preserving the public contract.

The `Hive.Tests` project now contains focused 0.2 contract tests for:

- identifier validation and round-tripping;
- Error / Result invariants;
- deterministic IClock usage;
- EventEnvelope invariants;
- JSON envelope/payload round-tripping;
- malformed and future schema rejection;
- sequential event upcasting;
- duplicate, missing, and non-sequential upcaster handling.

Current automated-test execution remains pending. No test-pass claim is recorded yet.

## 0.1 completion record

The initial .NET 10 solution scaffold was merged into main and manually verified by the developer.

- Build result: **PASS — developer reports full solution rebuild succeeds locally after pulling the current main branch.**
- Runtime result: **PASS — developer reports the solution runs successfully after the rebuild.**
- Verification result: **PASS for the 0.1 scaffold boundary; all eleven projects are present in the solution and the solution participates in a successful full rebuild.**
- Automated tests: **Not executed for 0.1 by design.**
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
- provider/database/fake-host test infrastructure belonging to later slices;
- V1 image/document pipeline;
- CognitiveAgent/CognitiveHive;
- automated end-to-end behavior.

## 0.2 Verification

The current verification record is:

1. **Developer-reported full-solution rebuild: PASS.**
2. **Developer-reported application launch: PASS.**
3. 0.2 contract-test suite has been added to `Hive.Tests`.
4. Automated execution of the 0.2 suite has **not yet been performed here**, so 0.2 is not marked complete.
5. The required 0.2 checks cover:
   - common ID/value/error/result invariants;
   - event envelope preservation;
   - JSON serialization round-trip;
   - supported older-event payload upcasting;
   - rejection of unsupported future/incompatible payload versions;
   - deterministic clock injection.

## Completion record

Complete this section after verification:

- Build result:
- Verification result:
- Commit:
- Next slice:
