# Off-Work Provider Completion — Slice 4: Provider Completion Integration & Hardening

> **Status:** Slice 4 baseline and Revision verified on 2026-10-05  
> **Roadmap impact:** None  
> **Authorized:** 2026-10-05 after verified Slice 3 closure  

## Purpose

Slice 4 is the final bounded off-work provider-completion slice. It integrates and hardens the already-verified pricing normalization, built-in provider catalog, and runtime token-usage foundations.

This is off-roadmap completion work. It does not start, advance, or implement Phase 1.30.

## Scope

### 1. Existing execution-boundary integration

- connect normalized pricing evidence and runtime usage through the existing provider/execution path;
- preserve the existing Provider → ProviderAccount → ExecutionTarget ownership and execution/resource identities;
- prevent a second execution, accounting, or provider-selection pipeline;
- preserve immutable terminal execution evidence and payload versioning.

### 2. Pricing applicability handoff

- establish a deterministic representation of the pricing evidence/applicability available at execution time;
- never silently assume a missing currency, quantity, or pricing variant;
- never substitute current provider pricing for historical execution evidence;
- keep cost calculation itself outside this slice unless required as a minimal boundary contract for Phase 1.30 handoff;
- preserve uncertainty when usage or pricing cannot be safely qualified.

### 3. Cross-provider hardening

- exercise the completed built-in catalog against the existing discovery/pricing routing boundary;
- cover representative OpenAI-compatible provider profiles, special discovery profiles, and native-integration-required providers;
- ensure native providers remain outside the OpenAI-compatible adapter;
- ensure provider-specific endpoint/auth/discovery classifications are honored without invented universal endpoints.

### 4. Failure, cancellation, and security hardening

- malformed provider usage cannot corrupt execution outcome handling;
- missing usage remains Unknown, never synthetic zero;
- provider transport/auth/failure paths do not fabricate successful usage or pricing evidence;
- cancellation does not persist fabricated successful accounting evidence;
- unexpected provider data remains bounded and does not leak credentials/secrets;
- persistence failures preserve existing execution lifecycle/error boundaries.

### 5. Verification and Example Host

- add focused deterministic integration and cross-provider regressions;
- add or update the required Example Host scenario;
- keep examples on public contracts and deterministic loopback fixtures;
- document the final provider-platform boundary and the exact Phase 1.30 handoff.

## Explicit exclusions

- Phase 1.30 metrics, budgets, OpenTelemetry, quota/rate-limit enforcement, reporting, or aggregation UI;
- tokenizer or generic estimation engine;
- provider billing/reconciliation APIs;
- account-level billing adjustments such as credits, discounts, taxes, or plan allowances;
- new native provider transports;
- Agent target-selection redesign;
- new durable Model resource;
- unrelated UI/control cleanup;
- roadmap advancement.

## Revision — 2026-10-05

The Revision audit identified and corrected three concrete production hardening findings within the Slice 4 boundary:

1. ExecutionPricingEvidence now retains Provider, ProviderAccount, and exact endpoint provenance, and AgentExecutionRequest validates those identities in addition to the model/deployment identity.
2. Configured execution now preserves the provider-reported model identity separately from the configured target model/deployment in AgentExecutionResult and usage-bearing terminal evidence.
3. Configured Management execution now rejects built-in native-integration providers and any Provider transport other than openai-compatible before the OpenAI-compatible adapter is invoked.

Focused regressions cover pricing provenance mismatches, provider-reported model preservation, and native/non-compatible transport rejection.

Developer verification of this Revision completed on 2026-10-05. Full `Hive.Tests` passed 638/638 (0 failed, 0 skipped), and the required Example Host scenario was manually verified successfully. The final Revision verification record is `docs/verification/phase-1/off-work-provider-completion-integration-hardening-revision-closure-2026-10-05.md`.

## Prior Slice 4 Verification

Developer verification completed on 2026-10-05.

Full `Hive.Tests`: **636/636 passed, 0 failed, 0 skipped**.

Exact Example Host scenario: `Providers / Runtime / Provider Completion Integration & Hardening` — Hive.Example.WinForms.

The scenario verified one discovery call before execution and one after execution, fresh cached pricing attached to `provider-completion-model`, 0.35 USD / 1M input and 1.50 USD / 1M output pricing, one preserved pricing variant, provider-reported Actual usage of 120 input / 45 output / 165 total, persistence of pricing and usage with the terminal execution event, no execution-time discovery, no external provider call, no credentials, and migration schema 14.

Closure evidence: `docs/verification/phase-1/off-work-provider-completion-integration-hardening-closure-2026-10-05.md`.

No separate build result was supplied.
