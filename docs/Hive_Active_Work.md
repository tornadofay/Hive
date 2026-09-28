# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision 3 — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 implementation. It does not advance the roadmap.

### Scope

- make discovery endpoint identity path-sensitive at both the Core capability-resolution boundary and the Hive.Management result-validation boundary;
- prevent a discovery result for a path such as `/v1/Models` from being treated as metadata for `/v1/models`;
- add focused regression coverage for endpoint path identity at both boundaries;
- preserve the existing Provider → ProviderAccount → ExecutionTarget ownership boundary, cache/version semantics, capability authority, stale handling, cancellation, and Example Host behavior.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

The previous Phase 1.16 Revision 2 was fully verified at **420/420 passed, 0 failed, 0 skipped** with the Example Host scenario manually exercised successfully. This revision changes production endpoint identity behavior and adds regression coverage, so the previous verification does not verify the revised source.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
