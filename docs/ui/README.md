# Hive WinForms UI — Agent Quick Reference

Use this directory only for practical API usage. Architecture and repository rules remain in AGENTS.md and docs/architecture.md. Source code is the final authority.

## Before UI work

Read:
1. AGENTS.md
2. relevant docs/architecture.md section
3. the relevant guide below

Guides:
- controls.md — existing UI controls and APIs.
- forms.md — HiveForm, layout, theme, and lifecycle.
- examples.md — how to add a Hive.Example.WinForms scenario.

## Basic rule

Reuse the smallest existing Hive UI API that fits. Use native WinForms controls when no Hive-specific contract is required.

Do not create a Hive wrapper only to rename a native control.

## Theme rule

HiveForm applies its theme during base construction. Controls added by the derived form afterward may need:

```csharp
ThemeManager.Apply(BodyPanel);
```

For a dynamic view:

```csharp
ThemeManager.Apply(view);
```

Use the shared theme manager; do not create a second one.

## Example rule

Every new meaningful externally usable capability needs:
- focused Hive.Tests coverage;
- a matching Hive.Example.WinForms scenario;
- exact `Example to run:` and `Tests to run:` handoff lines.

Read examples.md for the implementation pattern.
