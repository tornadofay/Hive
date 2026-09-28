# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision 7 — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 Revision 6. It does not advance the roadmap.

### Scope

- remove the remaining direct wall-clock reads from the Phase 1.16 Management capability-resolution path;
- ensure stale re-checking and effective discovered-capability resolution use the same injectable IClock boundary already established by Revision 6;
- add focused regression coverage proving injected-clock behavior remains authoritative after stale discovery refresh;
- preserve Provider → ProviderAccount → ExecutionTarget ownership, discovery/cache semantics, capability authority, stale handling, cancellation, credential isolation, and Example Host behavior.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

Revision 6 was completed and verified at **431/431 passed, 0 failed, 0 skipped**, with the matching Provider / Model Capability Discovery Example Host scenario manually exercised successfully.

Revision 7 is limited to the concrete remaining time-source inconsistency found during re-audit: `GetExecutionTargetCapabilityOverridesAsync` still used direct `DateTimeOffset.UtcNow` after stale refresh and during effective capability resolution.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; `tests/Hive.Tests/ProviderDiscoveryTests.cs`; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
