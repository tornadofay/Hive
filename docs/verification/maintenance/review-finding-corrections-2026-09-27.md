# Hive — Maintenance Review Finding Corrections Verification — 2026-09-27

## Scope

Bounded corrective maintenance for the four concrete findings identified by the 2026-09-27 repository Review:

1. workbook-wide and submission-wide spreadsheet preparation resource bounds;
2. WinForms semantic-provider data-surface identity enforcement;
3. sensitive WinForms field metadata and passive host-context/captured-value redaction;
4. case-insensitive parent/child field matching.

The slice also updated the affected architecture and public WinForms usage documentation.

Starting authorization checkpoint: `main @ 3bdaffc3754be471c2a52b27e7603bc7e56f355b`

No Phase 1.16+ work, provider/model discovery, persistence-schema/backend change, unrelated UI redesign, or branch/PR work was introduced.

## Corrective work completed

### Spreadsheet preparation resource bounds

- Added an aggregate uncompressed workbook-package budget of 64 MiB before XML loading.
- Preserved existing per-item, per-worksheet, cancellation, and failure-isolation limits.
- Added workbook-wide prepared-row and prepared-value-mapping budgets.
- Added submission-wide prepared-row and prepared-value-mapping budgets so multiple workbooks cannot collectively exceed the preparation output boundary.
- Committed a workbook's prepared-output usage to the submission budget only after that workbook parsed successfully, preserving failure isolation.
- Added focused regressions for aggregate uncompressed package expansion, workbook prepared-row output, and submission-wide prepared-row output.

### WinForms semantic-provider surface identity

- The capture service now requires a semantic provider to return the exact data-surface identity supplied by Hive.
- A mismatched returned identity fails with a structured integration error.
- Added focused regression coverage.

### Sensitive WinForms field semantics

- Added explicit `Sensitive` metadata to the existing WinForms field contract.
- Sensitive fields carry the marker into neutral host field descriptors while captured current values are forced to null.
- Passive host-context discovery redacts sensitive field text as `[redacted]`.
- Primary-key descriptor reconstruction preserves sensitivity.
- Preserved the original public `HiveHostFieldDescriptor` constructor shape and added compatibility regression coverage.

### Parent/child field matching

- Parent/child field-name matching is now case-insensitive.
- Surface identities remain case-sensitive and deterministic.
- Added focused regression coverage.

## Verification history

Developer verification exposed the following in-scope test-fixture problems during the slice:

- an earlier 379-test run reported 377 passed and 2 failed; the failures were corrected within the slice;
- a later 381-test run reported 380 passed and 1 failed because the submission-wide regression fixture supplied 19,999 prepared rows instead of exceeding the 20,000-row boundary.

The final fixture was corrected so four workbooks contribute 19,996 rows and the fifth contributes 5 rows, producing the intended 20,001-row boundary.

## Final developer verification

The developer ran the authoritative full `Hive.Tests` suite:

```
========== Starting test run ==========
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v3.1.5+1b188a7b0a (64-bit .NET 10.0.1)
[xUnit.net 00:00:00.39]   Starting:    Hive.Tests
[xUnit.net 00:00:35.13]   Finished:    Hive.Tests
========== Test run finished: 381 Tests (381 Passed, 0 Failed, 0 Skipped) run in 35.2 sec ==========
```

Result: **VERIFIED — 381 passed, 0 failed, 0 skipped.**

The final full-suite run includes the affected `InputPreparationTests`, `HiveWinFormsHostIntegrationTests`, and `HiveWinFormsHostContextTests`; no separate focused-run result was supplied.

No separate final Treat-Warnings-as-Errors result was supplied for this slice, so no additional warnings-as-errors verification claim is recorded.

## Closure

**Maintenance — Review Finding Corrections: Complete and verified.**

No Phase 1.16+ work was started or authorized.

Tests to run: focused `InputPreparationTests`, `HiveWinFormsHostIntegrationTests`, and `HiveWinFormsHostContextTests`; full `Hive.Tests` suite — completed through the final full-suite run with 381/381 passing.
