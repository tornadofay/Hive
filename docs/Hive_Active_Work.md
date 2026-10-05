# Hive — Active Work

Status: VERIFICATION PENDING

## Off-Work Slice — Model Information Decision Surface: Pricing Correctness, Provider Metadata Enrichment & UI/UX Redesign

Authorized: 2026-10-05 (explicit maintainer request in a dedicated chat)
Checkpoint: `b9cde02` — "Cover structured Model Information details and tiered pricing"
Roadmap impact: None. Bounded off-roadmap work. Does not activate or advance any Phase 1 roadmap slice.

### Maintainer intent

Model Information is the surface where a user decides which model to bring into Hive. It must be:

- **Functionally correct** — a bounded filter must never silently hide the catalog.
- **Decidable** — surface the information a user actually chooses on.
- **Simple for non-technical users**, while remaining a precise tool for experienced users.
- **Free-model friendly** — a user with no budget must find usable free models easily.

### Part 1 — Confirmed defect (reproduced)

**Symptom (developer-reported):** On OpenRouter the max price slider starts at $5. Dragging it below ~$4.78 removes every model; at $0 free models reappear. ~19 pages of models.

**Root cause, reproduced in `tests/Hive.Tests/ModelInformationOpenRouterPricingTests.cs` (4 tests, passing):**

1. OpenRouter reports USD **per token** (`"prompt": "0.0000007"`) — that is $0.70/M.
2. Per-token normalization applies only under the OpenRouter discovery profile. `ModelCatalogParserRegistry` supplies `defaultTokenUnitQuantity` only for `OpenRouter` / `CerebrasOpenRouter`; under `Standard` the same numbers yield `Currency = null`, `UnitQuantity = null`.
3. `IsComparableTokenPrice` requires `Currency == "USD"` and `UnitQuantity > 0`, so `TryGetComparableTokenPricePerMillion()` returns `null`.
4. `ModelInformationFilter.MatchesPrice` returns `false` for a null comparable price under any bounded range, so the whole catalog vanishes with no explanation.
5. At $0 the separate `IsFreeModel` branch applies — this asymmetry is the diagnostic tell.
6. `ConfigurePriceFiltersForSnapshot` derives its ceiling from comparable rates only, so all-null yields the `$5` fallback. The ceiling is a symptom, not the cause.

**Suspected configuration cause (needs developer confirmation):** `OpenAICompatibleProviderCapabilityDiscovery` resolves the profile via `BuiltInProviderCatalog.Find(providerKey)?.DiscoveryProfile ?? StandardOpenAICompatible`. A Provider created as generic `openai-compatible` rather than the built-in OpenRouter catalog entry falls back to `Standard`, which is non-comparable.

**Authorized fix (maintainer chose option C):**

- (a) Correct the configuration path so a genuine OpenRouter provider uses the OpenRouter discovery profile.
- (b) Make the filter incapable of silently hiding models: unknown comparable pricing becomes an explicit, visible, user-controllable state.
### Part 2 — Provider metadata enrichment (from live API research)

Live `GET https://openrouter.ai/api/v1/models` was retrieved during this slice. Hive already consumes `architecture.input_modalities` / `output_modalities`, `context_length`, `top_provider`, and `supported_parameters`. Verified gaps:

1. **`pricing.overrides` is ignored.** OpenRouter returns tiered pricing as an **array**: `"overrides": [{"min_prompt_tokens": 272000, "prompt": "0.000004", "completion": "0.000015"}]`. `ParsePricing` only recognizes an **object** named `variants`. Users currently see only the base rate and are silently misled for long-context requests.
2. **`reasoning` object is ignored.** OpenRouter returns `{"mandatory": bool, "default_enabled": bool, "supported_efforts": ["max","xhigh","high","medium","low","none"], "default_effort": "medium"}`. `ParseThinking` reads only top-level `supported_reasoning_efforts` and a `thinking` object, so reasoning levels and the mandatory-reasoning case are lost.
3. **Free-model signal is available and reliable.** Free variants report `"prompt": "0", "completion": "0"` and ids ending `:free`. This must drive a first-class, easily reachable free-model experience.

Confirmed available but unused: `name`, `canonical_slug`, `instruct_type`, `architecture.modality`, `knowledge_cutoff`, `per_request_limits`, `supported_voices`, `benchmarks`.

