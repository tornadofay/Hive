# Off-Work Plan — Provider Pricing Normalization & Built-In Provider Catalog

> **Status:** Planned, not started  
> **Roadmap impact:** None  
> **Implementation authorization:** Must be explicitly started in a dedicated chat; this file does not activate a Phase 1 roadmap slice.  
> **Prerequisite:** Finish/close the current 1.19A verification gate separately.

## Objective

Before the Hive provider portion is considered finished, complete two bounded off-roadmap corrections:

1. make model pricing normalization explicit and provider-correct;
2. complete the built-in provider catalog for the major providers Hive should know natively.

The work must remain compatible with the existing Provider → ProviderAccount → ExecutionTarget architecture and must not modify Agent target-selection behavior.

## Phase A — Finish the current verification gate first

Do not mix this plan with the outstanding Phase 1.19A verification failure.

Current recorded failure:

```
593 tests
592 passed
1 failed
0 skipped

Phase116FollowUpTests.ModelInformationView_RendersRichDiscoveryProfile
Expected: "Additional provider information"
```

The production heading defect has now been corrected within the current Active Work boundary. This remains a verification gate: developer re-verification must pass before this off-work implementation is started.

## Workstream 1 — Pricing contract

### B1. Make source quantity explicit

Audit `ProviderModelPrice.UnitQuantity` and remove any token-price path that relies on an implicit quantity of 1.

Required behavior:

- per-token source → quantity 1;
- per-1K source → quantity 1,000;
- per-1M source → quantity 1,000,000;
- unknown quantity → non-comparable.

Keep the contract as small as possible. The objective is an enforceable normalization invariant, not a general billing framework.

### B2. Introduce explicit normalization rules

Create a provider pricing normalization abstraction owned by the OpenAI-compatible provider boundary.

It should make these decisions explicit:

- source property/path;
- target billing dimension;
- source quantity;
- currency policy;
- free-evidence fields;
- pricing conditions/variants;
- whether a rate is comparable to the USD token filter.

Avoid a generic "guess the unit" algorithm.

### B3. Correct free-evidence semantics

Replace the current overly broad zero-price inference.

Required regression:

```
input > 0
output > 0
image = 0
request = 0

=> ExplicitFreeEvidence = false
=> comparable token price remains paid
```

Required free cases:

```
free = true
=> provider-declared free
```

and:

```
input = 0
output = 0

=> free for token-price filtering when both comparable rates are fully reported
```

Missing pricing must remain distinct.

### B4. Support pricing variants

Expand the normalized contract enough to represent documented conditions such as:

- peak/off-peak;
- standard/batch/priority;
- regional;
- short/long-context;
- cache-hit/cache-miss.

Do not invent a universal pricing taxonomy larger than the provider evidence requires.

The comparison layer must never silently choose an arbitrary variant when there is no deterministic applicable default.

## Workstream 2 — Provider normalization profiles

Create an explicit built-in provider normalization profile for each provider.

Initial profiles:

### Existing

- OpenAI
- Groq
- OpenRouter
- Cerebras
- NVIDIA
- Google Gemini
- Ollama
- LM Studio
- Cloudflare

### New OpenAI-compatible

- DeepSeek
- Qwen / Alibaba Cloud Model Studio
- Kimi / Moonshot
- xAI
- Mistral
- Cohere
- Fireworks AI
- Together AI
- Perplexity
- MiniMax
- AI21
- SambaNova
- DeepInfra
- Nebius AI
- SiliconFlow
- Z.ai / GLM
- StepFun
- Baidu Qianfan / ERNIE
- Tencent Hunyuan
- ByteDance Volcengine / Doubao
- Writer

### Different provider transport/integration boundaries

Catalog entries should exist now, but implementation should remain separately bounded:

- Anthropic
- AWS Bedrock
- Azure OpenAI
- Google Vertex AI
- Replicate

For these, do not force fake OpenAI-compatible discovery behavior just to make the catalog entry exist.

