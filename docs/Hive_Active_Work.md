# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 implementation. It does not advance the roadmap.

### Scope

- enforce the Phase 1.16 discovery snapshot invariant that non-Supported model enumeration states cannot carry discovered models;
- validate discovery results at the Hive.Management boundary before caching or returning them;
- normalize conflicting provider fields that map to the same Hive capability conservatively as `Unknown`, independent of provider JSON field order;
- add focused regression coverage for contradictory/mismatched discovery results, conflicting capability signals, and concurrent discovery-cache access;
- preserve existing Provider → ProviderAccount → ExecutionTarget ownership, capability authority, caching, cancellation, and Example Host behavior.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

The 2026-09-28 verification failure was traced to malformed JSON in the revised regression-test fixture for `ProviderDiscoveryTests.Adapter_ListModels_NormalizesExplicitCapabilitiesAndMetadata`. The fixture has been corrected within this slice. The previous **419/420** result does not verify the corrected source; developer rerun is required.

The previous Phase 1.16 revision was fully verified before this corrective revision. This revision changes production code and tests, so the previous 418/418 result does not verify the revised source.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
