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

## Off-Work UI Slice — Restore Model Information inside Advanced Provider Configuration

Authorized: 2026-10-05 (explicit maintainer request)
Status: VERIFICATION PENDING
Roadmap impact: None. Bounded off-roadmap UI/navigation reversal. Does not activate or advance any Phase 1 roadmap slice.
Checkpoint: `ce30bb47` — "Clean Model Information layout test"

### Maintainer request

Return the existing Model Information page to **Advanced Provider Configuration**, reversing the immediately preceding navigation relocation.

Target normal Provider Settings navigation:

`Providers | Favorite Execution Targets`

Target Advanced Provider Configuration navigation:

`Providers | Accounts / Credentials | Execution Targets | Model Information`

### Scope

- Restore `HiveModelInformationSettingsView` as the fourth Advanced Provider Configuration tab.
- Remove Model Information from the normal Providers Settings tabs.
- Restore the existing Advanced Model Information Example Host navigation path.
- Preserve the already-verified Model Information implementation itself: pricing/filter semantics, metadata presentation, details tabs, scrolling, checkbox layout, and Add to Favorites behavior.
- Restore focused navigation assertions and documentation to match the previous verified ownership.

### Exclusions

- No Model Information redesign or functional changes.
- No provider/discovery/parser changes.
- No changes to Provider, ProviderAccount, ExecutionTarget, or favorite persistence contracts.
- No Agent target-selection changes.
- No roadmap advancement.

### Verification gate

1. Focused Provider Settings and Advanced Provider Configuration navigation tests pass.
2. Full `Hive.Tests` passes.
3. Developer manually verifies the real Example Host path:
   `Overview / Getting Started / Example Configuration → Advanced Provider Configuration → Model Information`
   and confirms the existing Model Information behavior remains intact.
4. Confirm normal Providers Settings has only Providers and Favorite Execution Targets, while Advanced contains Providers, Accounts / Credentials, Execution Targets, and Model Information.

### Verification failure — 2026-10-07

Developer verification reported **681 tests: 680 passed, 1 failed, 0 skipped**. The failure is the in-scope Model Information layout regression test expecting the filter container to be a TableLayoutPanel while the current implementation intentionally uses a FlowLayoutPanel.

The maintainer also requested the next bounded layout refinement: place Capability and State beside Provider and Account on the primary filter row, move the price sliders and both pricing-evidence checkboxes to the row beneath, and use the resulting vertical space to increase the left-side model catalog height while keeping the details panel on the right.

**Status: VERIFICATION FAILED / REMEDIATION REQUIRED**

Remediate only this Model Information layout/test boundary. Preserve discovery semantics, filter semantics, details behavior, favorites behavior, and Advanced Provider Configuration ownership. After remediation, return Active Work to VERIFICATION PENDING and require focused/full test rerun plus manual UI verification.
### Polish follow-up — 2026-10-06

Developer verification of the immediately preceding navigation restoration passed **681/681 tests (0 failed, 0 skipped)**. The remaining authorized work is a bounded visual/layout polish of the existing Model Information page.

### Maintainer request

Refine the Model Information filter/header area to reduce wasted space and give the model catalog more vertical room:
- remove the bordered Provider / Account / endpoint context box;
- keep Provider and Account / Credential selectors in the same borderless filter area as the other inspection filters;
- remove the endpoint ComboBox from the visible UI;
- preserve discovery by selecting the same deterministic first endpoint internally for the selected account/provider context;
- reorganize the filter rows so the selectors, price/capability filters, and pricing-evidence switches have clear spacing and no overlap;
- use the reclaimed vertical space to increase the model-list viewport at normal window size.

### Scope

- HiveModelInformationSettingsView presentation/layout only, plus the minimum internal endpoint-selection adjustment required by removing the endpoint selector.
- Focused UI regression coverage for the new layout and the absence of the endpoint selector.
- Synchronize only the owning current UI/plan/example documentation that describes the Model Information filter layout.

### Exclusions

- No provider/discovery contract changes.
- No pricing/filter semantics changes.
- No changes to the Model Information details surface.
- No changes to Provider, ProviderAccount, ExecutionTarget, or favorite persistence contracts.
- No change to Advanced Provider Configuration ownership/navigation.
- No roadmap advancement.

### Verification gate

1. Focused Model Information UI tests pass.
2. Full Hive.Tests passes.
3. Developer manually verifies the real Advanced Provider Configuration → Model Information page at normal size, including filter organization, model-list height, selector behavior, and the existing details/favorites behavior.
### Verification remediation — 2026-10-07

The failed UI regression is remediated within the same boundary. The Model Information filter surface is now a borderless TableLayoutPanel with two responsive rows: Provider / Account / Capability / State on the first row, and Min / Max price controls plus both pricing-evidence switches on the second row. The old endpoint selector remains removed; endpoint selection is internal and deterministic as previously specified.

The regression coverage now asserts the two-row structure, the four top-row selectors, the second-row price/evidence controls, absence of the visible endpoint selector, and a larger normal-size model catalog viewport.

**Status: VERIFICATION PENDING**

Rerun the focused Model Information tests and the full `Hive.Tests` suite, then manually verify the real Advanced Provider Configuration → Model Information page at normal size.
