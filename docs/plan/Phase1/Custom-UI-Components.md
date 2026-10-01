# Hive Custom UI Components Plan

## Purpose

Define the standalone implementation plan for the next reusable Hive WinForms UI component work. The plan covers four bounded implementation slices:

1. Custom Scroll Infrastructure
2. HiveComboBox with Filtering
3. HiveTabControl
4. Existing Hive UI Integration, Consistency & Hardening

The goal is to provide production-quality Hive-owned presentation for controls whose native WinForms rendering does not reliably match Hive's Light / Dark / System visual contract, while preserving normal WinForms behavior where practical.

This document is a plan only. It does not authorize implementation and does not activate any roadmap slice.

## Design Principles

### Hive-owned visual contract

All four components consume the existing `IHiveThemeManager` / `HiveThemeDefinition` contract. They must not introduce a second theme system or duplicate Light / Dark / System resolution.

### Native behavior where it is reliable

Hive should replace the presentation only where native rendering is insufficient. Existing native scrolling, page hosting, keyboard behavior, and framework mechanisms should be reused where they remain reliable rather than reimplemented unnecessarily.

### Overlay-first scrolling

The custom scrollbar should normally be rendered as an overlay so it does not consume layout width or height and does not introduce avoidable layout shifts.

### No control-specific architecture leakage

The components belong in `Hive.Host.WinForms.UI`. They must not introduce dependencies into Hive.Core, Hive.Management, Hive.Persistence, provider projects, or host business code.

### Production UI behavior

The components must account for focus, keyboard interaction, mouse interaction, disabled state, theme changes, resize, DPI, disposal, accessibility, and state preservation. They are not prototype-only visual controls.

### Reuse instead of one-off styling

Existing Hive forms should consume the new controls through reusable contracts. Individual forms must not implement parallel filtering, popup rendering, tab rendering, or scrollbar rendering.

---

# Slice 1 — Custom Scroll Infrastructure

## Objective

Create the reusable scrolling foundation consisting of `HiveScrollBar` and `HiveScrollHost`.

The scrollbar owns the Hive visual presentation. The scroll host is the reusable composition container that hosts a scrollable child/content surface and overlays the Hive scrollbar(s) without consuming content space. A small internal control-specific adapter boundary may translate the hosted control's native scroll state and requests. Native controls remain responsible for their actual scrolling where practical.

The public host contract must not require callers to implement per-control scrolling logic. Control-specific adapters are internal implementation details and are introduced only where a supported control actually requires them.

## HiveScrollBar

Create a reusable Hive-owned scrollbar supporting vertical and horizontal orientations.

### Visual behavior

- Hive Light / Dark / System theme support.
- Track, thumb, hover, and pressed visual states.
- Theme-driven sizing and semantic colors.
- Configurable orientation.
- Minimum thumb size so very large content remains usable.
- Proportional thumb sizing based on viewport/content relationship.
- Overlay-friendly rendering.
- DPI-aware dimensions.
- Invalidation limited to the affected visual state where practical.
- No dependency on a specific data/control type.

### Interaction behavior

- Mouse hover.
- Mouse down / pressed state.
- Thumb dragging.
- Click-on-track paging behavior.
- Mouse-wheel compatibility through the owning scroll host.
- Keyboard-driven scroll state synchronization.
- Correct capture/release behavior during drag.
- Scroll cancellation when the control is disposed or disabled.
- No recursive update loop when the native scroll position changes as a consequence of a scrollbar update.

### State contract

The scrollbar needs an explicit scroll-state model sufficient to represent:

- minimum;
- maximum;
- current value;
- viewport/large-change;
- small-change;
- enabled/disabled;
- orientation.

The implementation must tolerate content that is not scrollable and avoid displaying a misleading active thumb.

## HiveScrollHost

Create the reusable synchronization/hosting layer around a scrollable control or content surface.

### Responsibilities

- Host a scrollable child/content surface and overlay the Hive scrollbar without disrupting content layout.
- Observe effective content and viewport dimensions.
- Convert the hosted surface's native scroll state to normalized scrollbar state.
- Convert scrollbar value changes back into native scrolling requests.
- Keep ownership/disposal of a hosted child explicit and deterministic.
- Synchronize after resize, content changes, and native scrolling.
- Preserve scroll position when visual theme changes.
- Prevent feedback loops between host and scrollbar.
- Handle disposal/detachment safely.
- Support one or both orientations where required.
- Keep the scrollbar overlay aligned with the hosted surface.

