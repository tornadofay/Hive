# Hive — Active Work

Status: VERIFICATION PENDING

## Current slice

### Revision — Maintenance — Backend: Management Lifetime, Provider Dependency, and Forced Refresh Coalescing

Checkpoint: `46bf631b41ac7142926036ed2e64a36ff7ed5efa`

This Revision re-audits the immediately preceding bounded maintenance slice and corrects a concrete forced-refresh semantic gap found during the re-audit.

## Authorized scope

1. Preserve the completed Management facade disposal/lifetime correction unless the Revision identifies a concrete defect within that same boundary.
2. Preserve the completed Hive.Management dependency correction unless the Revision identifies a concrete defect within that same boundary.
3. Correct forced provider-discovery refresh coalescing so a forced request only reuses a successful overlapping **forced** refresh; an overlapping non-forced discovery must not satisfy `forceRefresh: true`.
4. Add focused regression coverage for the corrected forced/non-forced concurrency boundary and retain existing forced-refresh success/failure coverage.
5. Correct the verification archive metadata identified during the Revision audit.
6. Do not advance the roadmap or introduce unrelated refactoring/capability.

## Explicit exclusions

- No Phase 1.16 UI implementation.
- No Phase 1.17 Structured Extraction & Validation.
- No new provider transport implementation.
- No public Management API redesign.
- No new persistence backend or vector database.
- No unrelated dependency upgrades or refactoring.
- No new host/business capability.

## Verification boundary

Developer verification required before closure:
- `tests/Hive.Tests/HiveManagementFacadeTests.cs`;
- `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`;
- full `Hive.Tests`;
- established Treat Warnings as Errors / zero-warning build configuration remains applicable.

## Implementation state

Revision implementation is complete and the corrected forced/non-forced coalescing invariant plus archive metadata were re-audited at source level. Developer verification remains pending.