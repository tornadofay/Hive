# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

Current slice: Maintenance — Host/UI: Host Integration Contract Corrections

Scope completed:
- Corrected WinForms password-control capability advertisement so password fields do not advertise writable value capability.
- Unified data-surface field-override matching under a case-insensitive semantic key rule.
- Made ReadControl capture-bound in the Core request/capability contracts and WinForms execution path, including stale capture and stale target protection.
- Made IHiveHostIntegrationAdapter low-level operations explicit on the concrete WinForms adapter while preserving the neutral interface used by Management and custom adapters.
- Documented the Management authorization boundary versus adapter execution port in public API documentation and the owning architecture document.
- Added/updated focused regressions for capture requirements, stale reads, password capability advertisement, case-insensitive field overrides, and the concrete adapter public API shape.

Exclusions respected:
- No Phase 1.16+ or other roadmap advancement.
- No new host-integration capability beyond correcting existing behavior/contracts.
- No unrelated persistence, dependency, or UI redesign work.

Remediation completed:
- Removed optional parameter defaults from the three explicit `IHiveHostIntegrationAdapter` method implementations. Optional defaults remain on the public interface contract.
- Static API-surface reinspection confirms the three corrected explicit signatures are present.

Developer follow-up failure:
- Two `Hive.Tests` compile errors (`CS1061`) remain at lines 67 and 87 of `HiveWinFormsHostIntegrationTests.cs` because those tests still call `CaptureAsync` directly on the concrete adapter type. This is inside the same explicit-interface correction boundary.

Remediation completed:
- Routed the two remaining test `CaptureAsync` calls through the `IHiveHostIntegrationAdapter` interface helper.
- Reinspected the affected test file; no remaining concrete-adapter calls to `CaptureAsync`, `ExecuteInteractionAsync`, or `ResolveLookupAsync` remain.

Remediation completed:
- Restricted the explicit-interface reflection assertion to the three low-level adapter operations; `AdapterId` remains intentionally public.
- Changed the background-thread regression to capture and validate the host context before adding 600 traversal-padding controls, avoiding a test-side null capture caused by the bounded capture limit.
- Static reinspection confirms the corrected test paths and assertions are present.

Remediation target:
- Correct the xUnit `Assert.Contains` argument order in the explicit-adapter API regression test.
- Reinspect the corrected test and return to `VERIFICATION PENDING` with the exact developer rerun targets.

Verification target:
- Developer build with the repository's Treat-Warnings-as-Errors configuration.
- Focused tests covering:
  - HiveHostIntegrationContractTests
  - HiveWinFormsBaseControlIntegrationTests
  - HiveWinFormsHostIntegrationTests
- Full Hive.Tests suite.
- Existing relevant Hive.Example.WinForms host-integration scenario/manual launch.

Agent verification:
- Static repository/source review completed after remediation.
- Builds, tests, and manual Example Host execution were not run by the agent.
- Developer-reported CS1066 errors are remediated in-slice; no new verification result has been supplied yet.
