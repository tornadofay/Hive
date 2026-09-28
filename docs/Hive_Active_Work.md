# Hive — Active Work

Status: IN PROGRESS

## Current slice

### Maintenance — Backend: Provider Discovery Review Corrections

Checkpoint: `e5c381d6269e087b84e62479c6eed5af04095357`

This is a bounded corrective slice opened from the repository-wide Review findings. It restores/preserves existing Provider / ProviderAccount / ExecutionTarget discovery and input-routing behavior; it does not advance the roadmap.

### Authorized scope

1. Correct the ExecutionTarget endpoint security boundary so provider credentials cannot be supplied through credential-bearing query parameters while preserving legitimate non-secret query parameters.
2. Preserve actionable provider-discovery failure information during capability-aware input routing rather than collapsing provider operational failures into only a generic no-qualifying-target result, while still allowing independent targets to qualify.
3. Correct the current provider-discovery caching/routing shape where the same ProviderAccount + endpoint can cause repeated model-catalog discovery across multiple ExecutionTargets; keep target/model capability resolution and the existing authoritative ExecutionTargetSelector boundary intact.
4. Add focused regression coverage for security, failure classification/diagnostics, cancellation/stale handling where affected, cache reuse, and deterministic behavior.
5. Update the owning architecture/usage documentation only where the corrected behavior establishes or clarifies an existing invariant.

### Explicit exclusions

- No Phase 1.16 UI implementation.
- No Phase 1.17 Structured Extraction & Validation.
- No new provider transport implementation.
- No new vector database or persistence backend.
- No unrelated refactoring, dependency upgrades, or roadmap advancement.
- No material expansion of Hive's authorization model or host-integration surface.

### Verification boundary

Developer verification required before closure:
- focused provider/discovery/security/input-routing tests covering the corrected boundaries;
- full `Hive.Tests` suite;
- no manual UI verification required unless the implementation changes user-visible behavior;
- Visual Studio Treat Warnings as Errors / zero warning confirmation remains a developer-run verification item if applicable.

Until those results are reported, this slice remains open and must not be advanced to another roadmap slice.
