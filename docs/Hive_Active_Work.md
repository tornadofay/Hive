# Hive — Active Work

Status: VERIFICATION PENDING

## Current slice

**Maintenance — Host/UI: WinForms Standard Control Value Adaptation Refactor**

## Authorization

Bounded corrective maintenance. Extract the existing standard WinForms control value adaptation mechanism from `HiveWinFormsHostIntegrationAdapter` into small internal control-family adapters so the existing behavior remains unchanged while future standard-control additions remain localized. No public contract expansion is authorized.

## Scope

- Extract only the standard value adaptation responsibilities currently concentrated in `HiveWinFormsHostIntegrationAdapter`.
- Keep the existing neutral host-integration contracts and public adapter type unchanged.
- Keep capability identity generation, authorization, host discovery, data-surface semantics, lookup behavior, lifecycle, disposal, cancellation, and threading boundaries unchanged.
- Preserve current supported standard controls and their existing read/write/type/read-only behavior:
  - `TextBoxBase`
  - `CheckBox`
  - `ComboBox`
  - `DateTimePicker`
  - `NumericUpDown`
- Preserve password-field redaction and existing bounded range validation for date/time and numeric controls.
- Use only internal implementation contracts/classes; do not create one wrapper per WinForms control merely for size.
- Add focused regression coverage only where needed to prove the extraction preserves existing behavior.
- No new control support, schema change, roadmap advancement, or unrelated WinForms refactor.

## Checkpoint

Implementation complete. `HiveWinFormsHostIntegrationAdapter` retains host integration orchestration, capture/discovery, identity, data-surface handling, provider delegation, capability identity validation, lifecycle, and authorization-facing boundaries. Standard control value mechanics now live in internal control-family adapters without changing the public host integration contracts or capability identity rules. Focused regression coverage now exercises Boolean, editable ComboBox, NumericUpDown, DropDownList capability behavior, and NumericUpDown range validation in addition to the existing text, password, and DateTimePicker coverage.

## Design boundary

The main `HiveWinFormsHostIntegrationAdapter` remains responsible for host integration orchestration, capture/discovery, identity, data-surface handling, provider delegation, capability identity validation, lifecycle, and authorization-facing boundaries.

An internal standard-value adaptation layer owns:

```text
control support detection
value type projection
read-only detection
current-value reading
bounded value writing/validation
```

Control-family adapters remain internal to `Hive.Host.WinForms`.

## Example to run

None — this is an internal implementation refactor with no new externally usable capability.

## Tests to run

`tests/Hive.Tests/HiveWinFormsHostIntegrationTests.cs`; then the broader `Hive.Tests` suite.