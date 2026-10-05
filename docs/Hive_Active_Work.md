# Hive — Active Work

Status: VERIFICATION PENDING

### Remediation checkpoint — 2026-10-05

The reported compile failure was remediated in `6095249c` — `Restore Model Information formatting helpers`. The change restores the missing `GetCapabilityState`, `FormatTokenLimit`, and `FormatDateTime` helpers and resolves the `SystemFonts.MessageBoxFont` nullable warning without changing the Model Information behavior or tab layout.

Verification remains pending. Re-run the developer compile/build and the focused Model Information tests before closure; the broader verification gate below remains unchanged.

### Verification failure remediation — 2026-10-05

The reported 3-test failure was remediated by aligning the affected assertions with the lazy tab contract. The UI polish remediation constrained the details content to the scroll viewport, enabled filter-row wrapping with bounded checkbox widths, removed the redundant empty Overview helper text, rendered capability details one per line, changed the technical tier heading to `Tiered rates:`, and formatted provider JSON with indentation and line-separated evidence. Documentation was synchronized with the resulting presentation.

Verification is pending developer rerun.

### Verification failure — 2026-10-05

Developer verification passed **680/680 tests (0 failed, 0 skipped)**. Manual Model Information verification then reported two remaining in-scope presentation defects: the pricing-evidence checkboxes are not visible at normal window size, and the top edge of the CRUD/details surfaces overlaps the lower portion of the capability/state filter controls because the filter/header region does not reserve sufficient vertical spacing.

Remediate only this Model Information layout boundary. Preserve the existing filter semantics, details scrolling, and responsive-row structure. Return Active Work to `VERIFICATION PENDING` after the correction and rerun the focused/full automated checks plus the manual Example Host path.

### Verification remediation — 2026-10-05

Developer verification reported **679 tests: 677 passed, 2 failed, 0 skipped**. The reported failures were remediated by moving the tiered-pricing check to the Technical lazy tab and validating selection-change data across Overview, Details, and Technical.

UI polish remediation is complete within the recorded failure boundary: detail values are line-oriented and width-constrained, details are synchronized after tab content changes so the existing vertical scroll host can recalculate for long text, provider JSON is indented and property-separated, the Model Information filter row is responsive with both pricing-evidence checkboxes preserved, and the page-specific CRUD search is bounded to 220px.

Verification remains pending developer rerun. No provider, persistence, filter-semantic, roadmap, or unrelated UI changes were made.

### Verification remediation — 2026-10-05

Developer verification reported **679 tests: 678 passed, 1 failed, 0 skipped**. The remaining failure was caused by the Technical tab omitting the model's `OwnedBy` provider evidence; the Technical page now renders it explicitly as `Provider`.

The final in-scope UI remediation also addresses the two reported presentation defects: the selected detail tab now determines an explicit content height so the enclosing HiveScrollHost can expose vertical scrolling at normal window size, and the filter controls are split into responsive primary/options rows so both pricing-evidence checkboxes remain visible without overlap.

A focused regression test was added for long Technical content at the normal `1160 x 760` test window and asserts that the Hive vertical scrollbar becomes available.

Verification remains pending developer rerun. No provider, persistence, filter-semantic, roadmap, or unrelated UI changes were made.
### Verification remediation — 2026-10-05

The nullable compile failure reported for `_detailsTabs.SelectedTab` was corrected by using the existing Overview tab as the non-null fallback before calling `MeasureDetailsPageHeight`. This preserves the details sizing path when a selected tab is temporarily unavailable and removes the CS8604 warning without changing the tab presentation contract.

Verification remains pending developer re-build and rerun of the focused Model Information tests and broader verification gate.

### Verification failure — 2026-10-05

Developer-reported Visual Studio compile error: **CS8604** in `HiveModelInformationSettingsView.cs` line 1855: possible null reference argument passed to `MeasureDetailsPageHeight(TabPage page, int availableWidth)` from `_detailsTabs.SelectedTab`.

This is an in-scope nullable-contract defect in the current details-scroll sizing remediation. Fix only the selected-tab nullability boundary, preserving the existing normal-size scroll behavior and tab layout. Verification must return to pending after remediation, with developer re-build and focused Model Information tests rerun.

### Verification remediation — 2026-10-05

The reported manual layout defects were corrected within the same boundary. The filter container now participates in the parent auto-size calculation instead of using `Dock=Fill`, reserves explicit vertical space for the two responsive filter rows, and adds bottom breathing room before the CRUD/details surface. A focused regression test now verifies that the capability/state combos and both pricing-evidence checkboxes are visible at normal size and that none extends into the CRUD/details surface.

Developer re-verification is pending. Rerun the full `Hive.Tests` suite and manually verify **Providers / Provider Platform / Model Information** at normal window size, specifically the filter controls and spacing.

## Off-Work UI Slice — Move Model Information into Provider Settings

Authorized: 2026-10-05 (explicit maintainer request)
Status: IN PROGRESS
Roadmap impact: None. Bounded off-roadmap UI/navigation work. Does not activate or advance any Phase 1 roadmap slice.

### Maintainer request

Move the existing, verified Model Information page out of **Advanced Provider Configuration** and into the normal **Providers** Settings page as a sibling tab.

Target normal Provider Settings navigation:

`Providers | Model Information | Favorite Execution Targets`

Advanced Provider Configuration remains administrative and contains only:

`Providers | Accounts / Credentials | Execution Targets`

### Scope

- Rehost the existing `HiveModelInformationSettingsView` under `HiveProvidersSettingsView`.
- Preserve all existing Model Information behavior, controls, pricing/filter semantics, details tabs, scrolling, and `Add to Favorites` action.
- Initialize Model Information when its normal Provider Settings tab is selected, with deterministic cancellation/disposal behavior matching the existing Favorites tab.
- Remove Model Information from Advanced Provider Configuration.
- Update focused UI tests and affected documentation/example navigation references.
- Keep Provider, ProviderAccount, ExecutionTarget, favorite persistence, and Management contracts unchanged.

### Exclusions

- No redesign of the Model Information surface.
- No provider/discovery/parser changes.
- No changes to Agent `Auto` / `Favorites` behavior.
- No new roadmap slice or durable resource.
- No changes to the underlying Provider → ProviderAccount → ExecutionTarget ownership model.

### Verification gate

1. Focused Provider Settings and Advanced Provider Configuration navigation tests pass.
2. Full `Hive.Tests` passes.
3. Developer manually verifies the real Example Host path:
   `Overview / Getting Started / Example Configuration → Providers → Model Information`
   and confirms the existing Model Information behavior remains intact.
4. Confirm Advanced Provider Configuration no longer presents a Model Information tab and still presents Accounts / Credentials and Execution Targets.