### Native scrolling boundary

The host should use the existing control's scrolling APIs or native mechanism where practical. It must not create a universal replacement for Win32 scrolling.

Where a native control exposes its own visible scrollbar, Slice 1 must not claim complete visual replacement unless that native scrollbar can be suppressed reliably without breaking the control. Otherwise the surface remains on native scrolling until a later bounded integration is proven.

Control-specific integration should be limited to the minimum necessary adaptation required to obtain reliable scroll state.

## First Slice Integrations

Integrate the infrastructure into the first applicable Hive-owned scrolling surfaces, prioritizing:

- Hive-owned scrollable panels/content surfaces.
- `HiveNavigationTree` where reliable synchronization is available.
- `HiveListView` / `HiveCrudPage` where the native viewport can be synchronized without replacing native list behavior.

The implementation should avoid forcing a custom scrollbar onto a surface when the native behavior cannot be integrated cleanly yet. Suppressing or replacing a native scrollbar is a separate integration concern from drawing the Hive scrollbar and must be proven per control rather than assumed from a common base type.

## Verification

Focused automated coverage should cover at minimum:

- vertical and horizontal state calculation;
- minimum thumb behavior;
- proportional thumb behavior;
- drag updates;
- track paging;
- hover/pressed state transitions;
- content/viewport resize;
- non-scrollable content;
- native-to-Hive synchronization;
- Hive-to-native synchronization;
- feedback-loop prevention;
- theme changes without scroll-position loss;
- disabled/disposed behavior.

Manual Example Host verification should demonstrate the reusable scrollbar against representative scrollable Hive content.

---

# Slice 2 — HiveComboBox with Filtering

## Objective

Create a separate Hive UI ComboBox control that provides a fully Hive-themed selection field and popup rather than relying on the native ComboBox popup renderer.

The custom UI control must be a distinct opt-in presentation control and must not alter, replace, or take ownership of the Phase 1.14 host-integration `HiveComboBox` contract.

The control must be reusable for general Hive UI configuration and must not contain provider-specific behavior.

## Host-integration boundary

Phase 1.14 already provides `HiveComboBox : ComboBox, IHiveWinFormsFieldControl` for host integration metadata and bounded host semantics. That control remains unchanged by this UI slice.

The custom UI ComboBox must be a separate Hive UI control owned by the presentation layer. A host application that uses the Phase 1.14 `HiveComboBox` must not receive the custom popup, filtering, or Hive-specific rendering merely because it references the Hive library.

Hive's own UI surfaces may explicitly choose the custom control where the Hive visual contract is desired.

## Field presentation

The custom control should retain the conventional selection-oriented behavior expected from a ComboBox where practical, including stable selected-item/value access and programmatic selection. Domain-specific code must not depend on the popup implementation.

The closed field should provide:

- Hive Light / Dark / System rendering.
- Normal, hover, focused, disabled, and invalid/error-compatible visual states where the consuming surface needs validation feedback.
- Hive typography and palette tokens.
- Hive-consistent border, padding, and value alignment.
- Clear dropdown affordance.
- DPI-aware sizing.
- A defined distinction between the displayed selected value and the transient filter query so filtering does not corrupt the committed selection.
- Correct accessibility name/role/value reporting.
- Keyboard focus behavior consistent with normal WinForms expectations.

## Popup presentation

The opened popup should be Hive-rendered and must remain visually consistent with the field.

It should provide:

- themed popup surface;
- optional filtering/search field;
- filtered item list;
- selected-item visual;
- deterministic filtered-item ordering and stable selection identity;
- hover visual;
- disabled-item handling where supported by the item model;
- empty/no-match state;
- bounded popup sizing;
- screen-edge positioning;
- correct owner/activation behavior;
- Escape to close/cancel;
- Enter to commit the current selection;
- keyboard navigation through filtered results;
- mouse selection;
- focus restoration to the ComboBox after closing.

## Filtering behavior

Filtering should be incremental and deterministic:

- typing updates the filter;
- matching is case-insensitive;
- filtered results preserve the source ordering unless a later explicit contract says otherwise;
- empty filter restores the complete item set;
- no-match state is explicit;
- filtering must not mutate the underlying item collection;
- selection remains valid when the current item remains in the filtered set;
- a disappearing selected item must not produce an invalid selection state.

The filtering model must remain generic and independent of Provider / Model / ExecutionTarget domain types.

