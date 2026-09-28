# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

Developer verification reported active C# compiler errors in `src/Hive.Management/HiveManagementFacade.cs` (CS1002, CS1519, CS0411), all within the new facade operation-wrapper syntax.

## Current slice

### Maintenance — Backend: Review Follow-Up — Management Lifetime, Provider Dependency, and Forced Refresh Coalescing

Checkpoint: `9ebcf5a7e18376c495bf271bbd18063446783d60`

This is a bounded corrective maintenance slice opened from the immediately preceding repository-wide Review. It addresses the three concrete findings from that review without advancing the roadmap.

## Authorized scope

1. Correct `HiveManagementFacade` disposal so the entire public Management boundary has one consistent post-disposal lifetime contract while preserving the existing behavior that operations already in flight may finish.
2. Remove the unnecessary direct `Hive.Providers.OpenAICompatible` project dependency from `Hive.Management`; do not redesign the provider abstraction or transport boundary.
3. Coalesce concurrent forced provider-discovery refresh requests for the same discovery cache key so a successful refresh already performed by an overlapping forced request is reused rather than causing another provider request, while failed refreshes remain retryable and existing force-refresh semantics remain intact.
4. Add focused regression coverage for the three corrections, including disposal across representative Management domains and forced-refresh concurrency/failure behavior.
5. Update only the owning architecture/usage or verification documentation required to describe the corrected existing invariant.

## Explicit exclusions

- No Phase 1.16 UI implementation.
- No Phase 1.17 Structured Extraction & Validation.
- No new provider transport implementation.
- No public Management API redesign.
- No new persistence backend or vector database.
- No unrelated refactoring, dependency upgrades, or roadmap advancement.
- No material expansion of authorization or host-integration behavior.

## Verification boundary

Developer verification required before closure:
- focused tests covering Management facade disposal, provider dependency/build boundary, and concurrent forced discovery refresh behavior;
- full `Hive.Tests` suite;
- no manual UI verification is required unless the implementation changes user-visible behavior;
- Treat Warnings as Errors / zero-warning confirmation remains a developer-run verification item as part of the established project build configuration.

## Implementation state

Implementation is complete and the corrected result has been re-audited at source level. No developer verification is claimed yet.

## Verification handoff

Example to run: Not required — this maintenance slice changes internal lifecycle/dependency/concurrency behavior and does not add a new externally meaningful capability.

Tests to run: `tests/Hive.Tests/HiveManagementFacadeTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; `tests/Hive.Tests/Hive.Tests.csproj` full suite.
