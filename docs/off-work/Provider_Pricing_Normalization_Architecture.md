# Off-Work Provider Completion — Pricing Normalization Architecture

> **Status:** Design baseline / not implementation authorization  
> **Roadmap impact:** None  
> **Purpose:** Preserve the provider-pricing decisions that must be completed before the provider platform is considered finished, without creating or advancing a Phase 1 roadmap slice.

This document records an architectural correction identified during the Phase 1.19A Model Information verification work. It is intentionally outside the main roadmap. The work described here is provider-platform completion work to be performed before the provider portion is considered closed.

## 1. Problem being corrected

Hive currently stores model pricing in a form that can represent different units, but the source-unit semantics are not explicit enough at the discovery boundary.

The same normalized shape can represent:

- OpenRouter: `0.00000035 USD per input token`;
- Groq: `$0.15 USD per 1,000,000 input tokens`;
- a provider with CNY pricing;
- a provider that reports per-request or per-image charges.

The current architecture can therefore accidentally make a source value look comparable when its unit or currency was never established.

The Model Information filter must never interpret raw provider numbers. Provider discovery must normalize the source semantics first.

## 2. Canonical pricing model

A normalized `ProviderModelPrice` represents a provider-reported billable dimension and an explicit quantity basis.

Conceptually:

```
BillingDimension
    input_token
    output_token
    cached_input_token
    reasoning_token
    image
    request
    audio_second
    character
    page
    ...

Price
Currency
UnitQuantity
```

`UnitQuantity` is the number of billing-dimension units covered by `Price`.

Examples:

```
OpenRouter
input_token
price = 0.00000035
currency = USD
unitQuantity = 1

=> $0.35 per 1M tokens
```

```
Groq
input_token
price = 0.15
currency = USD
unitQuantity = 1,000,000

=> $0.15 per 1M tokens
```

The important rule is that the normalized record must not rely on an implicit `unitQuantity = 1` for token pricing. A comparable token price must have an explicitly established source quantity.

## 3. Discovery normalization rules

Pricing normalization happens once, at provider discovery ingestion.

### Rule A — explicit provider units win

When the provider reports a pricing unit or quantity, Hive preserves that exact semantic basis after validation.

Examples:

- per token → quantity 1;
- per 1K tokens → quantity 1,000;
- per 1M tokens → quantity 1,000,000.

The UI and later consumers do not reinterpret those provider values.

### Rule B — provider-specific defaults are explicit

When a provider omits a field that is guaranteed by its documented contract, Hive may apply a provider-specific default.

Examples:

- OpenRouter `prompt` / `completion` values are understood as USD per token;
- a provider whose documented endpoint returns USD per 1M tokens may receive that explicit provider normalization rule.

A generic OpenAI-compatible parser must never assume that every provider uses the same pricing unit.

### Rule C — missing currency is not automatically USD

Currency is accepted in this order:

1. provider-reported currency;
2. provider-specific documented default currency;
3. otherwise currency remains unknown.

Hive must never turn an unqualified currency into USD merely because the UI filter is denominated in USD.

For example, Qwen/Alibaba Model Studio may expose region-dependent pricing and can use currencies other than USD. Non-USD pricing remains useful observational metadata but is not USD-comparable unless a separate, explicitly authorized currency-conversion policy is introduced.

### Rule D — unresolved unit means non-comparable

If Hive cannot establish the source unit quantity, the price may remain in observational pricing metadata, but it must not enter the canonical USD token-price comparison.

This prevents a raw `0.00000035` from being accidentally interpreted as $0.00000035/M.

## 4. Canonical comparable price

The Model Information price filter compares one value per model:

**highest normalized USD input/output token rate per 1,000,000 tokens.**

Only these normalized billing dimensions participate:

- `input_token`;
- `output_token`.

Cached, reasoning, image, audio, request, character, page, and other billing dimensions remain visible in pricing details but do not change the token-price filter.

The comparable value is calculated only after:

1. the source unit has been normalized;
2. the currency has been established as USD;
3. the billing dimension is input/output token;
4. the quantity basis is valid.

This makes OpenRouter and providers using per-million-token pricing directly comparable.

## 5. Free-pricing evidence

`ExplicitFreeEvidence` must represent actual evidence that the model is free, not the existence of an arbitrary zero-priced line item.

The corrected evidence rules are:

### Provider-declared free

A provider explicitly declares a model free using a supported field such as:

- `free: true`;
- `is_free: true`;
- another provider-specific documented free flag.

This is explicit provider evidence.

### Derived zero-comparable pricing

When no explicit free flag exists, a model may be considered free for the token-price comparison when all comparable input/output token rates are present and every comparable rate is exactly zero.

