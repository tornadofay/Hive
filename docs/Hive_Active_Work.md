# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED

## Authorized revision

**Revision — Phase 1.16 UI — Provider Configuration, Discovery & Target Reconciliation**

This revision re-audits the immediately preceding Phase 1.16 UI slice and corrects only concrete same-slice defects.

## Verification failure / remediation

Developer verification reported that `ProviderSettingsIntegrationTests` hangs and does not terminate. The newly added no-credential presentation test was narrowed to deterministic in-memory Management data. Developer verification then reported two concrete test defects: the new `ProvidersSettingsManagementProxy` was declared `sealed`, which `DispatchProxy` rejects, and the new-target management-mode test clicked Save without populating the form's required endpoint/model/name/key fields, causing the UI error path to block the test. Both defects are limited to the focused regression tests.

## Revision findings and remediation

1. The Advanced Execution Target editor exposed the Automatic / Manual management selector but ignored the selection when creating a new ExecutionTarget. New targets now apply the selected durable management mode.
2. The normal Provider Settings row labeled providers without credentials as `Not configured`. Catalog-defined no-credential providers now display `Not required`.
3. The shared CRUD Add action was hard-coded as `Add`, while the Provider Settings requirement calls for `Add Provider`. The shared CRUD page now supports a bounded Add-action caption customization, and the normal Providers page uses `Add Provider`.
4. The Add Provider dialog detail strings contained literal `\\r\\n` sequences rather than actual line breaks. They now use real environment line breaks.

## Scope

- no Management, persistence, provider transport, or public domain architecture changes;
- no new roadmap capability;
- no Phase 1.17 work;
- no background discovery scheduling;
- no Agent target-selection work;
- focused regression coverage added for the corrected UI behaviors.

## Verification handoff

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms
Tests to run: ProviderSettingsIntegrationTests.cs; HiveUiPolishTests.cs; full Hive.Tests suite

Developer verification failure was recorded before remediation. The following test corrections are authorized within the same revision boundary; agent did not run a build, test suite, or application launch.