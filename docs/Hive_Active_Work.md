# Hive — Active Work

Status: IN PROGRESS

## Authorized Slice

**Phase 1 Custom UI Components — Slice 1: Custom Scroll Infrastructure**

Authorization: explicit user command `Hive: start` against `docs/plan/Phase1/Custom-UI-Components.md`.

Repository checkpoint before implementation: `de46b3e74b20c1eb83a8a453ff8b8fd8080c48a8` on `main`.

Current implementation head: `79ef6a76205f4254bea6ba9e4ec914b5dfc03b5a` on `main`.

## Scope

Implement only the first bounded slice from the linked plan:

- reusable Hive-owned `HiveScrollBar` with normalized scroll state, vertical/horizontal orientation, proportional/minimum thumb behavior, hover/pressed/drag/track-page/keyboard interaction, theme support, DPI-aware geometry, and deterministic disposal;
- reusable `HiveScrollHost` with one caller-supplied content surface, overlay scrollbars, normalized horizontal/vertical state, host-to-content and content-to-host synchronization, resize/content-change synchronization, feedback-loop protection, wheel/keyboard support, and explicit detach/ownership semantics;
- first applicable Hive-owned integration, prioritizing `HiveEditorLayout` and other generic scrollable Hive content where reliable synchronization is available;
- focused `Hive.Tests` coverage for normalized state/scroll math, interaction state transitions, host synchronization, resize/non-scrollable behavior, feedback-loop prevention, theme preservation, and lifecycle/disposal;
- a deterministic `Hive.Example.WinForms` scenario demonstrating the reusable scroll infrastructure through public UI contracts;
- update the owning UI documentation to describe the implemented public scroll contracts.

## Explicit Exclusions

Do not implement or activate Slice 2 (`HiveComboBox` filtering), Slice 3 (`HiveTabControl`), Slice 4 broad UI hardening, later roadmap work, native scrollbar suppression for controls where reliable integration is not proven, or unrelated UI refactoring.

`HiveNavigationTree` and `HiveListView` retain native scrolling unless this slice can integrate their native viewport reliably without replacing their native list/tree behavior.

## Verification State

Status: VERIFICATION PENDING

Required developer handoff:

Example to run: UI / Foundation / Scroll Infrastructure — Hive.Example.WinForms
Tests to run: HiveScrollBarTests.cs and HiveScrollHostTests.cs; broader-suite requirement: full Hive.Tests suite after focused coverage passes.

No build, test run, or manual Example Host verification has been performed by the agent.
