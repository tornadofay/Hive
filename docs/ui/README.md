# Hive WinForms UI — Agent Reference

Short usage reference only. Source is authoritative.

- `controls.md`: control APIs.
- `forms.md`: common form composition.
- `examples.md`: Example Host API.

Use an existing Hive control before creating a new one.

After composing controls in a derived `HiveForm`:
```csharp
ThemeManager.Apply(BodyPanel);
```

For a dynamic view:
```csharp
ThemeManager.Apply(view);
```

New externally usable capabilities require a matching Example and focused `Hive.Tests` coverage.
