# Hive — Active Work

Status: VERIFICATION PENDING

## Authorized slice

**Off-work provider completion — Slice 3: Runtime Token Usage Foundation**

Explicit user authorization: start the next off-work slice, Slice 3 only.

## Checkpoint

Started from verified Slice 2 closure at `main` commit `84c488060a72571300520ec8fa5282c70c4b7655`.

## Scope

Implement the provider-reported runtime token-usage foundation described by the off-work plan:

- capture provider-reported usage at the OpenAI-compatible provider boundary;
- normalize the supported usage dimensions into Hive-owned provider-neutral semantics;
- distinguish `Actual`, `Estimated`, and `Unknown` without treating missing usage as zero;
- preserve cached-input and reasoning/thinking counts without double-counting them into input/output/total;
- carry usage through the existing chat/execution boundary;
- persist immutable usage evidence using the existing execution-event persistence boundary;
- preserve the applicable execution/resource correlation identities available to the current execution path;
- keep cancellation/failure paths from fabricating successful usage records;
- add focused deterministic provider, execution, persistence, and boundary regression coverage;
- add the required deterministic Example Host scenario and owning documentation.

## Explicit exclusions

Not implemented in this slice:

- Phase 1.30 metrics, budgets, OpenTelemetry, quota/rate-limit enforcement, or reporting/aggregation UI;
- generic/local tokenizer estimation implementation;
- native provider transport implementations;
- provider billing reconciliation;
- historical cost reporting UI;
- Agent target-selection changes;
- new durable Model resource;
- unrelated UI/control cleanup;
- roadmap advancement.

A provider-reported usage value is authoritative evidence for that dimension. An unreported dimension remains unknown. Estimated usage may be represented by the contract for future bounded estimation, but this slice does not create a tokenizer or estimation engine.

## Required verification

```
Tests to run:
- focused runtime/provider usage tests;
- AgentExecutionIntegrationTests usage/persistence regressions;
- full Hive.Tests.

Example to run:
Providers / Runtime / Token Usage Foundation — Hive.Example.WinForms
```

Build result is not to be claimed unless the developer reports it separately.

## Implementation completion

The Slice 3 implementation is complete within the authorized boundary.

Implemented:
- Hive-owned `ExecutionTokenUsage` contract with Actual / Estimated / Unknown evidence states;
- provider-reported OpenAI-compatible usage parsing for input/output/total, cached input, reasoning, and bounded additional token counts;
- preservation through `OpenAICompatibleChatClient` and Microsoft Agent Framework;
- immutable usage persistence inside the existing terminal execution event;
- execution/resource correlation fields already available to the current AgentExecutionRequest;
- focused provider, contract, and execution persistence regressions;
- deterministic Example Host scenario at the required path;
- owning architecture, example, and off-work plan documentation.

No tokenizer/estimation engine, reporting subsystem, Phase 1.30 metrics/budgets/OpenTelemetry/quota behavior, native transport, or roadmap behavior was added.

## Closure gate

Do not start any future slice. After implementation, return Active Work to `VERIFICATION PENDING` and require developer verification. Close Slice 3 only after the required tests and Example Host scenario are actually verified.
