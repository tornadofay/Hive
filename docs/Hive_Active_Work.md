# Hive — Active Work

Status: IN PROGRESS

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

## Verification boundary

Required before closure:
- focused automated tests for the Settings discovery integration and relevant edge/cancellation paths;
- exact Example Host Settings scenario: `Overview / Getting Started / Example Configuration` → real Settings → `Providers / Execution Targets`;
- manual developer verification of discovery, model selection, refresh/stale/failure presentation, and credential secrecy;
- developer build/test verification with the repository's standing Treat Warnings as Errors configuration.

Until developer results arrive, implementation verification is **PENDING**.