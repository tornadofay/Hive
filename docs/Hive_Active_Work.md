# Hive — Active Work

Status: IMPLEMENTATION IN PROGRESS

## Authorized slice

**Off-work provider completion — Slice 2: Built-In Provider Catalog**

Explicit user authorization: start the next off-work slice, Slice 2 only.

## Checkpoint

Started from verified Slice 1 closure at `main` commit `3da623aaa305da6069facddb98a647fbbefe839d`.

## Scope

Implement the bounded built-in provider catalog defined by the off-work provider plan:

- complete the built-in provider inventory listed for the off-work provider platform;
- represent stable provider key, display name, credential requirement, default endpoint where a safe universal endpoint exists, normal-onboarding eligibility, discovery behavior/profile, pricing-normalization profile, and provider-specific onboarding notes;
- distinguish providers that can use the existing OpenAI-compatible transport from providers requiring a different/native integration boundary;
- keep account-, workspace-, region-, deployment-, or endpoint-specific providers out of unsafe default endpoint assumptions;
- preserve the existing Provider → ProviderAccount → ExecutionTarget architecture;
- add focused catalog validation and routing tests;
- update the owning architecture/plan documentation.

## Exclusions

Not included:

- runtime token-usage foundation (Slice 3);
- Phase 1.30 metrics, budgets, OpenTelemetry, or quota enforcement;
- new native provider transport implementations;
- background catalog/pricing scraping;
- live provider discovery calls;
- Agent target-selection changes;
- new durable Model resource;
- unrelated UI/control cleanup;
- roadmap advancement.

Native/different-transport providers may be represented as catalog entries with an explicit native-integration-required classification, but Slice 2 must not pretend that they are functional through the OpenAI-compatible adapter.

## Verification

- Source inspected/reviewed: Yes.
- Developer automated verification: Not yet run for Slice 2.
- Build: Not executed by agent.
- Example Host: Not yet manually verified for Slice 2.

Required developer verification after implementation:

- focused built-in-provider catalog tests;
- relevant provider/discovery regression tests;
- full `Hive.Tests`;
- matching Example Host provider/settings scenario demonstrating the expanded built-in provider catalog without performing live external calls unless the developer deliberately configures one.

Return Active Work to `VERIFICATION PENDING` before handoff and do not close Slice 2 until developer verification passes.