This is derived from the complete comparable token set, not from one zero-priced ancillary field.

### Zero ancillary charge is not free

A model with:

```
input = paid
output = paid
image = 0
request = 0
```

is **not** free.

Likewise, a model with one paid token rate and one zero token rate is **not** free.

### Missing pricing is not free

No pricing evidence remains distinct from free pricing.

### Free-only filter

The Model Information `Max = $0.00` presentation must include models with:

- explicit free evidence and no conflicting paid comparable token pricing; or
- normalized comparable input/output token pricing equal to zero.

A model with no comparable pricing and no free evidence must not appear in the free-only view.

## 6. Multiple pricing conditions must be representable

The current one-price-per-billing-dimension assumption is too restrictive for providers that publish different rates by condition.

Examples include:

- peak vs off-peak pricing;
- standard vs batch vs priority processing;
- regional endpoint pricing;
- short-context vs long-context pricing;
- cache-hit vs cache-miss pricing.

The normalized pricing contract must therefore allow more than one pricing variant while preserving deterministic comparison semantics.

A pricing variant should be able to carry bounded conditions such as:

```
schedule = peak
service_tier = priority
region = us
context_tier = long
cache_state = hit
```

These conditions are observational pricing context. They are not authorization or execution-target configuration.

For the Model Information default token-price filter, Hive should compare the provider's default/standard applicable rate when one is unambiguous. If multiple rates are simultaneously applicable and no single default can be established safely, the UI should keep the price non-comparable rather than selecting an arbitrary rate.

## 7. Provider-specific normalization profiles

The OpenAI-compatible adapter should consume an explicit provider normalization profile rather than scattering vendor-specific assumptions through `ParsePricing`.

A profile should define, where applicable:

- model catalog endpoint format;
- pricing property names / source paths;
- source billing quantity basis;
- default currency;
- supported free-evidence fields;
- supported pricing variants/conditions;
- comparable billing dimensions;
- whether pricing is available from discovery at all.

The provider profile is part of provider discovery knowledge. It is not part of the durable ProviderModelMetadata payload itself.

The production parser should follow this shape:

```
Provider Definition
        ↓
Discovery Profile
        ↓
Raw Provider Model
        ↓
Provider Pricing Normalization Rules
        ↓
Normalized ProviderModelPricing
        ↓
Model Information / later consumers
```

## 8. Provider catalog completion

The built-in catalog should contain a curated, maintained list of major providers rather than requiring users to manually configure every common service.

### Already present in Hive

- OpenAI
- Groq
- OpenRouter
- Cerebras
- NVIDIA
- Google Gemini
- Ollama
- LM Studio
- Cloudflare

### Direct/OpenAI-compatible providers to add

Priority group:

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

These providers should reuse the existing OpenAI-compatible transport where their API contract actually supports it. A different provider name does not justify a separate transport adapter.

### Native/cloud providers requiring a different integration boundary

These should be represented in the built-in provider catalog but should not be forced through the OpenAI-compatible adapter:

- Anthropic
- AWS Bedrock
- Azure OpenAI
- Google Vertex AI
- Replicate

Their authentication, endpoint, deployment, pricing, or request semantics can require a distinct provider integration.

## 9. Catalog policy

"Built-in list of all providers" means a maintained catalog of the major first-party providers, major hosted inference platforms, major gateways, and important regional providers supported by Hive's architecture.

The catalog must not claim that the list is literally every AI service on the internet. New vendors can appear independently of Hive releases.

Hive must retain the ability to configure a custom provider endpoint where the existing generic contract is sufficient.

## 10. Architectural non-goals

This off-roadmap work does not:

- create a durable Model resource;
- redesign Agent target selection;
- change Provider → ProviderAccount → ExecutionTarget ownership;
- turn pricing into an authorization mechanism;
- scrape provider websites at runtime;
- introduce periodic background pricing refresh;
- add FX conversion silently;
- make provider-specific pricing rules editable by model output.

## 11. References used for the design

Provider contracts should be revalidated at implementation time. Current official references consulted include:

- DeepSeek Models & Pricing: https://api-docs.deepseek.com/quick_start/pricing/
- DeepSeek API compatibility: https://api-docs.deepseek.com/quick/_start/pricing/
- xAI Pricing: https://docs.x.ai/developers/pricing
- xAI REST API: https://docs.x.ai/developers/rest-api-reference/inference
- Mistral Pricing: https://docs.mistral.ai/inference/pricing
- Cohere OpenAI Compatibility: https://docs.cohere.com/docs/compatibility-api
- Qwen / Model Studio OpenAI compatibility: https://help.aliyun.com/en/model-studio/compatibility-of-openai-with-dashscope

These references are implementation inputs, not runtime dependencies.
