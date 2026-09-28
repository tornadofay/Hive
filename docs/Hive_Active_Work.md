# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision 4 — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 implementation. It does not advance the roadmap.

### Scope

- correct discovery endpoint identity comparison so scheme/host/port use URI authority semantics while HTTP path/query matching remains case-sensitive;
- centralize that endpoint identity rule in the existing Core ownership boundary without adding a public API;
- apply the same identity rule in Hive.Management discovery-result validation;
- add focused regression coverage for equivalent authority casing and distinct path casing;
- preserve the existing Provider → ProviderAccount → ExecutionTarget ownership boundary, cache/version semantics, capability authority, stale handling, cancellation, and Example Host behavior.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

The previous Phase 1.16 Revision 3 was fully verified at **422/422 passed, 0 failed, 0 skipped** with the Example Host scenario manually exercised successfully. This revision changes endpoint identity handling and adds regression coverage, so the previous verification does not verify the revised source.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
