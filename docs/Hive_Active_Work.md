# Hive — Active Work

Status: VERIFICATION PENDING

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
  - `BaseAgentWorkProtocolsTests`
- run the full `Hive.Tests` suite;
- manually launch `Hive.Example.WinForms` and verify the affected Host/UI behavior remains correct where applicable.

Verification state is pending because no builds/tests/manual launch were executed by the agent. No verification closure has been performed.

Developer verification failure received: `HiveWinFormsHostIntegrationTests.Capture_BindingInspectionFailureIsReportedInsteadOfReturningIncompleteSurface` initially failed with a raw `ArgumentException` from `DataGridView`/`BindingContext` access before the typed binding-inspection failure boundary. Same-slice remediation is complete: the full binding-inspection and bound-row-count paths are now inside their typed exception boundaries, with regression coverage for both. Developer verification also reported `CS1744` in `BaseAgentWorkProtocolsExampleView.cs` because the Example passed `clock` positionally before a named `delegation` argument after the compatibility-preserving signature reorder. Same-slice remediation corrected both Example call sites to `(now, delegation, clock)`.

The revision also corrected `Agent.CreateRuntimeInstance` so the new optional `IClock` parameter remains after the existing `IDelegationChannel` parameter, preserving positional-call compatibility; corrected the internal `RuntimeInstance.Create` parameter order for the same reason; completed the bound-row-count binding exception boundary; added duplicate business-operation capability-identity validation and coverage; and made an explicitly supplied runtime clock control the runtime creation timestamp as well as subsequent lifecycle timestamps.
