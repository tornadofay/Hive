# Hive — Active Work

Status: VERIFICATION PENDING

## Authorized slice

**Off-work provider completion — Slice 2: Built-In Provider Catalog**

Explicit user authorization: start the next off-work slice, Slice 2 only.

## Checkpoint

Started from verified Slice 1 closure at `main` commit `3da623aaa305da6069facddb98a647fbbefe839d`.

## Implementation completed

Implemented the bounded Slice 2 provider catalog:

- expanded the static `BuiltInProviderCatalog` to the complete 35-provider inventory defined by the off-work plan;
- added explicit integration-boundary metadata distinguishing OpenAI-compatible providers from providers requiring native integration;
- added discovery profiles and discovery-endpoint classifications;
- added pricing-normalization profile metadata;
- preserved credential modes and provider-specific onboarding notes;
- prevented account/region/workspace-specific providers from receiving invented universal endpoints;
- kept native providers out of the OpenAI-compatible adapter while retaining catalog representation;
- routed OpenAI-compatible discovery selection through the catalog's discovery profile rather than a second provider-key mapping;
- hardened current endpoint metadata for Together AI, AI21, and MiniMax;
- added focused built-in catalog validation and provider-discovery regression tests;
- added the deterministic `Hive.Example.WinForms` Built-In Provider Catalog scenario;
- updated owning architecture, off-work plan, and Example Host documentation.

## Explicit exclusions

Not implemented in this slice:

- runtime token-usage foundation (Slice 3);
- Phase 1.30 metrics, budgets, OpenTelemetry, or quota enforcement;
- new native provider transport implementations;
- background catalog/pricing scraping;
- live provider discovery calls;
- Agent target-selection changes;
- new durable Model resource;
- unrelated UI/control cleanup;
- roadmap advancement.

Native/different-transport providers are catalog metadata only in this slice and are explicitly marked as requiring native integration. AI21 and MiniMax remain OpenAI-compatible catalog identities but are Advanced-only because a safe universal Hive discovery endpoint is not established for them.

## Verification

Developer verification result received:

- Full `Hive.Tests`: **619 tests, 617 passed, 2 failed, 0 skipped**.
- Failed: `BuiltInProviderCatalogTests.ExistingSpecialDiscoveryAndPricingProfilesRemainExplicit`.
- Failed: `ProviderModelMetadataProviderTests.OllamaDiscovery_UsesBulkTagsAndDoesNotProbeEveryModel`.

Observed cause:
- the Ollama catalog entry did not explicitly declare its `Ollama` discovery profile after the catalog expansion;
- catalog-driven discovery therefore selected the standard OpenAI-compatible parser for the Ollama `/api/tags` response.

Remediation completed within this same Slice 2 failure boundary:
- restored `BuiltInProviderDiscoveryProfile.Ollama` on the Ollama catalog entry;
- retained catalog-driven discovery routing with no second provider-key routing table;
- no unrelated provider catalog or discovery behavior was changed.

A developer rerun is required to confirm the remediation.

Required developer verification:

```
Tests to run:
- BuiltInProviderCatalogTests
- ProviderModelMetadataProviderTests
- relevant Phase116FollowUpTests provider/catalog regressions
- full Hive.Tests

Example to run:
Providers / Provider Platform / Built-In Provider Catalog — Hive.Example.WinForms
```

The Example must remain deterministic and should report the static 35-entry catalog without creating Provider resources, accessing credentials, or making external provider calls.

Return Active Work to `VERIFICATION PENDING` after any remediation and do not close Slice 2 until the required developer verification passes.

## Closure gate

After successful developer verification, archive the verification evidence, update Current Status from actual results, close this slice, and leave only the current no-slice state before starting Slice 3.