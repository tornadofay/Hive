# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision 5 — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 Revision 4. It does not advance the roadmap.

### Scope

- invalidate cached provider/model discovery observations when a referenced provider credential is successfully replaced through the Hive.Management facade;
- invalidate the discovery cache on cancellation-ambiguous credential replacement so a commit that may have completed cannot leave prior observations current;
- prevent an in-flight discovery started under the pre-replacement state from becoming the current cache entry after invalidation;
- preserve existing Provider → ProviderAccount → ExecutionTarget ownership, cache bounds/version semantics, capability authority, stale handling, cancellation, and Example Host behavior;
- add focused regression coverage for credential replacement and discovery-cache invalidation.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

The previous Phase 1.16 Revision 4 was fully verified at **426/426 passed, 0 failed, 0 skipped** with the Example Host scenario manually exercised successfully. This revision changes cache invalidation behavior and adds regression coverage, so previous verification does not verify the revised source.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
