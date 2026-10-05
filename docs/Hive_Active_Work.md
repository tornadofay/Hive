# Hive — Active Work

Status: VERIFICATION PENDING

## Revision — Off-Work Provider Completion Integration & Hardening

Authorized by the user's explicit request on 2026-10-05 to fix all findings from the Slice 4 Revision audit.

### Authorized corrective boundary

Correct only the concrete Slice 4 findings:

1. Bind execution pricing evidence to its complete execution-visible Provider → ProviderAccount → endpoint → model source identity so evidence cannot be attached to a different execution context. Management discovery lookup continues to use Provider/ProviderAccount resource versions and discovery generation as part of cache identity.
2. Preserve provider-reported model identity separately from the configured target model/deployment in execution results and usage evidence so provider-resolved aliases/deployments are not discarded.
3. Enforce the existing OpenAI-compatible execution boundary in configured Management execution so native/different-transport Providers cannot be routed through the OpenAI-compatible adapter.

Required focused regression tests and owning architecture/example documentation updates are in scope.

### Explicit exclusions

- no new provider transports;
- no new pricing, billing, tokenizer, estimation, metrics, budgets, quota, OpenTelemetry, or reporting capability;
- no Agent target-selection redesign;
- no durable Model resource;
- no Phase 1.30 or later roadmap work;
- no unrelated UI or cleanup work.

### Verification gate

A developer compile failure was recorded and remediated within the same Slice 4 Revision boundary. The reported `CS1061` in `src/Hive.Coordination/AgentExecutionService.cs` line 205 was caused by reading `ModelId` directly from MAF `AgentResponse`, which does not expose that property. The correction now reads the provider-reported model from the underlying `Microsoft.Extensions.AI.ChatResponse` preserved in `AgentResponse.RawRepresentation`, then applies the existing bounded normalization. Developer re-verification is still required.

Required handoff after changes:

Example to run: Providers / Runtime / Provider Completion Integration & Hardening — Hive.Example.WinForms
Tests to run: full Hive.Tests suite; focused ProviderCompletionIntegrationTests and relevant configured Agent execution regressions.

No roadmap phase has been activated.
