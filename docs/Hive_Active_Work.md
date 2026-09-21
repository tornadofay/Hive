# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.7 — First-Class Example Host Shell**

Phase 0.6 was accepted after the developer exercised the shared UI foundation in the real Example application, including CRUD editing/deletion, pagination, editor interaction, and error/failure presentation. The implementation now proceeds to the documented 0.7 shell; do not begin Phase 0.8 or later slices.

## Objective

Turn Hive.Example.WinForms into a permanent developer-facing host with scalable Category → Subcategory → Example navigation and one replaceable right-side UserControl view.

The shell must discover IHiveExample implementations without a central manual registration list, own navigation/view lifetime, and consume only Hive-owned UI contracts.

## 0.7 implementation scope

The active 0.7 pass is now a full production UI/UX polish pass over the shared WinForms foundation and Example host. It is intentionally limited to presentation, interaction quality, responsiveness, state handling, theme consistency, and UI-code performance. No later platform functionality is introduced.

The pass covers:
- professional desktop layout, spacing, hierarchy, typography, and visual density;
- Category → Subcategory → Example navigation and stable selection/scroll/focus state;
- Light / Dark / System themes and semantic visual states;
- CRUD list, search, actions, pagination, empty/loading/error states, and editor presentation;
- dialogs and technical-error presentation;
- resize behavior and compact supported dimensions;
- consistent native/Hive control styling;
- disposal, nullability, allocation, layout, painting, and traversal discipline on UI paths.

- add the IHiveExample discovery contract;
- discover examples from the designated Example assembly;
- build a left-side Category → Subcategory → Example tree;
- replace the active right-side view without stacking or overlapping pages;
- dispose the previous example view deterministically;
- provide the shared Example services through IServiceProvider;
- keep the existing 0.6 UI foundation as the first discoverable example;
- preserve Light / Dark / System theme behavior through the shared IHiveThemeManager;
- no feature-specific platform functionality is added to the shell.

## Implementation progress

- added IHiveExample and the Example-side service provider contract;
- added reflection discovery for internal IHiveExample implementations in the designated Example assembly;
- added the first-class HiveExampleHostForm with Category → Subcategory → Example navigation and a replaceable right-side view;
- added themed navigation rendering with explicit selected/hover/focus states and preservation of selection/scroll during theme changes;
- added themed lightweight ListView rendering for CRUD selection/hover/disabled states;
- refined the shared palette, visual-state tokens, button hierarchy, window header, editor rhythm, CRUD density, pagination, and message-dialog presentation;
- made the permanent Example host behave as a normal desktop window and improved compact-window resizing;
- converted the existing 0.6 UI foundation surface from a top-level form into a discoverable UserControl example;
- updated Program to launch HiveExampleHostForm;
- removed the legacy 0.6 top-level form path.

## Verification

1. adding an IHiveExample implementation makes it appear without editing shell registration code;
2. category/subcategory/example navigation selects exactly one active view;
3. switching examples disposes the previous view and does not accumulate controls;
4. the shell remains usable with multiple examples;
5. Light / Dark / System theme changes continue to propagate to the active example;
6. the full solution builds and Hive.Example.WinForms launches normally.

Implementation is complete for the current UI polish pass. Automated UI verification is intentionally not introduced; developer manual verification is still required for theme-state preservation, resizing, interaction states, CRUD, dialogs, and overall visual acceptance.

## Dependency direction

```
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → public platform contracts + Host.WinForms + Host.WinForms.UI
Tests → projects under test

Coordination may depend on Core + Agents + MAF contracts where required.
No core/platform project may depend on Example.WinForms.
```

## Constraints

- WinForms-specific types remain outside Hive.Core.
- ReaLTaiizor remains exclusively inside Hive.Host.WinForms.UI.
- Hive.Example.WinForms does not reference xUnit runner internals.
- The shell must not become an alternate test runner.
- No central manual example registration table.
- Do not start Phase 0.8 until 0.7 is complete and manually verified.
