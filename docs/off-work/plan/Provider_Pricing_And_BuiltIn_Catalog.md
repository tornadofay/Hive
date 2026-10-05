# Off-Work Plan — Provider Pricing, Built-In Catalog & Runtime Usage

> **Status:** Planned, not started  
> **Roadmap impact:** None  
> **Implementation authorization:** Must be explicitly started in a dedicated chat; this file does not activate a Phase 1 roadmap slice.  
> **Prerequisite:** Phase 1.19A verification is now closed. This off-work plan remains separate and still requires explicit implementation authorization in a dedicated chat.

## Objective

Before the Hive provider portion is considered finished, complete three bounded off-roadmap provider-platform tasks:

1. make model pricing normalization explicit and provider-correct;
2. complete the built-in provider catalog for the major providers Hive should know natively;
3. establish provider-reported runtime usage as the authoritative usage input for the later Phase 1.30 metrics/budget/cost boundary.

The work must remain compatible with the existing Provider → ProviderAccount → ExecutionTarget architecture and must not modify Agent target-selection behavior.

## Phase A — Prior verification gate is closed

Phase 1.19A completed its final verification on 2026-10-04:

```
593 tests
593 passed
0 failed
0 skipped
```

The previously recorded Model Information provider-details heading failure was corrected and included in the final verified result. The matching `Hive.Example.WinForms` Settings / Advanced Provider Configuration workflow was also manually confirmed by the developer.

This off-work plan is therefore no longer blocked by the 1.19A verification gate. It remains a separate, planned task: do not start implementation from this document alone, and do not open or advance a Phase 1 roadmap slice through this plan.

## Workstream 1 — Pricing contract

### 1.1 Make source quantity explicit

Audit `ProviderModelPrice.UnitQuantity` and remove any token-price path that relies on an implicit quantity of 1.

Required behavior:

- per-token source → quantity 1;
- per-1K source → quantity 1,000;
- per-1M source → quantity 1,000,000;
- unknown quantity → non-comparable.

Keep the contract as small as possible. The objective is an enforceable normalization invariant, not a general billing framework.

### 1.2 Introduce explicit normalization rules

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

### 1.3 Correct free-evidence semantics

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

=> explicit free evidence when both token-price dimensions are explicitly reported as zero
=> USD comparability remains a separate decision and may still be unavailable when currency or quantity is missing
```

Missing pricing must remain distinct.

### 1.4 Support pricing variants

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

These providers belong in the built-in provider inventory, but their transport/discovery implementation is a separate boundary:

- Anthropic
- AWS Bedrock
- Azure OpenAI
- Google Vertex AI
- Replicate

For each such provider, the catalog definition may record that a native integration is required. Do not route it through the OpenAI-compatible adapter merely to make the catalog entry functional.

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

The final implementation must choose the practical ceiling from the normalized built-in catalog and retain an unrestricted state for prices above that ceiling. The exact ceiling is deliberately not frozen in this plan.

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


## Workstream 8 — Runtime Token Usage & Cost Accounting

This workstream is separate from pricing normalization. Pricing answers what a model costs; runtime usage answers what an execution actually consumed. It is designed as the provider-usage foundation that Phase 1.30 will consume for metrics, budgets, and cost accounting.

### 8.1 Capture provider-reported usage

Capture usage from the provider response at the provider boundary whenever the provider reports it.

The preferred evidence path is:

```
Provider response + usage
        ↓
Provider-specific usage normalization
        ↓
Provider-neutral usage contract
        ↓
Execution / ChatResponse boundary
        ↓
