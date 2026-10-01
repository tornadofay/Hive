# Phase 1.16 Follow-Up — Model Information

Status: in progress — authorized 2026-09-29

This document contains the detailed implementation plan referenced by the roadmap entry:

**1.16 Follow-Up — Complete Provider Model Metadata Discovery**

The follow-up extends the existing Phase 1.16 provider/model discovery boundary and makes the richer discovered information understandable through the Advanced Provider Configuration UI. It does not create a durable Model resource or replace the Provider → ProviderAccount → ExecutionTarget resource model. The normal Provider Settings onboarding remains intentionally minimal: the Add Provider dialog selects a built-in provider from the catalog and collects credential material only; endpoint, account, model, and target administration remains in Advanced Provider Configuration.

## 1. Scope

### Rich provider/model discovery

Extend the existing provider-neutral model discovery profile so every successfully enumerated model preserves all useful provider-reported metadata that can be safely attributed to that model within Hive's existing security, response-size, validation, cancellation, and bounded-processing limits.

The normalized profile includes, where reported:

- model/deployment identity and descriptive metadata;
- input modalities;
- output modalities;
- capabilities, including text generation, vision, tool calling, structured output, reasoning, thinking, and other machine-readable capabilities;
- thinking levels/options/defaults;
- model-scoped limits such as context, maximum input, maximum output, and other model constraints;
- structured pricing/economic information, including input/output, separately priced reasoning/thinking, cached/image/audio/request or other billable units;
- explicit free-pricing evidence;
- model-level availability, health, observation time, and freshness;
- bounded, non-secret provider-specific extension/evidence data.

Missing metadata remains explicitly unknown/not reported. Missing pricing is not free; missing limits are not unlimited; missing capability evidence is not Unsupported; missing operational state is not Healthy/Available.

Provider/account quotas and rate limits remain separate from model-scoped limits.

Discovery remains evidence rather than configuration or authorization. Automatic target reconciliation may use applicable discovered capability evidence, but rich model metadata is not copied into durable ExecutionTarget configuration. Configured target capability overrides remain authoritative.

### Advanced Provider Configuration navigation

The existing Advanced Provider Configuration window becomes a proper tree-based administrative surface with an Overview page and selectable resource/information pages:

```
Advanced Provider Configuration
├── Overview
├── Providers
├── Accounts / Credentials
├── Execution Targets
└── Model Information
```

The tree is the navigation mechanism; individual pages are replaceable content views. This is a presentation refinement over the existing generalized Advanced resource-management surface, not a second settings system.

**Overview** is the landing page. It explains the Provider → ProviderAccount → ExecutionTarget relationship, the distinction between normal Provider Settings and Advanced administration, automatic versus manual target ownership, and the observational nature of discovery. It is informational and does not require a provider credential or database initialization merely to render.

**Providers**, **Accounts / Credentials**, and **Execution Targets** retain their existing generalized CRUD/resource-management responsibilities.

### Execution Target editor order

The Execution Target editor presents Management before Capabilities. The management choice establishes whether capability state is provider/discovery-managed or administrator-configured before the administrator reaches the capability controls.

### Model Information page

**Model Information** is a read-only discovery-information page in Advanced Provider Configuration. It does not become a second model configuration store.

The page should allow the administrator to identify the relevant provider/account/endpoint discovery context and inspect successfully discovered models. It should distinguish discovery evidence from durable target configuration and show freshness/observation state so an administrator does not mistake stale or missing information for authoritative configuration.

A selected model's information view should present the normalized profile in understandable sections:

- Identity / description;
- Inputs;
- Outputs;
- Capabilities;
- Reasoning / Thinking;
- Limits;
- Pricing;
- Operational state;
- Additional provider information where safely available.

Provider-specific extension data is displayed only as bounded, non-secret observational information. It cannot grant capability, bypass authorization, or alter target policy.

### Capability configuration UX

The existing free-form capability textbox is replaced by a structured capability editor.

The editor uses known Hive capability identities and bounded state choices rather than allowing arbitrary capability keys or values to be typed.

The structured capability editor uses one consistent state-selector column for every known capability:

```
Capability           Set state                 Current
Text generation      Supported ▼              Supported • override
Vision               Not configured ▼         Supported • discovered
Tool calling         Unsupported ▼            Unsupported • override
Structured output    Supported ▼              Supported • discovered
...
```

**Current** is the effective capability state and identifies its source as `discovered`, `override`, or `not reported`. This keeps discovery evidence visible without turning it into a second configuration surface.

For an automatically managed target, the state selector is disabled and displays **Managed by discovery**. The Current value remains the discovered/effective state. For a manually managed target, the administrator can select Supported / Unsupported / Unknown as an explicit override or choose Not configured so applicable discovery evidence remains the effective state.

An explicit configured override remains authoritative even when discovery reports a different value. Unknown/unreported capability evidence remains distinct from Unsupported. Provider-specific capability evidence that Hive does not understand remains observational and is not editable through the normalized capability selector.

Unknown/unreported capability evidence must be presented as Unknown or not reported, never as zero capabilities.

Provider-specific capability evidence that Hive does not understand may remain visible as observational extension data, but it is not editable through the normalized capability editor.

### Credential semantics used by Provider Settings

Provider catalog authentication metadata must distinguish exactly:

- **No credential**;
- **Optional credential**;
- **Required credential**.

A provider not requiring a credential must not be shown as though credential entry is forbidden merely because its normal flow can succeed without one.

Ollama and LM Studio therefore support an optional protected credential in the provider/account configuration path while still allowing an empty credential.

Providers whose onboarding needs an account-specific or non-universal endpoint, such as Cloudflare in the current catalog, remain configured through Advanced. Advanced account/credential configuration must still expose the required protected credential input.

The credential model remains secret-backed. No credential is displayed or returned in plaintext.

## 2. Ownership and boundaries

- Hive.Management remains the owner of provider discovery, reconciliation, provider/account configuration, and lifecycle operations.
- Hive.Host.WinForms remains a thin presentation layer over Management/application boundaries.
- The Advanced tree/window owns navigation and page composition only.
- Model Information is observational/read-only with respect to discovered model metadata.
- ExecutionTarget configuration remains the authoritative durable target resource.
- Discovery does not introduce periodic/background refresh as part of this follow-up.
- No provider-per-vendor transport architecture is introduced.
- No durable Model resource is introduced.
- No Agent target-selection redesign is introduced.

## 3. Verification

Automated verification should cover:

- deterministic normalization of all supported rich model metadata;
- all supported input/output modalities;
- capabilities, reasoning, thinking, and thinking options/defaults;
- model limits and distinction from provider/account quotas/rate limits;
- pricing/economics and explicit free evidence;
- Unknown/not-reported semantics;
- bounded provider-specific extension data and secret redaction;
- malformed, oversized, cancelled, failed, unsupported, rate-limited, and authentication-failing discovery;
- preservation of the last successful observation on failed refresh;
- stable ProviderAccount + endpoint + model/deployment correlation;
- automatic reconciliation and configured capability-override authority;
- structured capability-editor behavior and Automatic/Manual presentation;
- credential optional/required semantics for supported catalog entries;
- Advanced tree navigation and Overview landing behavior;
- read-only Model Information presentation over deterministic discovery fixtures;
- no duplication of rich model metadata into durable target configuration.

Manual Example Host verification should exercise the real Advanced Provider Configuration surface, including:

```
Advanced Provider Configuration
├── Overview
├── Providers
├── Accounts / Credentials
├── Execution Targets
└── Model Information
```

and demonstrate a discovered model whose profile includes representative identity, modalities, capabilities, reasoning/thinking, limits, pricing, operational metadata, and bounded provider-specific evidence without using a real vendor credential.

The existing Phase 1.16 discovery Example remains the deterministic provider-discovery foundation; this follow-up adds the richer Model Information UI acceptance path.

## 4. Explicit non-goals

- no durable Model resource;
- no alternate provider transport architecture;
- no automatic model probing of every model during ordinary Refresh;
- no automatic copying of the complete rich profile into ExecutionTarget configuration;
- no model-name-only identity;
- no free-form capability authority;
- no Agent target-selection redesign;
- no periodic/background discovery scheduler.
