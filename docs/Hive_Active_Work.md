# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

Slice: Maintenance — Review Medium-Finding Corrections

Opened: 2026-09-27

Checkpoint: 7d84a68f7101df715d429b5d55c6770aff389e68

## Authorized scope

Correct the four concrete medium-severity findings from the repository review immediately preceding this slice:

1. Outbox lease lifetime — prevent a long-running delivery from silently losing its lease while the handler is still executing; preserve at-least-once/idempotent delivery semantics.
2. WinForms host interaction freshness — bind consequential interaction requests to the host capture they came from so stale captured authorization cannot mutate a replaced/recreated current control or semantic surface.
3. UI exception diagnostics — prevent raw exception details from exposing credential/connection/secret material through the Output panel or user-visible technical details.
4. ProviderAccount credential references — prevent ProviderAccount records from accepting unresolved Secret references and prevent referenced secrets from being hard-deleted while still in use.

## Boundaries

- Backend/Persistence and Host/UI corrective work only.
- Preserve existing V1 architecture, Management ownership, host-neutral contracts, and at-least-once outbox semantics.
- No new roadmap capability, provider transport, MAF/orchestration work, business write implementation, or cognitive work.
- Public contract changes are allowed only where directly required to close the identified existing correctness/security boundary; do not widen them beyond that purpose.
- Add focused regression coverage for each corrected failure mode.
- Work directly on main; no feature branch or PR.

## Implementation targets

- Hive.Persistence outbox claim/renew/complete boundary and poller lifecycle.
- Hive.Core / Hive.Management / Hive.Host.WinForms interaction freshness contract and enforcement.
- Hive.Host.WinForms.UI exception-diagnostics sanitization boundary.
- Hive.Persistence ProviderAccount/Secret reference integrity.
- Relevant tests and verification documentation.

## Verification

Status: REMEDIATION REQUIRED — REVISION FINDINGS

Required developer verification before closure:

Example to run: existing affected Example Host scenarios for Outbox Poller, WinForms Host Integration / Dual Business-App Integration, and Provider Accounts / Security as applicable.

Tests to run: focused regression tests added by this slice; full Hive.Tests suite.

Developer verification on 2026-09-27 reported compile errors in `HiveWinFormsHostIntegrationAdapter.cs`, `EventOutboxPolling.cs`, `SqlProviderAccountStore.cs`, and `EventOutboxPollerExampleView.cs`. Same-slice remediation corrected those reported compile/type/nullability issues. The subsequent developer compile reported two `ProviderPersistenceIntegrationTests.cs` assertion type mismatches (`Secret` compared with `SecretReference?`). Same-slice remediation corrected those two test assertions. Developer verification reported 359 tests with 358 passed and 1 failed: `Hive.Tests.HiveUiExceptionDiagnosticsTests.Format_RedactsCommonCredentialForms` found `json-secret` in formatted diagnostics. Same-slice remediation added JSON-object credential-value redaction to the existing diagnostics sanitizer. The existing focused regression now covers the reported JSON-form credential case. Revision audit on 2026-09-27 found and corrected a credential-sanitization edge case: quoted secret regexes could stop at escaped quotes and leave trailing secret material exposed. Quoted-value matching now treats escaped characters atomically, with focused regression coverage for escaped JSON credential values. A further full-slice audit found an outbox lifecycle bug: handler exceptions returned before the lease-renewal task was awaited, allowing renewal to outlive `ProcessNextAsync`; same-slice remediation is covering that lifecycle boundary. The current revision also identified two Host/Provider security correctness issues: freshness validation touches WinForms controls before enforcing the UI-thread boundary, and ProviderAccount update can validate a candidate Secret before validating target-account ownership, enabling unnecessary Secret existence probing. Same-slice remediation corrected these findings: handler-failure renewal shutdown is now awaited; consequential host freshness validation enforces the WinForms UI-thread boundary before traversal; ProviderAccount update authorizes the target account before candidate Secret validation; focused regressions cover these boundaries. The earlier sanitizer hardening remains covered by focused tests. Final pattern audit found that the broadened unquoted credential matcher could overlap a quoted JSON property; remediation narrows the matcher so quoted JSON values are handled only by the quoted JSON pattern. The developer has reported that the tests and affected Example Host scenarios run successfully, but this revision requires another verification pass after the latest changes. The developer subsequently reported that the revised tests run successfully and the affected Example Host scenarios run successfully; the slice remains intentionally open for this requested Revision pass. Revision re-audit identified additional concrete UI diagnostics leaks: raw exception messages/`ToString()` remained in Example Host/output/status paths (`WinFormsHostContextExampleView`, `ControlsCrudExampleView`, `HiveWorkspaceExampleView`, `HiveWorkspaceView`, settings operation status handlers, `Program`, `HiveExampleOutputView`, `HiveExampleTestSurface`, and reusable CRUD diagnostics/debug paths). Revision also identified a quoted-credential sanitizer edge case where the opposite quote character inside a quoted secret could terminate the existing matcher early. Same-slice remediation is required for these omissions. Developer verification then reported compile error CS0128 in `SqlProviderAccountStore.cs` line 267: local variable `accessError` is already defined in the same update scope. Same-slice remediation removed the duplicate local by naming the post-reload authorization result `currentAccessError`; the authorized ProviderAccount target-authorization-before-Secret-validation boundary is unchanged.

Developer verification then reported compile error CS1061 in `EventOutboxPollerIntegrationTests.cs` line 126: `TaskCompletionSource<object?>` does not define `IsCompleted`. Same-slice remediation corrected the test assertion to inspect `RenewalStopped.Task.IsCompleted`, preserving the intended outbox lifecycle assertion without changing production behavior. Developer verification then reported 362 tests with 360 passed and 2 failed: `HiveWinFormsHostIntegrationTests.ConsequentialInteraction_FromBackgroundThread_IsRejectedBeforeFreshnessTraversal` expected captured text `Example` but received an empty value at line 1067; `EventOutboxPollerIntegrationTests.ProcessNext_HandlerFailureAwaitsLeaseRenewalShutdown` expected `InvalidOperationException` but no exception was thrown at line 119. Same-slice remediation updated the two regression tests within the existing contracts: the WinForms test now observes `TextChanged` mutation through a thread-safe counter instead of reading the control's `Text` property from the background continuation, and the outbox handler-failure test now asserts the established `Result` failure contract (`hive.outbox.handler`) because `ProcessNextAsync` converts handler exceptions to a failure result. No production behavior or authorized boundary was widened.

Rerun targets after this developer-test-failure remediation: build all affected projects with Treat Warnings as Errors enabled; run the focused regression tests added by this slice; run the full `Hive.Tests` suite; manually run the affected Example Host scenarios for Outbox Poller, WinForms Host Integration / Dual Business-App Integration, and Provider Accounts / Security as applicable. Record exact developer results before closure.

Do not close the slice until developer verification results are recorded.