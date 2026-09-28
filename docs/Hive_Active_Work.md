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

The revised source is **VERIFICATION FAILED / REMEDIATION REQUIRED**. The developer reported the full `Hive.Tests` suite at **415/415 passed, 0 failed, 0 skipped** on 2026-09-28 in 43.2 seconds, but the matching Example Host scenario then failed with `hive.provider.openai-compatible.transport-failed` while reading the discovery response. Same-slice remediation is required for this failure boundary.

Required verification follows the Phase 1.16 roadmap gate:

- supported provider discovery and model enumeration;
- capability normalization;
- unsupported/unavailable handling;
- discovery timeout/cancellation/failure;
- stale metadata refresh;
- explicit capability override behavior;
- no provider credentials or secrets in metadata/diagnostics;
- Example Host scenario and focused automated coverage.

Initial verification record: [Phase 1.16 verification](verification/phase-1/1.16-provider-model-capability-discovery-2026-09-28.md)

Revision verification record: [Phase 1.16 revision verification](verification/phase-1/1.16-provider-model-capability-discovery-revision-2026-09-28.md)

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run after remediation: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite. Then exercise the matching Example Host scenario again.

Failure evidence from the 2026-09-28 Example Host run: `Provider discovery failed: hive.provider.openai-compatible.transport-failed [External] The provider model-discovery response could not be read.` Do not treat the previous 415/415 automated result as sufficient until this Example Host failure is remediated and re-verified.

## Handoff status

Do not start Phase 1.17 from this file. Phase 1.16 remains active only for the explicit Example Host verification handoff; close the phase after that evidence is supplied and the current status/verification archive are finalized.