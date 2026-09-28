# Hive — Active Work

Status: VERIFICATION PENDING

## Current slice

### Maintenance — Backend: Provider Discovery Review Corrections

Checkpoint: `e144b9ea2d0637a0cd8b490310a4452a1de3aa2f0`

This is a bounded corrective slice opened from the repository-wide Review findings. It restores/preserves existing Provider / ProviderAccount / ExecutionTarget discovery and input-routing behavior; it does not advance the roadmap.

### Authorized scope

1. Correct the ExecutionTarget endpoint security boundary so provider credentials cannot be supplied through credential-bearing query parameters while preserving legitimate non-secret query parameters.
2. Preserve actionable provider-discovery failure information during capability-aware input routing rather than collapsing provider operational failures into only a generic no-qualifying-target result, while still allowing independent targets to qualify.
3. Correct the current provider-discovery caching/routing shape where the same ProviderAccount + endpoint can cause repeated model-catalog discovery across multiple ExecutionTargets; keep target/model capability resolution and the existing authoritative ExecutionTargetSelector boundary intact.
4. Add focused regression coverage for security, failure classification/diagnostics, cancellation/stale handling where affected, cache reuse, and deterministic behavior.
5. Normalize the duplicate roadmap heading for the completed Provider / Model Discovery slice and planned UI follow-on without changing roadmap order or authorization.
6. Update the owning architecture/usage documentation only where the corrected behavior establishes or clarifies an existing invariant.

### Explicit exclusions

- No Phase 1.16 UI implementation.
- No Phase 1.17 Structured Extraction & Validation.
- No new provider transport implementation.
- No new vector database or persistence backend.
- No unrelated refactoring, dependency upgrades, or roadmap advancement.
- No material expansion of Hive's authorization model or host-integration surface.

## Verification boundary

Developer verification required before closure:
- focused provider/discovery/security/input-routing tests covering the corrected boundaries;
- full `Hive.Tests` suite;
- no manual UI verification required unless the implementation changes user-visible behavior;
- Visual Studio Treat Warnings as Errors / zero warning confirmation remains a developer-run verification item if applicable.

## Implementation state

The bounded corrective implementation is complete and the result has been re-audited at source level. No further implementation changes are authorized while this slice is at the verification gate.

## Verification handoff

Example to run: Settings / Providers / Execution Targets — Execution Target endpoint validation and capability routing — Hive.Example.WinForms

Tests to run: `tests/Hive.Tests/ProviderResourceTests.cs`; `tests/Hive.Tests/ProviderDiscoveryTests.cs`; `tests/Hive.Tests/ProviderDiscoveryManagementIntegrationTests.cs`; then the full `Hive.Tests` suite.

Automated verification reported:
- Full `Hive.Tests`: **452 Tests (452 Passed, 0 Failed, 0 Skipped)** in 57.5 seconds.
- Runtime: .NET 10.0.1.
- The supplied full-suite result covers the focused provider/discovery/security/input-routing tests within the authoritative `Hive.Tests` project.

Manual verification remains outstanding because this corrective implementation changes the existing Execution Target validation/error behavior:
- an ExecutionTarget endpoint containing credential-bearing query parameters is rejected and reported through the existing UI error path;
- a non-secret provider query such as `api-version` remains accepted;
- configured vision capability does not require provider discovery;
- image routing preserves the provider discovery error when discovery prevents a target from being evaluated;
- equivalent Provider + ProviderAccount + endpoint targets reuse one discovery request.

Visual Studio Treat Warnings as Errors / zero-warning confirmation has not been separately reported for this corrective slice.

Do not record closure until the outstanding developer verification results are actually supplied.

Until those results are reported, this slice remains open and must not be advanced to another roadmap slice.
