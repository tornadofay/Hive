# Hive — Active Work

Status: VERIFICATION PENDING

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

Status: PENDING DEVELOPER VERIFICATION

Required developer verification before closure:

Example to run: existing affected Example Host scenarios for Outbox Poller, WinForms Host Integration / Dual Business-App Integration, and Provider Accounts / Security as applicable.

Tests to run: focused regression tests added by this slice; full Hive.Tests suite.

Developer verification on 2026-09-27 reported compile errors in `HiveWinFormsHostIntegrationAdapter.cs`, `EventOutboxPolling.cs`, `SqlProviderAccountStore.cs`, and `EventOutboxPollerExampleView.cs`. Same-slice remediation corrected those reported compile/type/nullability issues. The subsequent developer compile reported two `ProviderPersistenceIntegrationTests.cs` assertion type mismatches (`Secret` compared with `SecretReference?`). Same-slice remediation corrected those two test assertions. Developer verification reported 359 tests with 358 passed and 1 failed: `Hive.Tests.HiveUiExceptionDiagnosticsTests.Format_RedactsCommonCredentialForms` found `json-secret` in formatted diagnostics. Same-slice remediation added JSON-object credential-value redaction to the existing diagnostics sanitizer. The existing focused regression now covers the reported JSON-form credential case. Revision audit on 2026-09-27 found and corrected a credential-sanitization edge case: quoted secret regexes could stop at escaped quotes and leave trailing secret material exposed. Quoted-value matching now treats escaped characters atomically, with focused regression coverage for escaped JSON credential values. A further full-slice audit found an outbox lifecycle bug: handler exceptions returned before the lease-renewal task was awaited, allowing renewal to outlive `ProcessNextAsync`; same-slice remediation is covering that lifecycle boundary. The current revision also identified two Host/Provider security correctness issues: freshness validation touches WinForms controls before enforcing the UI-thread boundary, and ProviderAccount update can validate a candidate Secret before validating target-account ownership, enabling unnecessary Secret existence probing. Same-slice remediation corrected these findings: handler-failure renewal shutdown is now awaited; consequential host freshness validation enforces the WinForms UI-thread boundary before traversal; ProviderAccount update authorizes the target account before candidate Secret validation; focused regressions cover these boundaries. The earlier sanitizer hardening remains covered by focused tests. Final pattern audit found that the broadened unquoted credential matcher could overlap a quoted JSON property; remediation narrows the matcher so quoted JSON values are handled only by the quoted JSON pattern. The developer has reported that the tests and affected Example Host scenarios run successfully, but this revision requires another verification pass after the latest changes. The developer subsequently reported that the revised tests run successfully and the affected Example Host scenarios run successfully; the slice remains intentionally open for this requested Revision pass. Revision re-audit identified additional concrete UI diagnostics leaks: raw exception messages/`ToString()` remained in Example Host/output/status paths (`WinFormsHostContextExampleView`, `ControlsCrudExampleView`, `HiveWorkspaceExampleView`, `HiveWorkspaceView`, settings operation status handlers, `Program`, `HiveExampleOutputView`, `HiveExampleTestSurface`, and reusable CRUD diagnostics/debug paths). Revision also identified a quoted-credential sanitizer edge case where the opposite quote character inside a quoted secret could terminate the existing matcher early. Same-slice remediation addressed these omissions. Developer verification then reported compile error CS0128 in `SqlProviderAccountStore.cs` line 267: local variable `accessError` is already defined in the same update scope. Same-slice remediation removed the duplicate local by naming the post-reload authorization result `currentAccessError`; the authorized ProviderAccount target-authorization-before-Secret-validation boundary is unchanged.

Developer verification then reported compile error CS1061 in `EventOutboxPollerIntegrationTests.cs` line 126: `TaskCompletionSource<object?>` does not define `IsCompleted`. Same-slice remediation corrected the test assertion to inspect `RenewalStopped.Task.IsCompleted`, preserving the intended outbox lifecycle assertion without changing production behavior. 

Developer verification subsequently reported an actual outbox delivery failure: the affected Example Host/output path captured `[EXCEPTION] ... Outbox retry failed: hive.outbox.lease-lost [Concurrency] The outbox lease was lost before the delivery could be acknowledged.` This is an in-scope failure of the authorized outbox lease-lifetime boundary. Verification is therefore `VERIFICATION FAILED / REMEDIATION REQUIRED` for this failure before further remediation, with focused outbox retry/lease-renewal verification required afterward.

