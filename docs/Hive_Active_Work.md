# Hive — Active Work

Status: IMPLEMENTING

Current slice: Maintenance — Host/UI: Host Integration Contract Corrections

Scope:
- Correct WinForms password-control capability advertisement so password fields never advertise unsupported value-setting.
- Align data-surface field-override lookup and validation to one case-insensitive semantic field-key rule.
- Make ReadControl capture-bound like other host-control interactions, including Core contract, Management routing, WinForms stale-target validation, tests, and Example usage.
- Make IHiveHostIntegrationAdapter low-level operations explicit on the concrete WinForms adapter to make accidental authorization bypass harder, while preserving the public neutral interface required by Management and custom adapters.
- Make the Management-vs-adapter authorization/execution trust boundary explicit in public API XML documentation and the owning architecture document.
- Add/update focused regressions and required public example/handoff documentation as applicable.

Exclusions:
- No Phase 1.16+ or other roadmap advancement.
- No new host-integration capability beyond correcting the existing contracts.
- No unrelated refactoring, dependency changes, persistence changes, or UI redesign.

Checkpoint:
- Main branch at the last closed maintenance commit.
- Verification has not started for this slice; developer build/test/manual verification results are required before closure.

Verification target:
- Focused Host/UI and host-integration contract tests, then the full Hive.Tests suite and the existing relevant Example Host scenario, with exact results recorded before closing the slice.