### Part 3 — UI/UX redesign of Model Information

The existing details panel is a single scrolling `Label` blob. Required outcome:

- Decision-oriented layout, not a field dump.
- Grouped, scannable sections with clear visual hierarchy.
- Capability and state rendered as readable chips rather than raw text.
- Pricing presented in the comparable unit users think in, alongside the provider's raw source unit, with an explicit "no comparable pricing" state.
- A prominent, low-friction path to find free models.
- Filter controls sufficient to decide on context window, modality, capability, and price.
- Correct empty states distinguishing "no match" from "no comparable pricing".
- Responsive and readable for both novice and expert users.

### Constraints

- No roadmap advancement.
- Model Information stays read-only observational UI; adding a model to Hive remains an explicit user action.
- Provider parsing changes must be behavior-preserving except for the two verified gaps in Part 2.
- UI work follows `docs/ui/` guidance and reuses existing Hive UI APIs.

### Verification gate

1. Full `Hive.Tests` suite passes.
2. Focused tests for the filter state machine, pricing `overrides` parsing, reasoning-level parsing, and the redesigned view.
3. Developer manual verification of `Hive.Example.WinForms` **Providers / Provider Platform / Model Information**: free-model discovery, bounded-price behavior with and without comparable pricing, tiered pricing display, reasoning levels, and the redesigned details panel.
4. Confirmation of the developer-side OpenRouter provider catalog wiring.

### Implementation progress

**Part 1(b)(c) — done.** Verified behaviour is preserved: a bounded price range still excludes models with no comparable per-million rate. The difference is that it is no longer silent.

- `ModelInformationFilter` gained `UnknownPricingVisibility` (default `Exclude`) and `CountWithoutComparablePricing`.
- The view gained a "Show models without comparable pricing" checkbox and a filter notice that states how many models are hidden and why, and distinguishes "0 models match" from "23 of 480 models are hidden by the price filter".
- `Phase116FollowUpTests.ModelInformationView_DoesNotAssumeMissingPricingQuantity` was adapted from `MaxPriceFilter = 100` to `50`. Its intent is unchanged; only the absolute slider value moved because the ceiling is now data-derived. This is a deliberate, recorded adaptation to an authorized behaviour change, not a test relaxed to fit the code.

**Slider range — percentile-based ceiling.** Real provider catalogs are heavily skewed: OpenRouter spans roughly $0.01/M to $600/M with almost everything at the cheap end. A ceiling derived from the single most expensive model produced a slider whose entire usable range occupied a fraction of a percent of its travel. `CalculatePriceCeiling` now takes the 90th-percentile comparable rate and rounds it up to a friendly increment, and `CountAboveCeiling` reports how many models sit beyond it. The view exposes a "Show models above the price range" control and states the count in the filter notice, so nothing is silently unreachable. With no comparable evidence the ceiling is a small `$1.00`.

**Part 1(a) — root cause fixed.** The built-in `openrouter` catalog definition set `pricingNormalizationProfile` but never set `discoveryProfile`, so it silently inherited the `StandardOpenAICompatible` default. Real discovery therefore resolved `OpenAICompatibleModelCatalogFormat.Standard`, which supplies no per-token quantity, making every OpenRouter rate non-comparable. `BuiltInProviderDiscoveryProfile.OpenRouter` existed and was handled by the discovery switch, but no catalog entry ever selected it. The entry now declares that profile explicitly, and a regression guard asserts it can never fall back. This is the defect that caused the reported empty-catalog behaviour; the filter work above is defence in depth. A catalog audit confirmed only cerebras, google-gemini, ollama, lm-studio and cloudflare declare a non-standard profile, and all five already did; no other provider has this class of defect.

**Two verified WinForms assertions adapted, deliberately recorded:** `ModelInformationView_DoesNotAssumeMissingPricingQuantity` moved from `MaxPriceFilter` `100` to `50`, because the fixture ceiling is now `$1.00` rather than `$5.00` and the value must remain a *bounded* selection. `ModelInformationView_PriceRangeFilterCanShowOnlyFreeModels` replaced the magic `Assert.Equal(500, MaxPriceFilter.Value)` with `Assert.Equal(MaxPriceFilter.Maximum, MaxPriceFilter.Value)`, asserting the filter starts at its own derived ceiling instead of a hardcoded number.

