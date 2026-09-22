# Hive WinForms UI — Agent API Guide

Use these files as quick API references. They are intentionally short.

- `controls.md` — UI control APIs.
- `forms.md` — HiveForm, layout, and theme usage.
- `examples.md` — Example Host API and example creation.

Source code is authoritative for exact signatures.

## Rules

Use the smallest existing Hive UI API that fits.

Use native WinForms controls when no Hive-specific behavior is required.

After a derived HiveForm creates its body controls:

```csharp
ThemeManager.Apply(BodyPanel);
```

For a dynamic view:

```csharp
ThemeManager.Apply(view);
```

Every new meaningful externally usable capability also needs a matching Example and focused Hive.Tests coverage. See `examples.md`.
