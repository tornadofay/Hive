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
- Two `Hive.Tests` compile errors (`CS1061`) remained at lines 67 and 87 of `HiveWinFormsHostIntegrationTests.cs` because those tests still called `CaptureAsync` directly on the concrete adapter type. This is inside the same explicit-interface correction boundary.

Remediation completed:
- Routed the two remaining test `CaptureAsync` calls through the `IHiveHostIntegrationAdapter` interface helper.
- Reinspected the affected test file; no remaining concrete-adapter calls to `CaptureAsync`, `ExecuteInteractionAsync`, or `ResolveLookupAsync` remain.

Remediation completed:
- Restricted the explicit-interface reflection assertion to the three low-level adapter operations; `AdapterId` remains intentionally public.
- Changed the background-thread regression to capture and validate the host context before adding 600 traversal-padding controls, avoiding a test-side null capture caused by the bounded capture limit.
- Static reinspection confirms the corrected test paths and assertions are present.

Remediation completed:
- Corrected the xUnit `Assert.Contains` argument order in the explicit-adapter API regression test.
- Static reinspection confirms the assertion now uses the expected-item-first overload.

Developer verification failure:
- Full `Hive.Tests` run completed with 372 tests: 371 passed, 1 failed.
- `HiveWinFormsHostIntegrationTests.AdapterLowLevelOperationsAreExplicitInterfaceImplementations` failed because the reflection regression filtered `InterfaceMap.TargetMethods` by exact `MethodInfo.Name`, but explicit-interface target names are qualified and therefore produced zero matches instead of the expected three.
- The failure is confined to the same explicit-interface API-shape regression and does not establish a production implementation failure.

Remediation required:
- Change the reflection regression to correlate `InterfaceMap.InterfaceMethods` with `TargetMethods` by index (or equivalent interface-method identity) and assert privacy only for the three named low-level interface operations.
- Preserve the existing public `AdapterId` assertion.
- Return this document to `VERIFICATION PENDING` after the corrective test change.

Verification target:
- Developer build with the repository's Treat-Warnings-as-Errors configuration.
- Focused tests covering:
  - HiveHostIntegrationContractTests
  - HiveWinFormsBaseControlIntegrationTests
  - HiveWinFormsHostIntegrationTests
- Full Hive.Tests suite.
- Existing relevant Hive.Example.WinForms host-integration scenario/manual launch.

Agent verification:
- Static repository/source review completed after the reported failure.
- Builds, tests, and manual Example Host execution were not run by the agent.
- Developer-reported 371/372 result is recorded as the current verification failure.
