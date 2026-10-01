# Hive Custom UI Components Plan

## Purpose

Define the standalone implementation plan for the next reusable Hive WinForms UI component work. The plan covers four bounded implementation slices:

1. Custom Scroll Infrastructure
2. HiveComboBox with Filtering
3. HiveTabControl
4. Existing Hive UI Integration, Consistency & Hardening

The goal is not cosmetic restyling. These components become first-class Hive-owned presentation controls where native WinForms rendering cannot reliably participate in Hive's Light / Dark / System visual contract. Conventional WinForms behavior remains the compatibility target where practical, while the visible field/popup/tab presentation is fully Hive-rendered.

This document is a plan only. It does not authorize implementation and does not activate any roadmap slice.

## Design Principles

## Shared UI subsystem contract

The four components form one Hive UI subsystem over the existing theme manager and design-token model:

```text
HiveThemeManager
      ↓
HiveThemeDefinition
      ├── HiveScrollBar / HiveScrollHost
      ├── HiveComboBox
      └── HiveTabControl

HiveComboBox → HiveScrollBar / HiveScrollHost for popup overflow
HiveTabControl → HiveScrollBar / HiveScrollHost when tab-header overflow is required
```

The subsystem owns the visual surfaces that native WinForms does not reliably theme: ComboBox field and popup, filtered list, TabControl headers, and the custom scrollbar. It does not replace ordinary controls or introduce a new rendering/theme authority.

### Hive-owned visual contract

All four components consume the existing `IHiveThemeManager` / `HiveThemeDefinition` contract. They must not introduce a second theme system or duplicate Light / Dark / System resolution.

### Native behavior where it is reliable

Hive owns the visible presentation of these controls, but should reuse reliable WinForms behavior underneath rather than rebuild framework mechanisms unnecessarily. This means normal page hosting, selection semantics, focus semantics, data binding, and native scrolling APIs may be reused where they do not conflict with the Hive visual contract.

For these controls, reusing native behavior does not mean accepting native rendering. The entire visible ComboBox field/popup and TabControl header are Hive-owned presentation.

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

HiveScrollHost hosts one explicit scrollable content surface. The hosted control remains caller-owned when detached or replaced; the host does not silently dispose externally supplied content merely because the visual hosting relationship changed. While attached, normal WinForms parent/child disposal semantics apply.

The public host contract must not require callers to implement per-control scrolling logic. The host exposes normalized scroll state and user-facing scroll requests; control-specific synchronization adapters remain internal implementation details and are introduced only where a supported control actually requires them.

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

The scrollbar needs an explicit normalized scroll-state model sufficient to represent:

- minimum;
- maximum;
- current value;
- viewport/large-change;
- small-change;
- enabled/disabled;
- orientation.

The normalized model must distinguish content extent from viewport extent and current logical position. The effective maximum scroll position must be derived from those extents rather than copied blindly from WinForms Maximum/LargeChange semantics. The implementation must tolerate content that is not scrollable and avoid displaying a misleading active thumb.

## HiveScrollHost

Create the reusable synchronization/hosting layer around a scrollable control or content surface.

## HiveScrollHost public contract

The host exposes a small presentation contract rather than a control-specific adapter API:

- attach or detach one scrollable content surface;
- expose normalized horizontal and vertical scroll state;
- accept user-facing scroll position changes;
- synchronize after content or viewport changes;
- preserve the supplied content control's ownership semantics when detached or replaced.

The native synchronization adapter remains internal. Existing Hive forms must never need to know whether synchronization is implemented through WinForms properties, native messages, or a control-specific adapter.

### Responsibilities

- Host exactly one scrollable child/content surface and overlay the Hive scrollbar without disrupting content layout.
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

Use the existing HiveComboBox as the first-class Hive UI ComboBox control, providing a fully Hive-themed selection field and popup rather than relying on the native ComboBox renderer.

The existing `HiveComboBox` remains the single public ComboBox control. Its Hive presentation is not a separate opt-in control and does not replace its Phase 1.14 host-integration contract.

The control must be reusable for general Hive UI configuration and must not contain provider-specific behavior.

## Control implementation strategy

`HiveComboBox` remains the single public Hive ComboBox type, but its visible surface is Hive-owned rather than delegated to the native WinForms ComboBox renderer.

