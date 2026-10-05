# Hive — Active Work

Status: IMPLEMENTATION IN PROGRESS

## Authorized slice

**Off-work provider completion — Slice 4: Provider Completion Integration & Hardening**

Explicit user authorization: start the next off-work slice, Slice 4.

## Checkpoint

Started from verified Slice 3 closure at `main` commit `fb451a065cd483e6e5462de6ad9eff2eb83e5505`.

## Owning plan

`docs/off-work/plan/Provider_Completion_Integration_And_Hardening.md`

The four-slice provider completion plan remains off-roadmap and does not advance Phase 1.

## Scope

- connect the completed normalized pricing evidence and runtime token usage through the existing provider/execution boundary;
- preserve the Provider → ProviderAccount → ExecutionTarget ownership chain and applicable execution/resource correlation identities;
- establish deterministic pricing applicability/evidence for the execution accounting handoff without silently guessing missing currency, quantity, or pricing variants;
- preserve historical applicability rather than substituting current provider pricing;
- harden cross-provider catalog/discovery/pricing routing and keep native/different-transport providers outside the OpenAI-compatible adapter;
- harden malformed-response, missing-usage, provider-failure, cancellation, persistence-failure, and credential/security boundaries affected by the integrated path;
- add focused deterministic integration/cross-provider regressions;
- add/update `Providers / Runtime / Provider Completion Integration & Hardening` Example Host scenario;
- update owning architecture/example/provider documentation as implementation proves the final boundary;
- prepare the exact handoff contract into Phase 1.30 without implementing Phase 1.30.

## Explicit exclusions

- Phase 1.30 metrics, budgets, OpenTelemetry, quota/rate-limit enforcement, reporting, or aggregation UI;
- tokenizer/estimation engine;
- provider billing/reconciliation APIs and account-level billing adjustments;
- new native provider transports;
- Agent target-selection redesign;
- new durable Model resource;
- unrelated UI/control cleanup;
- roadmap advancement or activation of any subsequent slice.

## Required verification

```
Tests to run:
- focused provider pricing/usage integration and cross-provider regression tests;
- affected AgentExecutionIntegrationTests;
- affected ProviderPricingNormalizationTests;
- affected BuiltInProviderCatalogTests and ProviderModelMetadataProviderTests;
- full Hive.Tests.

Example to run:
Providers / Runtime / Provider Completion Integration & Hardening — Hive.Example.WinForms
```

Build result is not to be claimed unless the developer reports it separately.

## Verification gate

When implementation is complete, return Active Work to `VERIFICATION PENDING` and require developer verification. Any in-scope failure or compile error must be recorded as `VERIFICATION FAILED / REMEDIATION REQUIRED` before same-slice remediation.

Do not start any later slice or Phase 1.30 work from this authorization.
