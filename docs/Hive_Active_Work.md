# Hive — Active Work

Last updated: 2026-09-21

## Active slice

**0.7 — First-Class Example Host Shell**

Phase 0.6 was accepted after the developer exercised the shared UI foundation in the real Example application, including CRUD editing/deletion, pagination, editor interaction, and error/failure presentation. The implementation now proceeds to the documented 0.7 shell; do not begin Phase 0.8 or later slices.

## Objective

Turn Hive.Example.WinForms into a permanent developer-facing host with scalable Category → Subcategory → Example navigation and one replaceable right-side UserControl view.

The shell must discover IHiveExample implementations without a central manual registration list, own navigation/view lifetime, and consume only Hive-owned UI contracts.

## 0.7 implementation scope

- add the IHiveExample discovery contract;
- discover examples from the designated Example assembly;
- build a left-side Category → Subcategory → Example tree;
- replace the active right-side view without stacking or overlapping pages;
- dispose the previous example view deterministically;
- provide the shared Example services through IServiceProvider;
- keep the existing 0.6 UI foundation as the first discoverable example;
- preserve Light / Dark / System theme behavior through the shared IHiveThemeManager;
- no feature-specific platform functionality is added to the shell.

## Verification

1. adding an IHiveExample implementation makes it appear without editing shell registration code;
2. category/subcategory/example navigation selects exactly one active view;
3. switching examples disposes the previous view and does not accumulate controls;
4. the shell remains usable with multiple examples;
5. Light / Dark / System theme changes continue to propagate to the active example;
6. the full solution builds and Hive.Example.WinForms launches normally.

No 0.7 verification is recorded yet.

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
