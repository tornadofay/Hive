# Hive — Active Work

Status: IN PROGRESS

## Current slice

**Phase 1.16 Revision 6 — Provider / Model Capability Discovery & Operational Metadata**

This is a bounded corrective revision of the immediately preceding Phase 1.16 Revision 5. It does not advance the roadmap.

### Scope

- make capability-key identity canonical at the existing Core `CapabilityKey` ownership boundary so configured and discovered semantic keys compare consistently;
- preserve configured ExecutionTarget capability authority when a configured capability uses different casing from the normalized discovered key;
- reject duplicate configured capability declarations that differ only by casing;
- add focused regression coverage for canonical capability-key identity and configured Unsupported override authority;
- preserve Provider → ProviderAccount → ExecutionTarget ownership, discovery/cache semantics, stale handling, cancellation, credential isolation, and Example Host behavior.

### Out of scope

No new provider transport, capability vocabulary, workflow behavior, structured extraction/validation, business-app write, Tool authorization, Review, cognition, Workspace expansion, or Phase 1.17 work.

## Verification boundary

**VERIFICATION PENDING.**

The previous Phase 1.16 Revision 5 was fully verified at **428/428 passed, 0 failed, 0 skipped** with the matching Provider / Model Capability Discovery Example Host scenario manually exercised successfully. This revision changes capability-key normalization and adds regression coverage, so previous verification does not verify the revised source.

### Handoff

Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Capability Discovery — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderDiscoveryTests.cs`; focused provider-resource capability normalization coverage; full `Hive.Tests` suite; then re-exercise the matching Example Host scenario.

No Phase 1.17 work is authorized or started.