Per-execution usage record
```

The normal authoritative source is the provider's reported usage. Hive must not replace provider-reported usage with a generic local tokenizer when authoritative provider usage is available.

### 8.2 Preserve usage evidence

Usage must preserve its evidence quality:

- `Actual` — provider explicitly reported the usage value;
- `Estimated` — Hive calculated a bounded estimate because provider usage was unavailable and a documented estimation path was applicable;
- `Unknown` — usage was not reported and Hive could not safely determine it.

Missing usage must never be represented as zero.

Estimated usage must never be presented or persisted as provider-reported actual usage.

A local tokenizer, where later introduced, is therefore an estimation mechanism only. Tokenizer selection must remain model/provider-aware and must not become a universal assumption that all models share one tokenization scheme.

### 8.3 Normalize common usage dimensions

The provider-neutral usage contract should preserve, where reported:

- input token count;
- output token count;
- total token count;
- cached input token count;
- reasoning/thinking token count;
- other bounded provider-reported usage dimensions.

Do not manufacture totals or add breakdown fields together unless the provider contract or explicit normalization rule establishes that relationship.

In particular, cached-input and reasoning/thinking counts must not automatically be treated as additional tokens on top of the provider's input/output totals. A provider-reported total remains authoritative when present.

### 8.4 Preserve execution identity

Usage belongs to the actual execution, not merely to a model name.

Each usage record should be correlated with the applicable:

- Provider;
- ProviderAccount;
- ExecutionTarget;
- model/deployment identity;
- Tenant/User/Workspace scope where applicable;
- Agent;
- Runtime;
- Execution;
- WorkItem;
- observation timestamp.

The exact correlation set must follow the established resource/provenance contract rather than introducing a parallel identity model.

This allows Hive to answer usage questions by user, model, provider, target, Agent, Runtime, Execution, WorkItem, and time period without reconstructing ownership after the fact.

### 8.5 Persist immutable per-execution usage

A completed execution's usage observation should be durable and immutable once the execution reaches its applicable terminal accounting boundary.

Historical usage must remain correct even when provider pricing changes later.

Usage records must therefore remain separate from current provider pricing metadata.

### 8.6 Calculate cost from usage + applicable pricing

Cost is a derived accounting result:

```
recorded execution usage
        +
pricing applicable to that execution
        ↓
calculated Hive cost
```

The calculation must use the normalized pricing evidence applicable to the execution rather than the provider's current price at reporting time.

Where usage or pricing is missing/non-comparable, Hive must preserve that uncertainty rather than silently reporting a zero cost.

Cost estimates and provider-billed amounts must remain semantically distinct unless a provider gives Hive authoritative billing evidence.

### 8.7 Aggregation

Once per-execution usage is available, provide bounded aggregation by:

- User / applicable scope;
- Provider;
- ProviderAccount;
- ExecutionTarget;
- model;
- Agent;
- Runtime;
- WorkItem;
- day/week/month or another explicit reporting period.

Aggregation is reporting over recorded execution observations; it must not become a second source of truth.

### 8.8 Streaming and multi-response handling

For true provider streaming, prefer the provider's authoritative final usage report when available rather than counting tokens from streaming chunks.

Where an execution can produce multiple provider calls, record usage per provider call/execution component and aggregate them at the execution/WorkItem level through deterministic correlation. Do not collapse multiple calls into a single guessed total before the individual usage observations are preserved.

### 8.9 Phase 1.30 boundary

This workstream is a prerequisite/input to Phase 1.30 rather than a replacement for it.

Phase 1.30 remains responsible for:

- execution/provider/model metrics;
- budget enforcement;
- OpenTelemetry traces/metrics;
- quota/rate-limit handling;
- cancellation and lifecycle integration;
- consuming normalized token/cost information.

The off-work provider task should make the usage evidence available cleanly enough that Phase 1.30 does not need provider-specific token parsing or pricing heuristics.



## Workstream 8 — Runtime Token Usage & Cost Accounting

This workstream is separate from pricing normalization. Pricing answers what a model costs; runtime usage answers what an execution actually consumed. It is designed as the provider-usage foundation that Phase 1.30 will consume for metrics, budgets, and cost accounting.

### 8.1 Capture provider-reported usage

Capture usage from the provider response at the provider boundary whenever the provider reports it.

The preferred evidence path is:

```
Provider response + usage
        ↓
