# Phase 1.4 — Capability-aware Execution Target Selection

Select an ExecutionTarget using explicit capability requirements and a selection mode.

```csharp
var result = ExecutionTargetSelector.Select(
    new ExecutionTargetSelectionRequest(
        targets,
        [
            new CapabilityRequirement(
                new CapabilityKey("text.generate"),
                CapabilityRequirementKind.Required),
            new CapabilityRequirement(
                new CapabilityKey("vision"),
                CapabilityRequirementKind.Preferred),
            new CapabilityRequirement(
                new CapabilityKey("structured.output"),
                CapabilityRequirementKind.Forbidden)
        ],
        ExecutionTargetSelectionMode.Auto));
```

Selection rules:

- `Required` accepts only `Supported`; `Unsupported` and `Unknown` reject the target.
- `Forbidden` accepts only an explicitly `Unsupported` capability. `Supported` and `Unknown` reject the target.
- `Preferred` and `Optional` do not reject a target. Supported preferred/optional capabilities contribute to deterministic ranking.
- `Auto` selects the highest-scoring qualifying target, then orders equal scores by target key and identity.
- `Preferred` selects the specified preferred target when it qualifies; otherwise it falls back to the best qualifying target.
- `Fixed` selects only the specified target and never falls back.
- Missing capability evidence is treated as `Unknown`.
- Selection diagnostics contain target identity/key/name, status, score, and capability-policy reasons. They do not include endpoint or credential material.
- `ExecutionTargetCostPolicy` is carried separately from capability requirements. This slice does not infer target cost when no cost evidence is present.

Fixed-target failures and no-qualifying-target results are returned as typed `Result<T>` failures.
