# Phase 1.16 — Provider / Model Capability Discovery & Operational Metadata

## Purpose

Phase 1.16 completes the next Provider-platform boundary by discovering provider model metadata and normalizing provider-reported capability information without replacing the existing configured ExecutionTarget capability model.

Discovery is operational evidence. Persisted Provider, ProviderAccount, and ExecutionTarget configuration remains the durable authority.

## Discovery boundary

`IHiveManagementFacade.GetProviderDiscoveryAsync` is the consumer-facing entry point. Management:

- loads the access-scoped Provider, ProviderAccount, and active ExecutionTarget;
- resolves the account credential through the existing Secret Store boundary when a credential reference exists;
- invokes the configured provider discovery implementation;
- caches successful observations in a bounded process-local cache keyed by the relevant provider/account/target configuration;
- exposes stale observations as stale instead of silently converting them into current state;
- supports forced refresh; a failed refresh does not replace the last successful observation.

Provider discovery is represented by `IProviderCapabilityDiscovery` in Hive.Core. The first implementation is `OpenAICompatibleProviderCapabilityDiscovery`, which uses the existing OpenAI-compatible HTTP transport and calls the OpenAI-compatible `/models` endpoint.

Compatible hosted and local providers remain configurations of the same adapter. Phase 1.16 does not add a provider-specific transport implementation for each vendor.

## Model metadata

Discovered models expose provider-neutral metadata:

- model identifier and optional provider ownership value;
- optional creation time;
- model availability;
- model health;
- normalized discovered capability states.

Operational provider metadata also records observation time, stale-after time, availability, health, and bounded rate-limit information when the provider exposes it.

Missing provider fields remain `Unknown`. A successful metadata request is not treated as proof of model health.

## Capability normalization

The OpenAI-compatible adapter recognizes a bounded Hive capability vocabulary and known provider aliases:

```text
text.generate
vision
structured.output
tool.calling
```

Boolean and textual provider capability values normalize to Hive `Supported`, `Unsupported`, or `Unknown`. Unknown vendor-only capability names are ignored rather than guessed.

Configured ExecutionTarget capabilities remain authoritative. Discovered capabilities may add information that the target has not explicitly configured, but discovery never rewrites the persisted target.

`ExecutionTargetSelector` remains the authoritative policy boundary. Phase 1.16 supplies an ephemeral effective capability view to that selector; it does not create a second selection implementation.

## Stale and failure behavior

Discovery observations are timestamped and become stale after a bounded freshness interval. Stale observations do not grant new effective capabilities.

Input preparation explicitly refreshes stale discovery before using discovered capability information. If refresh fails, the previous successful observation remains cached and the routing path does not silently fabricate support.

Transport failures, timeout, cancellation, malformed model catalogs, unsupported model enumeration, and authentication/HTTP failures remain typed outcomes. Error messages do not include credentials, authorization headers, or raw provider response bodies.

A provider that does not implement `/models` is represented as model-enumeration unsupported rather than as an invented model list.

## Example Host

The externally usable Example Host scenario is:

`Providers → Target Selection → Capability Discovery → Provider / Model Capability Discovery`

It creates Provider → ProviderAccount → ExecutionTarget through the public Management facade, serves a deterministic local OpenAI-compatible `/models` response, discovers the model and its capabilities, routes a required vision capability using the existing selector, and demonstrates that an explicit configured `Unsupported` capability remains authoritative.

The example uses no provider credentials.

## Verification

Phase 1.16 remains incomplete until the active-work verification boundary has been executed. The required automated and manual verification is recorded in `docs/Hive_Active_Work.md`.