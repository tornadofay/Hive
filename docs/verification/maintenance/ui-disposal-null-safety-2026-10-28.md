# Hive — Maintenance UI: Disposal and Null-Safety Hardening — 2026-10-28

## Scope

Temporary corrective **Maintenance — UI** slice: production audit of the reusable WinForms control library (`Hive.Host.WinForms.UI`) focused on GDI resource disposal ordering and null-safety in `Dispose` paths. No new capability, public API, persistence behavior, or roadmap advancement was authorized.

## Corrective work

- `HiveForm.Dispose`: moved `_formFont` disposal into the `if (disposing)` block before `base.Dispose(disposing)` and guarded it with `?.`, nulling the field afterward. Previously the font was disposed after the base control graph, which is inconsistent with the rest of the disposal sequence.
- `HiveWindowHeader.Dispose`: guarded `_titleFont`, `_subtitleFont`, and `_buttonFont` disposal with `?.` for null-safety on repeated/disposed-state disposal.
- `HiveMessageBox` (`HiveMessageDialog`): guarded `_titleFont`, `_messageFont`, `_detailsFont`, and `_buttonFont` disposal with `?.`.

## Developer-reported verification (2026-10-28)

- **Build:** `Hive.Host.WinForms.UI` and `Hive.Host.WinForms` succeeded with 0 warnings/errors.
- **Automated:** `Hive.Tests` **818/818 passing (0 failed, 0 skipped)**, ~3.5 min, .NET 10.0.1.
- **Manual:** Workspace → Direct LLM and Work Items surfaces exercised with light/dark themes; dialog presentation verified.

## Scope outcome

**Closed — verification passed.** The temporary Maintenance — UI slice is closed. No roadmap phase was activated. Phase 1.20 Slice 4 remains not started and requires explicit `Hive: Start next slice` authorization.