# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

## Maintenance — Review Corrections

Opened: 2026-09-27

Scope: Correct the concrete production problems identified by the 2026-09-27 repository-wide Review, without advancing the roadmap or introducing new capability.

Completed implementation boundary:
- lookup authorization now carries and requires the specific `LookupId` for `ResolveLookup`;
- unused `ExpectedHostVersion` request input was removed; host operations may still return meaningful `HostVersion` evidence when supplied by the host;
- production time-sensitive boundaries use the existing injectable `IClock` with system-clock defaults;
- Agent lifecycle transitions preserve the injected clock across immutable state transitions;
- neutral host contracts reject duplicate semantic identities/capability IDs where ambiguity is invalid;
- WinForms binding-inspection failures now surface as typed capture failures instead of silently producing incomplete metadata;
- `HiveHostValue` DateTime serialization preserves the original `DateTimeKind` and no longer performs machine-local timezone conversion;
- focused regression tests cover lookup authorization context, DateTime round-tripping, host contract uniqueness, deterministic Agent lifecycle time, deterministic WinForms provenance time, and binding-inspection failure;
- UI test polling no longer uses arbitrary sleeps or wall-clock polling; it uses monotonic `Stopwatch` deadlines and yielding.

Explicit exclusions:
- no Phase 1.16+ implementation;
- no new host capabilities or new business workflow;
- no unrelated refactoring or dependency upgrades;
- no roadmap advancement.

Starting checkpoint: main @ a778b2414704d76190eedeba94560503c3702ffa

Implementation commits are now on `main`; current source is awaiting developer verification.

Required developer verification:
- build the affected solution/projects with warnings treated as errors;
- run the focused tests:
  - `HiveHostIntegrationContractTests`
  - `HiveWinFormsHostContextTests`
  - `HiveWinFormsHostIntegrationTests`
  - `AgentFactoryTests`
  - `HiveWorkspaceLifecycleTests`
  - `HiveUiPolishTests`
- run the full `Hive.Tests` suite;
- manually launch `Hive.Example.WinForms` and verify the affected Host/UI behavior remains correct where applicable.

Verification state is pending because no builds/tests/manual launch were executed by the agent.

Developer verification failure received: `HiveWinFormsHostIntegrationTests.Capture_BindingInspectionFailureIsReportedInsteadOfReturningIncompleteSurface` failed with a raw `ArgumentException` from `DataGridView`/`BindingContext` access before the typed binding-inspection failure boundary. Same-slice remediation is required. A review also identified the same uncovered `grid.BindingContext` access in `TryGetBoundRowCount` and will cover it with the same typed failure boundary.
