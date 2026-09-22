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
- cancellation-aware HTTP execution with bounded request timeout;
- fake-server integration coverage without real vendor credentials;
- matching public `Hive.Example.WinForms` scenario.

This slice consumes the already-established Provider / ProviderAccount / ExecutionTarget and Secret Store contracts where useful, but it does not add ProviderAccount credential wiring, execution-target selection/planning, Management settings UI, or agent execution.

## 1.3 implementation scope

- Implement the production OpenAI-compatible transport adapter inside `Hive.Providers.OpenAICompatible`.
- Define the smallest public adapter/request/response/error contract needed by callers.
- Use an injected/configurable `HttpClient` boundary so transport behavior is testable and replaceable.
- Accept endpoint/base URL and credential material without persisting or logging the credential.
- Support the standard OpenAI-compatible chat-completions request path for a selected model/deployment and optional system/user messages.
- Support an optional JSON structured-output request contract and return parsed structured JSON only when the provider response is valid.
- Normalize compatible provider HTTP/transport failures into Hive typed `ErrorCategory` results:
  - authentication/authorization → `Unauthorized`;
  - rate limit → `External` with stable provider-rate-limit error code;
  - timeout → `Timeout`;
  - cancellation → propagate `OperationCanceledException`;
  - transport/network failure → `External`;
  - malformed response or invalid structured output → `Serialization`.
- Preserve provider response details only where safe; never expose authorization headers, API keys, or secret material in errors or diagnostics.
- Keep the adapter vendor-neutral: provider-specific credentials and endpoints remain configuration; do not add separate Groq/OpenRouter/Cloudflare/Cerebras/NVIDIA/Google transport implementations.
- Add focused contract tests and local fake-server integration tests covering success, malformed response, timeout, cancellation, authentication failure, rate limit, transport failure, and structured-output failure.
- Add the mandatory Example Host public scenario using a local fake HTTP endpoint; do not require a real provider account.
- Add short `docs/examples/Phase13_OpenAI_Compatible_Provider_Adapter.md` usage documentation.
- Do not implement 1.4 capability-aware target selection, 1.9 Agent execution, ProviderAccount secret-reference wiring, Management configuration, Workspace, or later slices.

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

The adapter must remain independently usable and replaceable. Later Management/execution code may resolve a ProviderAccount + SecretReference and pass resolved `SecretMaterial` into the transport boundary; that wiring is intentionally outside 1.3.

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
- Do not add a dependency merely to implement straightforward HTTP/JSON transport if the .NET 10 framework capabilities are sufficient.

## Verification handoff

Example to run: Providers / Provider Transport / OpenAI-compatible Provider Adapter — Hive.Example.WinForms (net10.0-windows).

Tests to run: tests/Hive.Tests/OpenAICompatibleProviderAdapterTests.cs; broader Hive.Tests execution is required by the 1.3 completion gate.