## Scrolling

Long popup lists should reuse `HiveScrollBar` rather than implementing another scrollbar renderer.

The popup therefore becomes a direct consumer of Slice 1 infrastructure.

## Lifecycle and layout

The control must correctly handle:

- popup open/close;
- owner disposal;
- form deactivation;
- resize;
- DPI changes;
- theme changes while open;
- repeated opening and closing;
- item-source replacement;
- empty item sources;
- large item counts without avoidable repaint or allocation churn;
- programmatic selection/item-source changes while the popup is open.

## Verification

Focused automated coverage should cover:

- filtering;
- selection;
- keyboard navigation;
- Enter/Escape behavior;
- empty and no-match states;
- popup opening/closing;
- theme changes;
- disabled state;
- item-source changes;
- focus restoration;
- scrolling of long lists;
- popup bounds near screen edges;
- disposal and repeated use.

Manual Example Host verification should demonstrate a filtered ComboBox in Light and Dark modes, including a long list using the Hive scrollbar.

---

# Slice 3 — HiveTabControl

## Objective

Create a Hive-owned tab control presentation that replaces the native TabControl header rendering while retaining conventional WinForms page/content hosting.

The goal is full Hive visual consistency without rebuilding a complete page-management framework.

## Tab header presentation

The tab header renderer should support:

- Light / Dark / System.
- Normal, hover, selected, focused, pressed, disabled states as applicable.
- Hive typography.
- Theme-driven spacing and padding.
- Clear active-tab indicator.
- Consistent separator/border treatment where used.
- Keyboard-focus indication.
- DPI-aware sizing.
- Optional close/auxiliary actions only if later consumers require them; they are not required for the initial contract.

## Page hosting

Keep the page/content model conventional and predictable:

- ordinary WinForms controls can remain the content;
- only the tab presentation/header interaction is Hive-owned;
- the public contract should expose conventional tab concepts such as tab-page collection, selected index/tab, and programmatic selection where those concepts are part of the final control surface;
- changing tabs must preserve page instances and expected lifecycle behavior;
- disposal must be deterministic;
- the component must not introduce a second page-navigation framework.

## Keyboard and interaction behavior

Support:

- mouse selection;
- conventional keyboard tab navigation, including the key combinations required by the final WinForms interaction contract;
- focus movement;
- selected-tab preservation;
- disabled-tab behavior where supported;
- correct activation when the selected tab changes programmatically;
- synchronization between user selection and programmatic selection without duplicate or stale selection events.

Theme changes must not unexpectedly change the selected tab or active page.

## Overflow behavior

The control must define deterministic behavior when tab headers do not fit.

The preferred approach is bounded horizontal tab scrolling using reusable Hive scrolling infrastructure rather than silently compressing tabs until labels become unusable.

Where overflow scrolling is introduced, it should reuse `HiveScrollBar` / `HiveScrollHost` instead of creating separate scroll mechanics.

## Verification

Focused automated coverage should cover:

- selection changes;
- selected-state preservation across theme changes;
- keyboard navigation;
- focus behavior;
- disabled tabs;
- programmatic selection;
- page hosting;
- disposal;
- resize/DPI behavior;
- header overflow behavior;
- scrolling integration when overflow exists.

Manual Example Host verification should demonstrate tabs in Light and Dark modes, including selected/hover/focused states and overflow behavior if represented by the example.

---

# Slice 4 — Existing Hive UI Integration, Consistency & Hardening

## Objective

Integrate the completed components into Hive's existing UI where native controls currently fail to provide consistent Hive Light / Dark behavior or where the new reusable controls provide a clearly better shared implementation.

This slice is not an opportunity for unrelated UI redesign.

## Existing ComboBox integration

Identify existing Hive surfaces using native ComboBox where the native field/dropdown appearance conflicts with Hive theming.

Replace those instances with the separate custom Hive UI ComboBox control where the semantic behavior fits. Do not replace host-integration `HiveComboBox` merely to obtain the custom presentation.

Particular attention should be paid to:

- configuration/settings editors;
- provider configuration;
- model/target selection;
- capability-related selection controls;
- any other repeated configuration field using the same selection interaction.

Existing domain validation and business logic remain outside the control.

## Existing TabControl integration

Identify existing Hive surfaces using native TabControl where the tab presentation is visibly inconsistent with Hive.

Replace only the presentation that benefits from `HiveTabControl`.

Do not introduce unnecessary TabControl replacements where native behavior already meets the established Hive contract.

