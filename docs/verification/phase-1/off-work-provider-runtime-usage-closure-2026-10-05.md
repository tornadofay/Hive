# Off-Work Provider Completion — Slice 3: Runtime Token Usage Foundation — Verification Closure — 2026-10-05

## Scope

**Authorized slice:** Off-work provider completion — Slice 3: Runtime Token Usage Foundation

This slice established the provider-reported runtime token-usage evidence foundation at the existing OpenAI-compatible provider and execution boundaries. It did not activate or advance any Phase 1 roadmap slice and did not implement Phase 1.30 metrics, budgets, OpenTelemetry, quota enforcement, tokenizer estimation, native transport, billing reconciliation, cost UI, or target-selection behavior.

## Implementation verified

The verified implementation:

- captures provider-reported usage at the OpenAI-compatible provider boundary;
- normalizes provider-neutral input, output, total, cached-input, reasoning, and bounded additional token counts;
- distinguishes `Actual`, `Estimated`, and `Unknown`, with missing usage remaining unknown rather than zero;
- preserves cached-input and reasoning counts without double-counting them into input/output/total;
- carries usage through `OpenAICompatibleChatClient`, Microsoft Agent Framework, and the existing Agent execution result;
- persists immutable usage evidence inside the applicable terminal execution event;
- preserves the applicable Provider, ProviderAccount, ExecutionTarget, model/deployment, Agent, Runtime, Execution, and scope correlation identities;
- avoids fabricating usage on failure or cancellation paths;
- includes focused provider/core/execution regression coverage and the deterministic Example Host scenario.

## Verification history

The implementation required two bounded remediations before final verification:

1. nullable-flow warnings in `OpenAICompatibleProviderAdapterTests.cs` were corrected by explicit null-guarded locals for provider usage and additional token counts;
2. focused verification exposed a parser defect where a scalar numeric additional token-count property was passed to an object-only helper. The provider parser was corrected to handle scalar numeric elements directly while retaining object-property handling for standard and nested usage fields.

Both remediations remained within the authorized Slice 3 boundary.

## Final developer verification

On 2026-10-05 the developer ran the full `Hive.Tests` suite:

```
632 Tests (632 Passed, 0 Failed, 0 Skipped) run in 1.1 min
```

This full run includes the focused runtime/provider usage coverage and the `AgentExecutionIntegrationTests` usage/persistence regressions required by the slice.

The developer also manually exercised the deterministic Example Host scenario:

**Example to run:** `Providers / Runtime / Token Usage Foundation` — Hive.Example.WinForms

Observed output:

```
Execution: e3aecf5c-ba6f-4412-aa5a-a05fb69d52f4
Provider response: chatcmpl-runtime-usage
Usage evidence: Actual
Input tokens: 120
Output tokens: 45
Total tokens: 165
Cached input tokens: 20
Reasoning tokens: 10
Additional token count (accepted prediction): 3
Terminal event: b14354a1-ccb8-4693-9f2e-c56baf69deab
Durable usage evidence: yes (persisted with the terminal execution event)
Provider credentials: none
External provider call: no (loopback fixture)
Tokenizer estimation: no
Migration: Applied; schema=14
```

The scenario therefore demonstrated provider-reported Actual usage, preservation of cached/reasoning/additional dimensions, terminal-event persistence, a deterministic loopback provider boundary, and schema 14 availability.

## Verification boundary

Verified:

- full `Hive.Tests` result: 632/632 passed, 0 failed, 0 skipped;
- runtime/provider usage parsing and propagation through the existing execution path;
- missing-usage and failure/cancellation evidence boundaries through the automated suite;
- durable usage evidence in the terminal execution event through the deterministic Example Host scenario;
- usage/resource correlation identities exercised by the integration regressions;
- no live provider call from the deterministic Example Host scenario.

Not reported / not claimed:

- no separate Visual Studio/MSBuild build result was supplied by the developer.

## Roadmap boundary

This slice remains off-roadmap provider-platform completion work. It does not start, advance, or close Phase 1.20, Phase 1.30, or any other roadmap slice.

Phase 1.30 remains the future owner of metrics, budgets, OpenTelemetry, quota/rate-limit handling, reporting/aggregation, and consumption of normalized usage/cost information.

## Closure

Slice 3 is **complete and verified**.

No subsequent off-work slice is authorized by this verification record. `docs/Hive_Active_Work.md` must remain at `NO ACTIVE WORK` until a separate bounded task is explicitly authorized.