**Tiered pricing — done and verified.** `ParsePricingOverrides` parses OpenRouter's array-shaped `pricing.overrides` into pricing variants, inheriting base rates so a tier is never shown as cheaper than it is. Verified against a live-shaped payload: an override at `min_prompt_tokens: 272000` is preserved with its threshold condition and its `$4.00 / 1M` rate, while the base `$10.00 / 1M` remains the comparable headline.

**Reasoning levels — done and verified.** `ParseThinking` now reads OpenRouter's nested `reasoning` object (`supported_efforts`, `default_effort`), and `ParseCapabilities` reads `reasoning.mandatory` as reported evidence. Verified: efforts `["max","high","medium","low","none"]` with default `medium`.

**Part 3 — performance first, then layout (implemented; verification pending).**

*Performance.* Light-weightness was raised as a first-class requirement. Profiling the filter path by reading it showed three compounding costs, all repeated per model on every slider tick:

1. `GetComparableTokenPricePerMillion` recomputed the comparable rate from the pricing entries, and `UpdateFilterNotice` then computed it a second time across the whole catalog.
2. `ResolveExecutionTarget` resolved each model's execution target with a linear scan over all targets, with URI comparisons inside — quadratic in models × targets.
3. The six capability list columns each performed a LINQ scan over the discovered capabilities per row per redraw, roughly 2,900 scans for a 480-model catalog.

`ModelCatalogIndex` now resolves comparable price, free-ness, capability summary, and the target mapping once per snapshot; target resolution is a dictionary lookup, and filtering is a comparison over cached decimals. `ModelCatalogEntry` carries the precomputed `ModelCapabilitySummary`. The list columns read precomputed states instead of scanning.

*Layout.* The catalog columns were reoriented toward deciding which model to bring into Hive: `Model`, `Price / 1M`, `Context`, `Text`, `Vision`, `Tools`, `Reasoning`. Price is shown in the same per-million unit the filter uses, with an explicit `Free` marker coloured through the theme success state and `Not known` for a non-comparable rate, so the list never implies free when the evidence is absent. Context window is abbreviated (`1M`, `105K`). `Structured` and `Thinking` were dropped from the column set to make room; both remain visible in the details panel and in the capability filter.

**Test adaptations recorded:** `Phase116FollowUpTests` column-header and sub-item assertions were updated to the new column set and order. These are assertions about presentation layout, which the maintainer explicitly authorized changing.

**Details-panel redesign — implemented in the current slice.** The existing multiline detail blob was replaced with stable Hive-owned detail sections for Identity, Inputs, Outputs, Capabilities, Reasoning / Thinking, Limits, Pricing, Operational state, and Additional provider information. Capability states are rendered as readable themed chips; known capabilities remain visible as Unknown / unreported when discovery has no evidence. Pricing now presents the decision price, explicit-free evidence, raw source rates, and tiered pricing variants/conditions without collapsing provider source units into the comparable display. The details surface remains fixed-width with the existing HiveScrollHost and updates existing section content rather than rebuilding the section hierarchy on every selection.

**Verification coverage for the redesign was added.** `Phase116FollowUpTests` now asserts the stable section hierarchy, readable capability-state presentation, and tiered pricing display. The list-row pricing value also consumes the ModelCatalogIndex's precomputed comparable rate so slider/list redraws do not recompute pricing for every row.

**Not started:** the live multi-provider API research remains incomplete because DeepSeek returned HTTP 401 and the Groq/OpenRouter documentation URLs returned 404. Only OpenRouter has live-verified response data.

### Historical verification before the current details redesign

The following results were reported before the current details-panel changes. They are retained as history and do **not** verify the current code.

- `dotnet build Hive.sln` — 0 errors, 0 warnings.
- Full `Hive.Tests` — **678 passed, 0 failed, 0 skipped**. The prior verified baseline was 663.
- Focused `ModelInformation*` + `Phase116FollowUpTests` — 52 passed, 0 failed.
- Developer confirmed the Model Information price filter now functions against live OpenRouter data with a correct maximum.
- `Hive.Example.WinForms` has not been launched by the agent.

### Current verification status

No build or test run has been performed for the current details-panel redesign. The slice is therefore intentionally **VERIFICATION PENDING** until the developer runs the required automated and manual verification.

- (c) Never present "no rows" as if the catalog were empty; distinguish "0 models match" from "no comparable pricing evidence".