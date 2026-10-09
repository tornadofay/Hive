# Hive — Active Work

Status: **IN PROGRESS**

## Active corrective slice

**Maintenance — UI: Settings Overview Layout & Configuration Guidance**

Started: 2026-10-09

### Checkpoint

- Repository branch: `main`
- Starting commit: `c8165222cafc4d012c47fa9fa744970a4e9fb2c4`
- Phase 1.18A — Embedded Persistence Profile is closed and verified. This temporary maintenance slice does not authorize Phase 1.19 or any other roadmap advancement.
- The prior Phase 1.18A closure evidence remains preserved at [Slice 6 closure verification](verification/phase-1/1.18A-slice-6-closure-2026-10-09.md).

### Authorized scope

Perform a bounded production audit of the existing Hive WinForms/UI presentation boundary, with emphasis on the global Settings Overview and reusable layout/theme/lifecycle behavior. Correct only concrete defects that restore or preserve existing UI behavior. Add focused regression coverage and keep user-facing configuration guidance aligned with the existing Embedded and SQL Server functionality.

Initial confirmed finding: `HiveSettingsOverviewView.AddCard` adds the title, description, and note labels to a plain `Panel` without distinct layout positions or a layout container, causing the labels to share the default origin and overlap. The Persistence card also still describes only SQL Server / LocalDB despite the implemented Embedded profile.

### Boundaries and exclusions

- No new feature/capability, public API expansion, persistence/backend behavior change, or unrelated refactor.
- Do not start Phase 1.19 or alter roadmap ordering.
- Keep Hive.Host.WinForms.UI as the presentation owner and preserve the consumer-host boundary.
- Do not claim builds, tests, or manual UI verification unless the developer reports those results.

### Implementation and verification

- Correct the card layout using standard WinForms layout containers and content-driven sizing; update the outdated Persistence card wording.
- Add focused regression coverage for non-overlapping card content and current backend guidance.
- After changes, status must be **VERIFICATION PENDING** until developer verification is reported.

Example to run: Overview / Getting Started / Example Configuration — Hive.Example.WinForms

Tests to run: `HiveUiPolishTests`; then the full `Hive.Tests` suite. Build affected projects with Visual Studio **Treat warnings as errors** enabled and manually inspect the Settings Overview at normal and resized window sizes in Light and Dark themes.