Provider-specific usage normalization
        ↓
Provider-neutral usage contract
        ↓
Execution / ChatResponse boundary
        ↓
Per-execution usage record
```

The normal authoritative source is the provider's reported usage. Hive must not replace provider-reported usage with a generic local tokenizer when authoritative provider usage is available.

### 8.2 Preserve usage evidence

Usage must preserve its evidence quality:

- `Actual` — provider explicitly reported the usage value;
- `Estimated` — Hive calculated a bounded estimate because provider usage was unavailable and a documented estimation path was applicable;
- `Unknown` — usage was not reported and Hive could not safely determine it.

Missing usage must never be represented as zero.

Estimated usage must never be presented or persisted as provider-reported actual usage.

A local tokenizer, where later introduced, is therefore an estimation mechanism only. Tokenizer selection must remain model/provider-aware and must not become a universal assumption that all models share one tokenization scheme.

### 8.3 Normalize common usage dimensions

The provider-neutral usage contract should preserve, where reported:

- input token count;
- output token count;
- total token count;
- cached input token count;
- reasoning/thinking token count;
- other bounded provider-reported usage dimensions.

Do not manufacture totals or add breakdown fields together unless the provider contract or explicit normalization rule establishes that relationship.

In particular, cached-input and reasoning/thinking counts must not automatically be treated as additional tokens on top of the provider's input/output totals. A provider-reported total remains authoritative when present.

### 8.4 Preserve execution identity

Usage belongs to the actual execution, not merely to a model name.

Each usage record should be correlated with the applicable:

- Provider;
- ProviderAccount;
- ExecutionTarget;
- model/deployment identity;
- Tenant/User/Workspace scope where applicable;
- Agent;
- Runtime;
- Execution;
- WorkItem;
- observation timestamp.

The exact correlation set must follow the established resource/provenance contract rather than introducing a parallel identity model.

This allows Hive to answer usage questions by user, model, provider, target, Agent, Runtime, Execution, WorkItem, and time period without reconstructing ownership after the fact.

### 8.5 Persist immutable per-execution usage

A completed provider-call or execution-component usage observation should be durable and immutable once it reaches its applicable terminal accounting boundary.

Historical usage must remain correct even when provider pricing or provider catalog data changes later.

Usage records and pricing snapshots remain separate concerns.

### 8.6 Calculate cost from usage + applicable pricing

Cost is a derived accounting result:

```
recorded execution usage
        +
pricing applicable to that execution
        ↓