## Existing scrollbar integration

Apply `HiveScrollHost` to the appropriate existing Hive surfaces established by Slice 1.

Prioritize shared surfaces over one-off forms so the new behavior is centralized.

## Theme consistency

Verify that Light / Dark / System transitions produce a coherent result across:

- scrollbar;
- scrollbar host;
- ComboBox field;
- ComboBox popup;
- ComboBox filtered list;
- TabControl;
- tab headers;
- existing ListView/tree/grid surfaces surrounding the new controls.

The implementation must not create a mix of Hive-dark fields with a native light popup, or a Hive-themed tab header with an inconsistent native header fragment.

## State preservation

Theme changes and component refreshes must preserve, where applicable:

- current selection;
- focused control;
- open/closed popup state when safe;
- scroll position;
- active tab;
- filtered text/query;
- selected list item.

Avoid unnecessary handle recreation, layout passes, or scroll resets.

## Cleanup

Remove obsolete per-form styling workarounds only when the new shared control fully replaces their responsibility.

Do not perform unrelated cleanup or broad UI refactoring.

## Verification

The hardening pass should include:

- focused component test suites;
- affected existing UI regression tests;
- Light/Dark/System transition coverage;
- resize/DPI coverage where practical;
- disposal/reopen coverage;
- interaction between ComboBox popup scrolling and HiveScrollBar;
- interaction between TabControl overflow and HiveScrollBar when implemented;
- verification that existing ListView, TreeView, and DataGridView behavior remains intact;
- manual Example Host verification of representative integrated settings/configuration surfaces.

The broader `Hive.Tests` suite remains the regression gate when the integration changes are complete.

---

# Cross-Slice Non-Goals

These slices do not include:

- replacing every WinForms control with a Hive-prefixed equivalent;
- rewriting Win32 scrolling;
- creating a universal custom renderer for all Windows controls;
- replacing DataGridView with a custom grid;
- replacing ListView/TreeView internals;
- introducing a third-party UI framework;
- introducing a second theme manager;
- introducing a new application-wide navigation framework;
- redesigning Provider, Agent, Workspace, or business-domain behavior;
- changing Hive.Core or persistence architecture for UI concerns.

# Cross-Slice Dependencies

The intended implementation dependency is:

```
Slice 1
Custom Scroll Infrastructure
      ├───────────────┐
      ↓               ↓
Slice 2           Slice 3
HiveComboBox      HiveTabControl
      \               /
       \             /
        ↓           ↓
         Slice 4
 Existing UI Integration
 & Hardening
```

Slice 2 depends on Slice 1 because long ComboBox popups should reuse the Hive scrollbar.

Slice 2 must not modify the existing Phase 1.14 HiveComboBox integration control. The custom ComboBox is a separate presentation control used explicitly by Hive UI surfaces.

Slice 3 may consume Slice 1 for tab-header overflow scrolling, but its core tab presentation does not depend on scrollbar support.

Slice 4 depends on the stable reusable contracts from the preceding slices.

# Expected Public UI Surface

The final reusable UI surface should remain small:

- `HiveScrollBar`
- `HiveScrollHost`
- a separate Hive UI filtered ComboBox control
- `HiveTabControl`

The existing Phase 1.14 `HiveComboBox` remains a host-integration control and is not part of the custom visual-control contract.

Implementation helpers for popup rendering, scroll synchronization, tab measurement, filtering, or state management should remain internal unless a concrete consumer-facing contract is required.

# Example Host Expectations

Each slice that introduces a meaningful externally usable UI capability must provide a deterministic `Hive.Example.WinForms` scenario using public Hive UI contracts.

Required handoff format:

```
Example to run: <exact Category / Subcategory / optional AdditionalNavigationPath / Example title> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
```

# Completion Criteria

The overall custom UI component effort is complete only when:

- all four slices have passed their focused verification;
- integrated Hive surfaces no longer rely on native ComboBox or TabControl rendering where it violates the Hive theme contract;
- custom scrollbars behave correctly without breaking native scrolling;
- Light / Dark / System transitions preserve relevant interaction state;
- accessibility, keyboard, mouse, DPI, resize, disposal, and lifecycle behavior are covered to the extent applicable;
- no duplicate theme/rendering implementation exists;
- the final reusable control contracts are documented under `docs/ui/`;
- the affected Example Host scenarios and automated tests are in place;
- no later roadmap work has been activated by this plan.
