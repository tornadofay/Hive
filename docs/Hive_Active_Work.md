# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.3 — OpenAI-compatible Provider Adapter**

Phase 0 — Foundations, Phase 1.1 — Provider / ProviderAccount / ExecutionTarget, and Phase 1.2 — Secret Store are complete and verified.

Do not introduce 1.4 or later Phase 1 slices until 1.3 is complete.

## Objective

Establish one shared OpenAI-compatible provider transport boundary for compatible hosted and local targets:

- one provider adapter parametrized by endpoint and credentials;
- public request/response contracts independent of any vendor-specific SDK;
- structured output support without provider-specific transport implementations;
- deterministic typed mapping for authentication failure, rate limiting, timeout, cancellation, transport failure, malformed responses, and invalid structured output;
- cancellation-aware HTTP execution with a bounded request/response timeout;
- local fake-server integration coverage without real vendor credentials;
- matching public Hive.Example.WinForms scenario.

This slice consumes the established Provider / ProviderAccount / ExecutionTarget and Secret Store contracts where useful, but it does not add ProviderAccount credential wiring, execution-target selection/planning, Management settings UI, or agent execution.

## Implementation checkpoint

The 1.3 implementation is present in the repository at this checkpoint:

- `Hive.Providers.OpenAICompatible` now exposes the OpenAI-compatible request/message/structured-output/options contracts and `OpenAICompatibleProviderAdapter`.
- The adapter sends OpenAI-compatible `POST <base-uri>/chat/completions` requests with optional Bearer credentials supplied as `SecretMaterial`.
- Structured output uses the OpenAI-compatible JSON-schema response format and returns cloned `JsonElement` content only after successful JSON parsing.
- Timeout applies across both response-header and response-body reads; caller cancellation remains an `OperationCanceledException`.
- HTTP authentication, rate-limit, timeout, transport, and malformed-response failures map to typed Hive errors without including credential material.
- `Hive.Tests/OpenAICompatibleProviderAdapterTests.cs` contains local loopback fake-server coverage for the required transport/error/structured-output cases plus contract boundary validation.
- `Hive.Example.WinForms` contains `Providers / Provider Transport / OpenAI-compatible Provider Adapter`, which exercises the public adapter against a local loopback endpoint.
- `docs/examples/Phase13_OpenAI_Compatible_Provider_Adapter.md` documents the public API.
- `docs/ui/examples.md` records the new Example tree branch.

Agent-run build/tests/manual verification are not authorized. The implementation therefore remains pending the developer verification gate below.

## Architecture / dependency boundary

```text
Hive.Core
   ↑
Hive.Providers.OpenAICompatible
   ↑
future execution/planning / Agents / Management consumers

Hive.Persistence
   └─ owns Secret Store persistence

OpenAI-compatible adapter
   └─ receives already-resolved credential material
      and never persists it

Example.WinForms
   └─ exercises the public adapter contract against a local fake endpoint
```

The adapter remains independently usable and vendor-neutral. Later Management/execution code may resolve a ProviderAccount + SecretReference and pass resolved `SecretMaterial` into the transport boundary; that wiring is outside 1.3.

## Verification

Required for completion of 1.3:

1. public adapter contract normal/invalid/boundary tests;
2. local fake-server success path;
3. malformed response mapping;
4. timeout and cancellation behavior;
5. authentication failure mapping;
6. rate-limit mapping;
7. transport/network failure mapping;
8. structured-output success and malformed structured-output failure;
9. credential redaction/no secret leakage in errors or Example output;
10. public Example Host verification;
11. broader `Hive.Tests` execution;
12. manual Example verification of the local fake-server provider scenario.

No verification claim is recorded until it has actually been performed.

## Constraints

- No real vendor credentials or uncontrolled external provider calls.
- No provider-specific transport classes.
- No ProviderAccount credential persistence/wiring.
- No capability-aware selection/planning.
- No Agent/MAF execution integration.
- No Management settings/configuration UI.
- No changes to the SQL Server/DPAPI persistence boundary.
- Preserve existing Core dependency direction and public provider-resource contracts.
- Do not add a dependency merely to implement straightforward HTTP/JSON transport because the .NET 10 framework capabilities are sufficient.

## Verification handoff

Example to run: Providers / Provider Transport / OpenAI-compatible Provider Adapter — Hive.Example.WinForms (net10.0-windows).

Tests to run: tests/Hive.Tests/OpenAICompatibleProviderAdapterTests.cs; broader Hive.Tests execution is required by the 1.3 completion gate.
