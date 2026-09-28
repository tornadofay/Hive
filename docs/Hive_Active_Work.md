# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision 5 — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 Revision 4. It does not advance the roadmap.

### Scope

- align the process-local discovery cache key with the established provider endpoint identity semantics so identity-equivalent URIs do not create duplicate cache entries;
- preserve the existing Provider → ProviderAccount → ExecutionTarget ownership boundary, resource-version cache partitioning, capability authority, stale handling, cancellation, and Example Host behavior;
- add focused regression coverage for identity-equivalent endpoint cache reuse, including URI fragment differences that do not participate in endpoint identity.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

The previous Phase 1.16 Revision 4 was fully verified at **426/426 passed, 0 failed, 0 skipped** with the Example Host scenario manually exercised successfully. This revision changes cache-key construction and adds regression coverage, so previous verification does not verify the revised source.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
