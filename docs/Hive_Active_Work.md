# Hive — Active Work

Status: IN PROGRESS

## Authorized Slice

**Phase 1 Custom UI Components — Slice 3: HiveTabControl**

Authorization: explicit user command to start the next UI slice following the completed Slice 2 and `docs/plan/Phase1/Custom-UI-Components.md`.

Repository checkpoint before implementation: 2d5daf7018f05e82ab4131f63e48684895514b76 on `main`.

## Scope

Implement only Slice 3 from the linked plan:

- create `HiveTabControl` as a Hive-owned composite control rather than a native `TabControl` subclass;
- preserve conventional `TabPage` content hosting, selected tab/index, programmatic selection, selection-change notification, page preservation, keyboard navigation, and disposal behavior where practical;
- provide Hive-rendered tab headers with Light/Dark/System support and normal, hover, selected, focused, pressed, and disabled states;
- provide Hive typography, spacing, active-tab indicator, separator/border treatment, keyboard-focus indication, and DPI-aware sizing;
- support mouse selection, keyboard navigation, focus movement, disabled-tab behavior, and synchronization between user/programmatic selection without duplicate or stale selection events;
- preserve selected tab/active page across theme changes;
- provide deterministic horizontal header overflow behavior using `HiveScrollHost` / `HiveScrollBar`;
- keep the active tab visible when header overflow or viewport size changes;
- provide accessibility role/name/state/value behavior appropriate to a tab control;
- add focused automated coverage for selection, theme-state preservation, keyboard/focus behavior, disabled tabs, accessibility, page hosting, disposal, resize/DPI, overflow, and scroll integration;
- add deterministic `Hive.Example.WinForms` coverage for Light/Dark/System, selected/hover/focused/disabled states, keyboard navigation, page preservation, and overflow;
- update `docs/ui/controls.md` and applicable UI/architecture/Example guidance.

## Explicit Exclusions

Do not implement Slice 4 broad existing-UI integration/hardening, later roadmap work, broad native TabControl replacement, a new application-wide navigation framework, a second tab control type, unrelated renderer abstraction, or unrelated refactoring.

No existing native TabControl consumer is being migrated unless repository evidence identifies a concrete inheritance-dependent consumer during this slice.

## Implementation Checkpoint

Slice 3 implementation has just been opened. No code changes for this slice have been made yet.

## Verification State

Status: VERIFICATION PENDING

Required developer handoff:

Example to run: UI / Foundation / HiveTabControl — Hive.Example.WinForms
Tests to run: HiveTabControlTests.cs; broader-suite requirement: full Hive.Tests suite after focused coverage passes.

Agent has not run the build or tests. Developer verification is required after implementation, including the matching Example Host scenario.