The implementation should use a Hive-owned visible field and a Hive-owned popup surface. A native WinForms ComboBox may be retained internally only as a non-user-visible compatibility or data-binding backend when that materially reduces compatibility risk; it must not remain responsible for the visible field, popup, filtering UI, or scrollbar.

This is an intentional first-class control implementation, not a thin color/style wrapper. The public compatibility target is the conventional selection/data-binding behavior used by existing Hive consumers, not preservation of every implementation detail of the native ComboBox window.

Before implementation, inventory whether any existing Hive or Example Host consumer relies on HiveComboBox being assignable to ComboBox. Such inheritance-dependent consumers require an explicit migration path if the public implementation changes its base type; this must not become an accidental breaking change.

The visible field must not depend on native ComboBox painting. The current WinForms ComboBox implementation disables ControlStyles.UserPaint, and its owner-draw support is centered on drawing list elements rather than replacing the complete field and popup presentation.

`HiveComboBox` therefore owns its field, popup, filter box, result rows, selection visuals, disabled/error states, and popup scrolling.

## Required migration impact

The implementation changes the concrete control architecture from the current `HiveComboBox : ComboBox` inheritance to the Hive-owned composite implementation. That impact must be handled deliberately in the same bounded UI effort.

- replace the current native-inheritance assertion in `HiveWinFormsBaseControlIntegrationTests` with tests for the new public `HiveComboBox` contract;
- update `WinFormsControlValueAdapters` so `HiveComboBox` is handled explicitly rather than relying only on `control is ComboBox`;
- preserve `IHiveWinFormsFieldControl`, `HiveIntegration`, and `HiveField` metadata capture and authorization paths;
- audit any API, collection, helper, designer, or host integration that requires a concrete `ComboBox` base type and migrate it explicitly;
- preserve ordinary native `ComboBox` handling for business applications that are not using `HiveComboBox`;
- update `docs/ui/controls.md` and the applicable architecture/Example Host guidance when the new public contract is implemented.

## Host-integration and public-control boundary

Phase 1.14 established the public Hive field-integration role for `HiveComboBox`. This slice preserves that role and evolves the concrete presentation control; it does not create a second Hive ComboBox type.

The Phase 1.14 historical inheritance assertion is superseded by this plan's deliberate control-architecture change. Historical verification remains valid for the implementation that was verified at the time; it does not constrain the future first-class visual-control architecture.

The public compatibility target is the subset of conventional ComboBox behavior actually required by Hive consumers. At minimum the new control contract must define and test `Items`, data binding, `DisplayMember`, `ValueMember`, `SelectedIndex`, `SelectedItem`, `SelectedValue`, `Text`, enabled/read-only behavior where applicable, and selection-change notification. Any unsupported native member or behavior must be explicitly documented before existing Hive surfaces are migrated.

Host-integration metadata remains part of the same control and must not be lost or replaced by the visual implementation. Its existing `HiveIntegration` and `HiveField` properties remain public and behave as before; the custom field/popup/filter rendering is internal presentation behavior of `HiveComboBox`.

## Field presentation

`HiveComboBox` should retain the conventional selection-oriented behavior expected from a ComboBox where practical, including stable selected-item/value access and programmatic selection. Domain-specific code must not depend on the popup implementation.

The closed field should provide:

- Hive Light / Dark / System rendering.
- Normal, hover, focused, disabled, and invalid/error-compatible visual states where the consuming surface needs validation feedback.
- Hive typography and palette tokens.
- Hive-consistent border, padding, and value alignment.
- Clear dropdown affordance.
- DPI-aware sizing.
- A defined distinction between the displayed selected value and the transient filter query so filtering does not corrupt the committed selection.
- Correct accessibility name/role/value reporting while retaining the control's existing host-integration metadata.
- Keyboard focus behavior consistent with normal WinForms expectations.
- Normal programmatic selection/binding must update the visible field without opening the popup or changing the filter state.

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

Filtering should be incremental and deterministic. The filter is transient UI state and must never overwrite the committed selected item/value.

Use ordinal case-insensitive matching unless an existing consumer contract requires another documented comparison rule. Search text should come from the configured display representation rather than arbitrary object `ToString()` behavior when `DisplayMember`/binding semantics provide a stable display value.

- typing updates the filter;
- matching is case-insensitive;
- filtered results preserve the source ordering unless a later explicit contract says otherwise;
- empty filter restores the complete item set;
- no-match state is explicit;
- filtering must not mutate the underlying item collection;
- selection remains valid when the current item remains in the filtered set;
- a disappearing selected item must not produce an invalid committed selection state.
- duplicate display text must not make selection ambiguous; underlying item identity remains the selection identity.
- null/empty display text remains representable and does not break filtering.