calculated Hive cost
```

The calculation must use the normalized pricing evidence or preserved pricing snapshot applicable to the specific usage observation rather than the provider's current price at reporting time.

Where usage or pricing is missing/non-comparable, Hive must preserve that uncertainty rather than silently reporting a zero cost.

Hive-calculated cost remains an accounting estimate unless the provider supplies authoritative billing evidence. Provider-billed amounts and account-level adjustments such as credits, discounts, taxes, plan allowances, and quota effects remain distinct unless explicitly supported by provider evidence.

### 8.7 Reporting/aggregation handoff to Phase 1.30

The usage records must contain the dimensions required for Phase 1.30 to provide bounded aggregation by:

- User / applicable scope;
- Provider;
- ProviderAccount;
- ExecutionTarget;
- model;
- Agent;
- Runtime;
- WorkItem;
- day/week/month or another explicit reporting period.

Aggregation/reporting itself belongs to the Phase 1.30 observability/accounting boundary. It must operate over recorded usage observations and must not become a second source of truth.

### 8.8 Streaming and multi-response handling

For true provider streaming, prefer the provider's authoritative final usage report when available rather than counting tokens from streaming chunks.

Where an execution can produce multiple provider calls, record usage per provider call/execution component and aggregate them at the execution/WorkItem level through deterministic correlation. Do not collapse multiple calls into a single guessed total before the individual usage observations are preserved.

### 8.10 Phase 1.30 boundary

This workstream is a prerequisite/input to Phase 1.30 rather than a replacement for it.

Phase 1.30 remains responsible for:

- execution/provider/model metrics;
- budget enforcement;
- OpenTelemetry traces/metrics;
- quota/rate-limit handling;
- cancellation and lifecycle integration;
- consuming normalized token/cost information.

The off-work provider task should make the usage evidence available cleanly enough that Phase 1.30 does not need provider-specific token parsing or pricing heuristics.

## Workstream 7 — Documentation

Update the owning provider/UI documents only after implementation proves the behavior.

Required documentation targets:

- `docs/architecture.md` or the owning architecture detail document;
- `docs/architecture/execution-and-persistence.md` for execution/usage accounting boundaries;
- `docs/ui/controls.md`;
- `docs/ui/forms.md`;
- the provider catalog documentation;
- this off-work architecture/plan document;
- focused verification record under `docs/verification/`.

Do not modify `docs/roadmap.md` to create a new roadmap phase for this work.

## Workstream 8 — Runtime Token Usage & Cost Accounting Foundation

This workstream is separate from pricing normalization. Pricing answers what a model costs; runtime usage answers what an execution actually consumed. This off-work boundary establishes the provider-usage evidence and attribution foundation that Phase 1.30 will consume for metrics, budgets, and cost accounting. It does not create a second reporting/metrics subsystem or replace Phase 1.30.

### 8.1 Capture provider-reported usage

Capture usage from the provider response at the provider boundary whenever the provider reports it.

The preferred evidence path is:

```
Provider response + usage
        ↓
Provider-specific usage normalization
        ↓
Provider-neutral usage contract
        ↓
Execution / ChatResponse boundary
        ↓
Per-provider-call usage observation
        ↓
Phase 1.30 execution aggregation
```

The normal authoritative source is the provider's reported usage. Hive must not replace provider-reported usage with a generic local tokenizer when authoritative provider usage is available.

### 8.2 Preserve usage evidence

Usage must preserve its evidence quality:

- `Actual` — provider explicitly reported the usage value;
- `Estimated` — Hive calculated a bounded estimate because provider usage was unavailable and a documented estimation path was applicable;
- `Unknown` — usage was not reported and Hive could not safely determine it.

Missing usage must never be represented as zero.

Estimated usage must never be presented or persisted as provider-reported actual usage.

A local tokenizer, where later introduced, is therefore an estimation mechanism only. Tokenizer selection must remain model/provider-aware and must not become a universal assumption that all models share one tokenization scheme.

### 8.3 Normalize common usage dimensions

The provider-neutral usage contract should preserve, where reported:

- input token count;
- output token count;
- total token count;
- cached input token count;
- reasoning/thinking token count;
- other bounded provider-reported usage dimensions.

Do not manufacture totals or add breakdown fields together unless the provider contract or explicit normalization rule establishes that relationship.

In particular, cached-input and reasoning/thinking counts must not automatically be treated as additional tokens on top of the provider's input/output totals. A provider-reported total remains authoritative when present.

### 8.4 Preserve execution identity and scope

Usage belongs to the actual execution, not merely to a model name.

Each usage record should be correlated with the applicable:

- Provider;
- ProviderAccount;
- ExecutionTarget;
- model/deployment identity;
- Tenant/User/Workspace scope where applicable;
- Agent;
- Runtime;
- Execution;
- WorkItem;
- observation timestamp.

The exact correlation set must follow the established resource/provenance contract rather than introducing a parallel identity model.

This gives Phase 1.30 enough authoritative attribution to answer usage questions by user, model, provider, target, Agent, Runtime, Execution, WorkItem, and time period without reconstructing ownership after the fact.

### 8.5 Persist immutable usage observations and preserve pricing applicability

Persist usage at the smallest meaningful provider-call/execution-component boundary so a multi-call execution does not lose individual usage evidence.

Each provider usage observation should become immutable once that provider call reaches its applicable accounting boundary. An execution-level or WorkItem-level total is an aggregate derived from those immutable observations, not a replacement for them.

Historical usage must remain correct even when provider pricing or provider catalog data changes later.

For any cost calculation that Hive performs, preserve the normalized pricing evidence or pricing snapshot needed to establish which rate/variant was applicable to that usage observation. Do not recalculate historical cost later from whatever the provider currently advertises.

Usage records and pricing snapshots remain separate concerns, even though the cost calculation correlates them.

### 8.6 Calculate cost from usage + applicable pricing

Cost is a derived Hive accounting result:

```
recorded execution usage
        +