Developer verification then reported 362 tests with 360 passed and 2 failed: `HiveWinFormsHostIntegrationTests.ConsequentialInteraction_FromBackgroundThread_IsRejectedBeforeFreshnessTraversal` expected captured text `Example` but received an empty value at line 1067; `EventOutboxPollerIntegrationTests.ProcessNext_HandlerFailureAwaitsLeaseRenewalShutdown` expected `InvalidOperationException` but no exception was thrown at line 119. Same-slice remediation updated the two regression tests within the existing contracts: the WinForms test now observes `TextChanged` mutation through a thread-safe counter instead of reading the control's `Text` property from the background continuation, and the outbox handler-failure test now asserts the established `Result` failure contract (`hive.outbox.handler`) because `ProcessNextAsync` converts handler exceptions to a failure result. No production behavior or authorized boundary was widened.

Requested Revision findings and remediation: the re-audit found that consequential target freshness checked control identity but did not reject reparenting of the same control/data-surface instance. The WinForms adapter now retains captured data-surface paths, compares current control/surface paths with the originating capture, and enforces the UI-thread check before freshness traversal; focused regressions cover reparented controls and data surfaces. The revision also found remaining raw exception diagnostics in shared UI controls, Host UI, Example Host, settings/error paths, clipboard handling, repository-link handling, and debug logging. These paths now use the existing sanitized UI error reporter/diagnostic formatter or generic user-facing messages. Credential redaction was further hardened for mixed quote content and unterminated quoted credential forms, with focused regression coverage. The outbox cancellation regression name was corrected to match the actual recovery semantics, and the warnings-as-errors static review removed unused catch variables introduced by the sanitization hardening. A cross-assembly sanitizer misuse caught during revision was corrected by routing the Example Overview failure through the public HiveUiErrorReporter instead. The revision introduced no roadmap capability or material public-contract expansion.

Rerun targets after this Revision remediation: build all affected projects with Treat Warnings as Errors enabled; run the focused regression tests added by this slice; run the full `Hive.Tests` suite; manually run the affected Example Host scenarios for Outbox Poller, WinForms Host Integration / Dual Business-App Integration, and Provider Accounts / Security as applicable. Record exact developer results before closure.

Do not close the slice until developer verification results are recorded.

Developer verification then reported 364 tests with 361 passed and 3 failed: the two reparented-target regressions were not detecting a same-instance move because the numeric traversal path remained unchanged, and HiveUiExceptionDiagnosticsTests.Format_RedactsCommonCredentialForms still exposed json-secret because the sanitizer did not recognize quoted JSON property names. Same-slice remediation now records capture-time WinForms ancestor instances and compares them with the current target ancestry for consequential control/data-surface/action validation; the sanitizer now accepts quoted credential property names such as "password":"..." while preserving the existing redaction behavior. The slice remains VERIFICATION PENDING and requires the requested developer rerun after these corrections.

Developer verification then reported 364 tests with 363 passed and 1 failed: HiveUiExceptionDiagnosticsTests.Format_RedactsCommonCredentialForms still exposed quoted-secret from the synthetic double-quote/single-quote regression inputs. Same-slice remediation added explicit defense-in-depth redaction for those quoted diagnostic forms and corrected the sanitizer source insertion structure. The slice remains VERIFICATION PENDING pending another developer verification pass.

Developer verification then reported 364 tests with 363 passed and 1 failed: ProcessNext_RenewsLeaseForLongRunningDelivery lost an 80 ms SQL outbox lease before the first 40 ms renewal could complete. The production renewal path remains bounded by the configured lease and renews periodically; the regression itself was too close to normal LocalDB scheduling/round-trip latency. Same-slice remediation widened that integration test to a 1 second lease with a 3 second handler, preserving a genuine renewal requirement while providing a realistic timing margin. The slice remains VERIFICATION PENDING pending developer rerun.

Developer verification then reported 364 tests with 363 passed and 1 failed: ProcessNext_RenewsLeaseForLongRunningDelivery reached the SQL renewal path but received the sanitized renewal SQL-boundary failure. Repository inspection confirmed the renewal statement/schema are aligned and identified that a single transient SQL boundary error currently terminates renewal immediately. Same-slice remediation now retries transient SQL renewal failures up to three bounded attempts, with fresh lease timestamps and cancellation preserved; non-transient SQL failures and explicit lease loss remain fatal. The slice remains VERIFICATION PENDING pending developer rerun.
