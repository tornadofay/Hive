# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.5 — Base Agent & AgentFactory**

Phase 0 — Foundations and Phase 1.1 through Phase 1.4 are complete and verified.

Do not introduce 1.6 or later Phase 1 slices until 1.5 is complete.

## Objective

Implement the base Agent creation/runtime boundary and factory without pulling in cognitive-generation behavior:

- stable Agent base contract;
- AgentDefinition;
- isolated RuntimeInstance;
- Execution;
- AgentFactory.Create<TAgent>();
- explicit generation selection at creation;
- authorization/policy boundary for generation creation;
- no runtime promotion or demotion;
- multiple runtimes created from one definition remain isolated;
- factory design must not require CognitiveAgent types or cognitive-state mechanisms.

This slice must build on the existing identity, resource, error/result, provider, selection, persistence, and MAF-oriented architecture rather than introduce parallel representations.

## Phase 1.4 completion

Phase 1.4 — Capability-aware Execution Target Selection is complete and verified.

Developer verification:
- Hive.Example.WinForms `Providers / Target Selection / Capability-aware Execution Target Selection` completed successfully.
- Full `Hive.Tests` execution: **95 tests passed, 0 failed, 0 skipped in 2.9 seconds**.
- The initial xUnit `Assert.Single(...Where(...))` analyzer errors were corrected before the successful rerun.
- The 1.4 completion gate is satisfied.

## Architecture / dependency boundary

The base Agent boundary remains separate from later CognitiveAgent functionality:

```text
AgentDefinition
      ↓
AgentFactory.Create<TAgent>()
      ↓
Agent
      ├─ RuntimeInstance A
      │    └─ Execution
      └─ RuntimeInstance B
           └─ Execution

Later:
CognitiveAgent : Agent
```

Generation is selected explicitly at creation and is immutable for the Agent instance. AgentFactory must not inspect task complexity and silently create or promote a CognitiveAgent.

## Verification

Required for completion of 1.5:

1. normal AgentDefinition and AgentFactory creation;
2. multiple RuntimeInstance objects from one definition are isolated;
3. Execution identity/lifecycle is distinct from runtime identity;
4. explicit generation selection is preserved;
5. unauthorized generation creation is rejected;
6. factory does not require cognitive types;
7. malformed definitions/invalid creation inputs fail closed with typed errors;
8. focused automated coverage exists for normal, invalid, authorization, and isolation cases;
9. broader `Hive.Tests` execution;
10. public Example Host verification for the externally usable Agent/factory capability.

No verification claim is recorded until it has actually been performed.

## Constraints

- No 1.6 or later Base Agent Work Protocols.
- No CognitiveAgent implementation or runtime promotion/demotion.
- No cognitive Goals, Beliefs, Dreams, adaptive Questions, or learning behavior.
- No MAF execution integration unless a concrete 1.5 contract requires a MAF-owned mechanism.
- No Management settings/configuration UI.
- No provider transport changes.
- No changes to the SQL Server/DPAPI persistence boundary.
- Preserve existing identity/resource contracts and explicit generation boundaries.
- Keep runtime/execution state isolated between RuntimeInstance objects.
- Do not add a second orchestration engine.


## Implementation checkpoint

The 1.5 implementation is present in the repository at this checkpoint:

- `Hive.Agents` exposes `AgentDefinition`, `Agent`, `RuntimeInstance`, `Execution`, `IAgentCreationAuthorizer`, and `AgentFactory.Create<TAgent>()` for the base generation.
- Agent generation is explicitly stored in `AgentDefinition` and copied to the created Agent/runtime/execution objects; the factory does not infer or promote generations.
- Creation requires deployment and principal identity and passes through the injected authorization boundary.
- The current factory supports `AgentGeneration.Base`; the later `Cognitive` generation is rejected as unsupported rather than silently promoted.
- Runtime instances receive distinct identities and execution state is isolated per runtime.
- Runtime/Execution lifecycle transitions return typed failures for invalid transitions.
- `tests/Hive.Tests/AgentFactoryTests.cs` covers normal creation, malformed identity/definition, authorization denial, generation rejection, runtime isolation, stopped-runtime behavior, and terminal execution transitions.
- `Hive.Example.WinForms` contains `Agents / Base Agent / AgentFactory / Runtime Isolation`.
- `docs/examples/Phase15_Base_Agent_AgentFactory.md` documents the public API and slice boundary.
- `docs/architecture.md` records the Base Agent creation/runtime contract.

Developer verification is pending. Agent-run builds/tests/manual verification remain unauthorized.

## Verification handoff

Example to run: Agents / Base Agent / AgentFactory / Runtime Isolation — Hive.Example.WinForms (net10.0-windows).

Tests to run: tests/Hive.Tests/AgentFactoryTests.cs; broader Hive.Tests execution is required by the 1.5 completion gate.