The filtering model must remain generic and independent of Provider / Model / ExecutionTarget domain types.

## Scrolling

Long popup lists must reuse `HiveScrollHost` / `HiveScrollBar` rather than implementing another scrollbar renderer. The popup's visual scroll surface and scrollbar must participate in the same theme and lifecycle boundary.

The popup owns its scroll-host lifetime. Closing the popup detaches/disposes the popup-owned scrolling surface safely without disposing caller-owned item data. Reopening creates or reuses only internal visual resources; it never recreates or mutates the caller's item source merely because the popup was reopened.

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

Manual Example Host verification should demonstrate the first-class `HiveComboBox` in Light, Dark, and System modes, including a long filtered list using the Hive scrollbar and keyboard-only selection.

---

# Slice 3 — HiveTabControl

## Objective

Create `HiveTabControl` as a first-class Hive UI control that owns the visible tab-header rendering while retaining conventional WinForms tab-page/content hosting.

The control should preserve the useful public TabControl model rather than introduce a separate page/navigation framework. Hive owns the header presentation because native TabControl rendering cannot reliably satisfy the Hive visual contract.

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

## Tab control implementation strategy

`HiveTabControl` is a Hive-owned composite control with a dedicated header surface and a conventional `TabPage` content area. It is not a `TabControl` subclass whose native header is merely recolored. This makes the header fully Hive-rendered without depending on the native header renderer.

The public compatibility target is the conventional tab/page behavior actually needed by Hive: tab-page collection, selected tab/index, programmatic selection, selection-change notification, page preservation, keyboard navigation, and deterministic disposal. APIs that specifically require `HiveTabControl` to be assignable to the native `TabControl` are not part of the compatibility guarantee and must be migrated explicitly before adoption.

WinForms owner-draw support exists for `TabControl`, but it is fixed-size and applies to tab headers only. It therefore does not provide the flexible header and overflow presentation required by this plan.

## Page hosting and public compatibility

Use the normal WinForms `TabPage` content model through the Hive-owned control. The public control should preserve the conventional tab concepts Hive consumers reasonably depend on, including tab-page collection, selected index/tab, programmatic selection, and selection-change notification.

Use the normal WinForms `TabControl` / `TabPage` page-hosting model where practical. The Hive control must preserve the public tab concepts existing Hive consumers reasonably depend on, including tab-page collection, selected index/tab, programmatic selection, and selection-change notification.

- ordinary WinForms controls remain valid tab content;
- page instances remain stable when switching tabs;
- Hive owns the header visual/interaction surface, not the content controls' business semantics;
- disposal follows normal WinForms page/control lifecycle expectations;
- the component must not introduce a second application-wide navigation framework; its internal page-hosting mechanism is limited to managing its own tab pages;
- any native TabControl behavior intentionally changed by the custom header implementation must be explicit and covered by tests.

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

The preferred approach is bounded horizontal tab scrolling using reusable Hive scrolling infrastructure rather than silently compressing tabs until labels become unusable. The active-tab indicator and selected-tab visibility must remain stable when the header viewport changes.

Where overflow scrolling is introduced, it should reuse `HiveScrollBar` / `HiveScrollHost` instead of creating separate scroll mechanics.

## Verification

Focused automated coverage should cover:

- selection changes;
- selected-state preservation across theme changes;
- keyboard navigation;
- focus behavior;
- disabled tabs;
- accessibility name, role, state, and selected-value exposure;

- programmatic selection;
- page hosting;
- disposal;
- resize/DPI behavior;
- header overflow behavior;
- scrolling integration when overflow exists.

Manual Example Host verification should demonstrate `HiveTabControl` in Light, Dark, and System modes, including selected/hover/focused/disabled states, keyboard navigation, page preservation, and overflow behavior when represented by the example.

---

# Slice 4 — Existing Hive UI Integration, Consistency & Hardening

## Objective

Integrate the completed components into Hive's existing UI where native controls currently fail to provide consistent Hive Light / Dark behavior or where the new reusable controls provide a clearly better shared implementation.

This slice is not an opportunity for unrelated UI redesign.

## Existing ComboBox integration

Identify existing Hive surfaces using native ComboBox where the native field/dropdown appearance conflicts with Hive theming.

