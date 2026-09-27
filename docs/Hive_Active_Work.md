# Hive — Active Work

Status: VERIFICATION PENDING

Current slice: **Revision — Maintenance Host/UI: Host Integration Contract Corrections**

Revision scope:
- Re-audit the immediately preceding Host/UI contract-correction slice.
- Close the concrete same-slice test-coverage gaps identified during this revision:
  - explicit ReadControl reparenting/stale-ancestry regression;
  - Management authorization receives the originating ReadControl capture identity.
- Preserve existing production behavior and the closed slice's architecture, contracts, and scope.

Exclusions:
- No new host-integration capability.
- No material public-contract expansion beyond regression coverage.
- No unrelated refactoring, persistence/schema changes, UI redesign, or roadmap advancement.

Completed revision changes:
- Added ReadControl_RejectsReparentedControlWithoutRecapture to HiveWinFormsHostIntegrationTests.
- Extended the Management forwarding regression to assert the originating CaptureId reaches IHiveHostCapabilityAuthorizer.
- Updated the test fake to retain the observed capability-request capture identity.

Verification state:
- Revision implementation is complete.
- Developer re-verification is required for the focused host-integration tests and the full Hive.Tests suite.
- Do not modify implementation-affecting files while this gate is pending unless a real in-scope failure is recorded here first.

Tests to run:
- HiveHostIntegrationContractTests
- HiveWinFormsHostIntegrationTests
- full Hive.Tests suite
