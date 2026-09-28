# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 — Provider / Model Capability Discovery & Operational Metadata**

This slice is explicitly authorized by the user on 2026-09-28 through `Hive: Start Phase 1.16`.

### Scope

- provider/account/model discovery through the existing Provider, ProviderAccount, ExecutionTarget, Provider adapter, and Hive.Management boundaries;
- OpenAI-compatible model enumeration through the existing shared provider transport;
- provider-neutral normalization of model metadata and the supported Hive capability vocabulary;
- explicit `Supported / Unsupported / Unknown` discovered capability state;
- process-local discovery caching, forced refresh, and stale-discovery handling;
- provider/model availability, health, and bounded rate-limit metadata where explicitly supplied;
- typed discovery failures with credential and raw-response isolation;
- explicit configured ExecutionTarget capabilities remain authoritative over discovered capability information;
- existing `ExecutionTargetSelector` remains the capability-policy authority;
- input preparation may consume an ephemeral effective capability view without rewriting persisted target configuration;
- focused regression coverage and a matching Example Host scenario.

### Out of scope

Phase 1.17 structured extraction/validation and all later V1 phases remain outside this slice. No business-app write, Tool authorization, Review, cognition, Workspace interaction, MAF orchestration expansion, or future persistence/vector design is introduced here.

## Verification boundary

The implementation is **VERIFICATION PENDING**.

Required verification follows the Phase 1.16 roadmap gate:

- supported provider discovery and model enumeration;
- capability normalization;
- unsupported/unavailable handling;
- discovery timeout/cancellation/failure;
- stale metadata refresh;
- explicit capability override behavior;
- no provider credentials or secrets in metadata/diagnostics;
- Example Host scenario and focused automated coverage.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite after focused verification passes.

No build, test, or verification claim is recorded here until the developer provides actual execution results.

## Handoff status

Do not start Phase 1.17 from this file. Close Phase 1.16 only after the required verification evidence is supplied and the current status/verification archive are updated.