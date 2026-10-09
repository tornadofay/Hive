# Hive — Active Work

Status: **VERIFICATION FAILED / REMEDIATION REQUIRED**

## Active corrective slice

**Maintenance — UI: Settings Overview Layout & Configuration Guidance**

Started: 2026-10-09

### Checkpoint

- Repository branch: `main`
- Starting commit: `c8165222cafc4d012c47fa9fa744970a4e9fb2c4`
- Current implementation checkpoint: `45cd6dddba5ef9dc013a6429ebfebdf88135678d`
- Phase 1.18A — Embedded Persistence Profile is closed and verified. This temporary maintenance slice does not authorize Phase 1.19 or any other roadmap advancement.
- The prior Phase 1.18A closure evidence remains preserved at [Slice 6 closure verification](verification/phase-1/1.18A-slice-6-closure-2026-10-09.md).

### Authorized scope

Perform a bounded production audit of the existing Hive WinForms/UI presentation boundary, with emphasis on the global Settings Overview and reusable layout/theme/lifecycle behavior. Correct only concrete defects that restore or preserve existing UI behavior. Add focused regression coverage and keep user-facing configuration guidance aligned with the existing Embedded and SQL Server functionality.

Initial confirmed finding: `HiveSettingsOverviewView.AddCard` added the title, description, and note labels to a plain `Panel` without distinct layout positions or a layout container, causing them to share the default origin and overlap. The Persistence card also described only SQL Server / LocalDB despite the implemented Embedded profile.

### Boundaries and exclusions

- No new feature/capability, public API expansion, persistence/backend behavior change, or unrelated refactor.
- Do not start Phase 1.19 or alter roadmap ordering.
- Keep Hive.Host.WinForms.UI as the presentation owner and preserve the consumer-host boundary.
- Do not claim builds, tests, or manual UI verification unless the developer reports those results.

### Implementation review

- Replaced the Settings Overview card's overlapping child placement with a content-sized three-row `TableLayoutPanel`; the title, description, and note each receive their own row, and long text can wrap within the card width.
- Updated the Persistence card text to describe the Embedded local database and SQL Server deployment.
- Added `HiveSettingsOverviewCards_UseSeparateRowsAndDescribeBothPersistenceBackends` in `HiveUiPolishTests` to assert separate row placement for all three cards and accurate backend wording.
- Reviewed the focused source/test diffs after the changes. The implementation changes are limited to `src/Hive.Host.WinForms/HiveSettingsOverviewView.cs` and `tests/Hive.Tests/HiveUiPolishTests.cs`, plus this Active Work record. No roadmap or architecture changes were made.
- This assistant did not run builds/tests or launch the Example Host. Compile/runtime and visual behavior remain unverified until developer results are reported.

### Verification gate

Status: **VERIFICATION FAILED / REMEDIATION REQUIRED** based on developer-reported compiler/analyzer output.

Failure boundary: `tests/Hive.Tests/HiveUiPolishTests.cs`, `HiveSettingsOverviewCards_UseSeparateRowsAndDescribeBothPersistenceBackends`, reported at line 1629: analyzer `xUnit2031` rejects filtering with `.Where(...)` before calling `Assert.Single`. This is an in-scope test-code failure. No other failure has been reported in this handoff.

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms

Tests to run: `HiveUiPolishTests`; then the full `Hive.Tests` suite. Build affected projects with Visual Studio **Treat warnings as errors** enabled and manually inspect the Settings Overview at normal and resized window sizes in Light and Dark themes.
