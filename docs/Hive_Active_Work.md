# Hive — Active Work

Status: VERIFICATION PENDING

## Authorized slice

**Phase 1.16 — UI — Provider / Model Capability Discovery Settings Integration**

Checkpoint: `88e45daeec15ca9a7edea629360f0ec49daa2ccd` (main, 2026-09-28)

## Objective

Integrate the completed Provider / Model Capability Discovery Management contract into the global Hive Settings Execution Targets workflow without moving discovery ownership into WinForms.

## In scope

- integrate `IHiveManagementFacade.GetProviderDiscoveryAsync` into the Settings Execution Target workflow;
- automatically discover models for persisted/active ExecutionTargets when Provider, ProviderAccount, credential boundary, and concrete endpoint are available;
- provide explicit refresh using the Management forced-refresh path;
- present discovered model identifiers and provider/model operational metadata including availability, health, and normalized capability state;
- allow discovered-model selection to populate the ExecutionTarget model configuration while retaining manual model entry;
- handle loading, fresh, stale, unsupported, failure, and cancellation states without applying obsolete results;
- keep discovered metadata observational and separate from persisted ExecutionTarget capability overrides;
- preserve the Provider → ProviderAccount → ExecutionTarget hierarchy and existing Management ownership;
- preserve credential secrecy and avoid raw provider responses in UI/output/diagnostics;
- add focused automated Settings-to-Management discovery coverage and relevant failure/cancellation/selection behavior;
- extend the Example Host Settings workflow only as required to demonstrate the real Settings capability.

## Explicit constraints

- no new provider transport;
- no direct provider/network access from WinForms;
- no direct SQL/persistence access from Settings UI;
- no new database/schema changes;
- no changes to the existing discovery contract unless a concrete in-scope gap requires it;
- no future roadmap phase work;
- no unrelated UI toolkit/refactoring.

## Implementation state

Implementation is complete and source-reviewed at checkpoint `baa3d2524edd2e3b37c6e29b1837d62f9490a201`.

Implemented:
- Provider Model Discovery panel in the Execution Target editor using only `IHiveManagementFacade`;
- automatic discovery for persisted targets, explicit forced Refresh, stale/unsupported/failure/cancellation handling, and obsolete-result suppression;
- discovered model selection into the editable Model field without changing configured capability overrides;
- saved-endpoint invalidation when the endpoint is edited, and post-create reopen so a new persisted target can enter discovery;
- focused automated tests plus Example Configuration and UI documentation updates;
- defensive error presentation that does not place raw Management error messages in the visible discovery status.

## Verification boundary

Developer verification required before closure:
- `tests/Hive.Tests/HiveExecutionTargetDiscoverySettingsTests.cs`;
- full `Hive.Tests` suite;
- exact Example Host Settings scenario: `Overview / Getting Started / Example Configuration` → real Settings → `Providers / Execution Targets`;
- manual verification of supported discovery, discovered-model selection/save, Refresh/stale behavior, unsupported/failure handling, cancellation, endpoint-change invalidation, and credential/raw-response secrecy;
- standing Visual Studio Treat Warnings as Errors / zero-warning build configuration.

Agent verification performed:
- repository/source inspection and post-write diff review only;
- no build, test run, application launch, or external provider call was executed.

Verification remains **PENDING** until developer results are recorded.