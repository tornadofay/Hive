# Off-Work Provider Completion — Slice 2: Built-In Provider Catalog — Verification Closure — 2026-10-05

## Scope

**Authorized slice:** Off-work provider completion — Slice 2: Built-In Provider Catalog

This slice completed the bounded built-in provider inventory and its provider/discovery metadata boundary. It did not activate or advance any Phase 1 roadmap slice and did not implement runtime token usage.

## Implementation verified

The verified implementation:

- provides a static 35-provider built-in catalog;
- distinguishes OpenAI-compatible providers from providers requiring native integration;
- records explicit credential requirement, onboarding support, endpoint classification, discovery profile, and pricing-normalization profile metadata;
- keeps account/region/workspace-specific providers from receiving invented universal endpoints;
- keeps provider-specific discovery entries Advanced-only where a safe universal discovery endpoint is not established;
- keeps native/different-transport providers cataloged but outside the OpenAI-compatible adapter;
- routes OpenAI-compatible model discovery through the catalog discovery profile rather than a second provider-key routing table;
- includes the corrected explicit Ollama discovery profile so its `/api/tags` catalog is parsed by the Ollama discovery path;
- includes the deterministic Example Host catalog scenario.

## Verification history

The initial post-implementation developer run found two failures:

- `BuiltInProviderCatalogTests.ExistingSpecialDiscoveryAndPricingProfilesRemainExplicit`
- `ProviderModelMetadataProviderTests.OllamaDiscovery_UsesBulkTagsAndDoesNotProbeEveryModel`

Both failures traced to the same in-scope defect: the expanded Ollama catalog entry had not explicitly declared `BuiltInProviderDiscoveryProfile.Ollama`, causing catalog-driven discovery to select the standard OpenAI-compatible parser for Ollama's `/api/tags` response.

The defect was corrected in implementation commit `5569c361674c2a3e7385355865e4557b7f5ccbe3`. The Active Work verification gate was then restored to `VERIFICATION PENDING` in commit `712369578b9e90ce481fbe61c4d0d9c4783e0733` pending developer re-verification.

## Final developer verification

On 2026-10-05 the developer reran the full automated suite:

```
619 Tests (619 Passed, 0 Failed, 0 Skipped) run in 1.3 min
```

This full run includes the focused built-in catalog and provider model metadata regressions.

The developer also manually exercised the deterministic Example Host scenario:

**Example to run:** `Providers / Provider Platform / Built-In Provider Catalog` — Hive.Example.WinForms

Observed deterministic output:

- Catalog entries: 35
- OpenAI-compatible: 30
- Native integration required: 5
- Normal onboarding supported: 23
- Advanced-only configuration: 12
- External provider call: no
- Durable Provider resource created: no

The provider rows reported the explicit integration, credential, endpoint, discovery, and pricing classifications for all 35 entries, including the corrected Ollama `Discovery=Ollama` classification.

## Verification boundary

Verified:

- full `Hive.Tests` result: 619/619 passed, 0 failed, 0 skipped;
- built-in catalog inventory and metadata classifications through the deterministic Example Host scenario;
- no external provider call from the catalog Example Host scenario;
- no durable Provider resource creation from the catalog Example Host scenario.

Not reported / not claimed:

- a separate build result was not provided;
- no live provider catalog/discovery call was used for this deterministic catalog Example Host scenario.

## Closure

Slice 2 is **complete and verified**.

The next off-work Slice 3 — Runtime Token Usage Foundation — remains planned and **not started**. It requires separate explicit authorization and is not opened by this closure.