Migrate appropriate native `ComboBox` instances to the first-class Hive `HiveComboBox` where the semantic behavior fits. Existing `HiveComboBox` consumers already use the correct type; they gain the Hive-owned presentation without changing their integration contract.

Particular attention should be paid to:

- configuration/settings editors;
- provider configuration;
- model/target selection;
- capability-related selection controls;
- any other repeated configuration field using the same selection interaction.

Existing domain validation and business logic remain outside the control.

## Existing TabControl integration

The current repository search does not identify an existing production `TabControl` consumer comparable to the existing ComboBox usage. Slice 4 must therefore treat `HiveTabControl` as a reusable foundation first and migrate real existing consumers when they are actually present.

Where an existing Hive surface later uses native `TabControl` and its header rendering violates the Hive theme contract, migrate that surface to `HiveTabControl` after its public tab/page compatibility requirements are covered.

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

## Accessibility and keyboard contract

All four components are interactive Windows controls and therefore require explicit keyboard and accessibility behavior rather than relying on visual similarity alone.

`HiveComboBox` must expose an accessible name, role, selected/current value, expanded/collapsed state, enabled/disabled state, and meaningful keyboard focus. Its popup filter and result list must remain navigable without requiring mouse interaction.

`HiveTabControl` must expose the selected tab, tab count, selected/disabled state, and keyboard navigation through its accessibility tree. Header focus must remain distinguishable from page content focus.

`HiveScrollBar` and scrollable surfaces must expose orientation, current position, and usable range to accessibility clients where WinForms accessibility supports those concepts. Keyboard scrolling must remain available even when the visual scrollbar is not focused.

The custom rendering must never create a visually complete but accessibility-incomplete replacement for the corresponding native interaction surface.

# Cross-Slice Public Compatibility Rule

These controls are first-class Hive presentation components, not cosmetic wrappers. Their visible rendering is Hive-owned, but their public compatibility target remains conventional WinForms behavior where practical.

For `HiveComboBox`, this means the existing Phase 1.14 type and host-integration metadata remain intact while the field, popup, filtering surface, selection visuals, and scrollbar are Hive-rendered.

For `HiveTabControl`, this means ordinary `TabPage` content hosting and conventional tab selection remain intact while tab headers and overflow presentation are Hive-rendered.

For `HiveScrollHost`, this means it provides a reusable visual/synchronization host without requiring feature forms to know control-specific scrolling internals.

When native behavior cannot be preserved without undermining the Hive visual contract, the deviation must be explicit, narrowly scoped, documented, and covered by focused tests rather than left as an accidental side effect.

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

Slice 2 evolves the existing Phase 1.14 `HiveComboBox` into the first-class Hive-rendered control. Its host-integration contract remains intact; the visual implementation is part of that same control.

Slice 3 may consume Slice 1 for tab-header overflow scrolling, but its core tab presentation does not depend on scrollbar support.

Slice 4 depends on the stable reusable contracts from the preceding slices.

# Expected Public UI Surface

The final reusable UI surface should remain small. The concrete public contracts must be documented in `docs/ui/controls.md` when each component is implemented:


- `HiveScrollBar`
- `HiveScrollHost`
- `HiveComboBox` — first-class Hive-rendered field, popup, filtering, and selection control; retains the Phase 1.14 host-integration contract but is no longer required to derive from native `ComboBox`.
- `HiveTabControl`

There is no separate `HiveFilteredComboBox` or other parallel ComboBox type. Popup/filter helpers and rendering infrastructure remain internal implementation details unless a concrete consumer-facing contract is required.

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
- `HiveComboBox` is the single public Hive ComboBox type used by Hive UI surfaces where Hive-specific selection UI is required; no parallel filtered ComboBox type exists;
- the intentional removal of native `ComboBox` inheritance has been migrated through all affected Hive consumers, adapters, tests, and documentation;
- integrated Hive surfaces no longer rely on native ComboBox or TabControl rendering where it violates the Hive theme contract;
- custom scrollbars behave correctly without breaking native scrolling;
- Light / Dark / System transitions preserve relevant interaction state;
- accessibility, keyboard, mouse, DPI, resize, disposal, and lifecycle behavior are covered to the extent applicable;
- no duplicate theme/rendering implementation exists;
- the final reusable control contracts are documented under `docs/ui/`;
- the affected Example Host scenarios and automated tests are in place;
- no later roadmap work has been activated by this plan.