pricing applicable to that execution
        ↓
calculated Hive cost
```

The calculation must use the normalized pricing evidence or preserved pricing snapshot applicable to the specific usage observation rather than the provider's current price at reporting time.

Where usage or pricing is missing/non-comparable, Hive must preserve that uncertainty rather than silently reporting a zero cost.

Hive-calculated cost remains an estimate/accounting calculation unless the provider supplies authoritative billing evidence. Provider-billed amounts, credits, discounts, taxes, plan allowances, quota effects, and other account-level billing adjustments must remain distinct unless explicitly supported by provider evidence.

### 8.7 Reporting/aggregation handoff to Phase 1.30

The usage records must contain the dimensions required for Phase 1.30 to provide bounded aggregation by:

- User / applicable scope;
- Provider;
- ProviderAccount;
- ExecutionTarget;
- model;
- Agent;
- Runtime;
- WorkItem;
- day/week/month or another explicit reporting period.

Aggregation/reporting itself belongs to the Phase 1.30 observability/accounting boundary. It must operate over recorded usage observations and must not become a second source of truth.

### 8.8 Streaming and multi-response handling

For true provider streaming, prefer the provider's authoritative final usage report when available rather than counting tokens from streaming chunks.

Where an execution can produce multiple provider calls, record usage per provider call/execution component and aggregate them at the execution/WorkItem level through deterministic correlation. Do not collapse multiple calls into a single guessed total before the individual usage observations are preserved.

### 8.9 Focused usage verification

Add focused tests for:

- provider reports input/output/total usage and Hive preserves it;
- cached-input and reasoning/thinking breakdowns are preserved without double-counting;
- missing usage remains Unknown rather than zero;
- provider-reported usage is classified as Actual;
- bounded estimation, where explicitly supported later, is classified as Estimated and never as Actual;
- provider-specific usage property normalization is deterministic;
- multiple provider calls remain individually attributable;
- cancellation/failure does not fabricate a successful usage record;
- usage correlation preserves the applicable Provider / ProviderAccount / ExecutionTarget / model / User/scope / Agent / Runtime / Execution / WorkItem identities;
- true streaming prefers authoritative final provider usage when available.

### 8.10 Phase 1.30 boundary

This workstream is a prerequisite/input to Phase 1.30 rather than a replacement for it.

Phase 1.30 remains responsible for:

- execution/provider/model metrics;
- budget enforcement;
- OpenTelemetry traces/metrics;
- quota/rate-limit handling;
- cancellation and lifecycle integration;
- consuming normalized token/cost information.

The off-work provider task should make the usage evidence available cleanly enough that Phase 1.30 does not need provider-specific token parsing or pricing heuristics.

## Completion criteria

The off-work provider completion task is complete when:

- all token prices entering Hive have explicit source quantity semantics;
- provider-reported runtime token usage can be captured without being discarded at the provider boundary;
- usage evidence distinguishes Actual, Estimated, and Unknown and never treats missing usage as zero;
- usage is correlated to the applicable execution/resource identities so Phase 1.30 can aggregate it without reconstructing ownership;
- historical cost can be calculated from recorded usage plus preserved pricing applicability for that execution without silently using current prices;
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