## Workstream 3 — Built-in catalog quality

For every built-in provider entry, define:

- stable provider key;
- display name;
- credential requirement;
- default endpoint where one is valid;
- whether normal onboarding is supported;
- discovery endpoint rules;
- pricing normalization profile;
- supported provider-specific notes;
- whether the provider is OpenAI-compatible or uses another transport.

Provider endpoints that are workspace-, account-, region-, or deployment-specific must remain Advanced-configuration entries instead of inventing unsafe default endpoints.

## Workstream 4 — Model Information integration

Once normalization is correct:

- keep the canonical comparison as USD per 1M input/output tokens;
- use the highest normalized input/output rate as the model comparison value;
- keep other billable dimensions visible but outside the token filter;
- make `Max = $0.00` use normalized zero/free semantics;
- keep missing pricing distinct;
- keep the slider range practical.

### Slider target

Normal presentation should be centered on realistic model prices rather than a $0–$1000 linear range.

Target UI:

```
$0.00 ───────────────────────── $50.00
```

with $0.01 precision.

A separate unrestricted/Any state should be available so models above the practical display range are not made impossible to inspect.

The final implementation may choose a slightly different practical ceiling after inspecting the real built-in provider catalog, but it must remain small enough to provide useful slider precision.

## Workstream 5 — Focused automated tests

Add focused tests for:

### Source-unit normalization

- OpenRouter per-token prices;
- per-1K prices;
- per-1M prices;
- explicit quantity fields;
- unknown quantity does not become comparable.

### Currency

- explicit USD;
- documented implicit USD;
- non-USD remains non-comparable to the USD filter;
- missing currency does not become USD without a provider rule.

### Free evidence

- provider-declared free;
- zero input + zero output;
- paid token prices with zero image/request charge;
- mixed paid/zero token prices;
- missing pricing.

### Pricing variants

- peak/off-peak;
- batch/priority;
- regional;
- cache-hit/cache-miss;
- deterministic default selection;
- ambiguous variant remains non-comparable rather than guessed.

### Model Information UI

- very-low OpenRouter prices;
- $0.35/M, $1.50/M style comparisons;
- free-only view;
- missing pricing;
- practical slider ceiling;
- unrestricted/Any view;
- details text shows the normalized comparable rate.

## Workstream 6 — Provider catalog tests

For every built-in provider:

- catalog entry is discoverable;
- endpoint/auth requirements are correct;
- no provider duplicates another key;
- compatible providers route to the correct discovery profile;
- native providers do not route through the OpenAI-compatible transport;
- provider-specific pricing normalization is deterministic.

A representative provider test should be maintained for each normalization pattern rather than requiring one large duplicated test per provider.

## Workstream 7 — Documentation

Update the owning provider/UI documents only after implementation proves the behavior.

Required documentation targets:

- `docs/architecture.md` or the owning architecture detail document;
- `docs/ui/controls.md`;
- `docs/ui/forms.md`;
- the provider catalog documentation;
- this off-work architecture/plan document;
- focused verification record under `docs/verification/`.

Do not modify `docs/roadmap.md` to create a new roadmap phase for this work.

## Completion criteria

The off-work provider completion task is complete when:

- all token prices entering Hive have explicit source quantity semantics;
- currency qualification is deterministic;
- free evidence cannot be inferred from an unrelated zero-priced billing dimension;
- multiple pricing conditions can be represented without arbitrary flattening;
- the Model Information filter compares normalized USD/M values correctly;
- the practical price slider is useful for real provider prices;
- the built-in catalog contains the agreed major provider set;
- every catalog entry has explicit transport/discovery/auth ownership;
- focused and full automated verification pass;
- no Agent target-selection or other main-roadmap behavior changed.

## Explicit roadmap boundary

This plan is **not** Phase 1.20, Phase 1.21, or any other roadmap slice.

It is a provider-platform completion checklist that the maintainer may execute before declaring the provider part finished.

No roadmap milestone is considered started, advanced, or closed merely because this document exists.
