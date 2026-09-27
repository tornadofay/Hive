# Hive — Active Work

Status: IN PROGRESS

Current slice: Maintenance — Review Finding Corrections

## Checkpoint
Opened against main commit 3bdaffc3754b.

## Objective
Correct the four concrete production findings identified in the 2026-09-27 repository Review without advancing Phase 1.16 or introducing unrelated capability work.

## Scope
1. **Spreadsheet preparation resource bounds**
   - Add a workbook-wide uncompressed package byte budget.
   - Add workbook-wide prepared-row and prepared-value-mapping budgets.
   - Preserve per-item, per-worksheet, cancellation, and failure-isolation behavior.
   - Add focused boundary regressions.

2. **WinForms semantic-provider data-surface identity**
   - Enforce that a semantic provider returns the exact surface identity supplied by Hive.
   - Add a focused regression for mismatched returned IDs.

3. **Sensitive WinForms field semantics**
   - Add explicit sensitive-field metadata to the existing WinForms field contract.
   - Carry the sensitive marker into the neutral host field descriptor with redaction of captured current values.
   - Redact sensitive field-control text during host-context discovery.
   - Add focused regressions for custom field metadata and neutral descriptor behavior.

4. **Parent/child field matching**
   - Make parent/child field-name matching case-insensitive, consistent with existing field-override semantics.
   - Keep surface identities themselves case-sensitive/deterministic.
   - Add focused regression coverage.

5. Update relevant architecture/usage/verification documentation and preserve the existing roadmap boundary.

## Exclusions
- No Phase 1.16 implementation or roadmap advancement.
- No new provider/model discovery capability.
- No new persistence schema or backend.
- No unrelated host/UI redesign or refactoring.
- No branch/PR creation.

## Verification Gate
VERIFICATION PENDING — implementation and developer verification are complete only after the focused tests and full Hive.Tests suite are actually run. No execution is claimed by this agent.

## Handoff
Example to run: existing host/integration examples are unchanged by this maintenance slice; no new externally meaningful capability is introduced.
Tests to run: focused InputPreparationTests, HiveWinFormsHostIntegrationTests, and HiveWinFormsHostContextTests, followed by the full Hive.Tests suite.
