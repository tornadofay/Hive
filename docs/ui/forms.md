# Hive Forms — API

## HiveForm

```csharp
public sealed class MyForm : HiveForm
{
    public MyForm()
        : base("Title", "Subtitle", new Size(900, 600), new Size(760, 520))
    {
        BuildUi();
        ThemeManager.Apply(BodyPanel);
    }
}
```

Use `BodyPanel` for form content.

Useful members:
- `ConfigureHeader(...)`
- `SetHeaderText(...)`
- `SetBodyPadding(...)`
- `SetThemeManager(...)`
- `ThemeManager`
- `Theme`
- `OnThemeChanged(...)`

## List page

```text
HiveForm
  └─ BodyPanel
      └─ HiveListPageLayout
          ├─ HeaderPanel
          ├─ ActionBarPanel
          └─ ContentPanel
```

Use `HiveCrudPage<TItem>` inside the content when generic CRUD behavior is needed.

## Editor page

```text
HiveForm
  └─ BodyPanel
      └─ HiveEditorLayout
```

Use `AddField(...)` and `AddActionButton(...)`.

## Theme

```csharp
IHiveThemeManager
```

Members: `Mode`, `Theme`, `ThemeChanged`, `SetMode(...)`, `Apply(Control)`.

Modes: `Light`, `Dark`, `System`.

Apply after creating a derived form's content:

```csharp
ThemeManager.Apply(BodyPanel);
```

Apply a dynamic view before showing it:

```csharp
ThemeManager.Apply(view);
host.Controls.Add(view);
```

Use the shared theme manager; do not create a feature-local one.

## Layout

Use normal WinForms `Dock`, `Anchor`, `TableLayoutPanel`, and `FlowLayoutPanel`.

## Ownership

The owner of a dynamic child is responsible for disposing/replacing it.

Do not put SQL, provider transport, or domain rules in reusable UI controls.
