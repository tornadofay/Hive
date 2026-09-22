# Hive Forms — API Quick Reference

## Standard form

Use HiveForm:

```csharp
public sealed class ProviderEditorForm : HiveForm
{
    public ProviderEditorForm()
        : base(
            "Provider",
            "Create or edit a provider",
            new Size(900, 620),
            new Size(760, 520))
    {
        var layout = new HiveEditorLayout();
        var name = new TextBox();

        layout.AddField("Name", "Provider display name.", name);

        var save = layout.AddActionButton(
            "Save",
            HiveButtonStyle.Primary);

        save.Click += async (_, _) => await SaveAsync();

        BodyPanel.Controls.Add(layout);
        ThemeManager.Apply(BodyPanel);
    }
}
```

Do not recreate Hive window/header/theme infrastructure.

## List page

Preferred composition:

```text
HiveForm
  └─ BodyPanel
      └─ HiveListPageLayout
          ├─ HeaderPanel
          ├─ ActionBarPanel
          └─ ContentPanel
```

For generic CRUD, put HiveCrudPage<TItem> in the page content.

## Editor page

Preferred composition:

```text
HiveForm
  └─ BodyPanel
      └─ HiveEditorLayout
```

Use:
- AddField(...)
- AddActionButton(...)

Use the layout's scrolling and responsive label sizing.

## Theme

Public contract:

```csharp
IHiveThemeManager
```

Useful members:
- Mode
- Theme
- ThemeChanged
- SetMode(...)
- Apply(Control)

Modes:
- Light
- Dark
- System

After creating feature controls in a derived form:

```csharp
ThemeManager.Apply(BodyPanel);
```

For a dynamic view:

```csharp
ThemeManager.Apply(view);
```

Do not create a feature-local theme manager.

## Dynamic views

Use this order:

```csharp
var view = CreateView();
Configure(view);
ThemeManager.Apply(view);
host.Controls.Add(view);
```

The owner that replaces a dynamic child is responsible for disposing the previous child.

## Layout

Use normal WinForms:
- Dock
- Anchor
- TableLayoutPanel
- FlowLayoutPanel

Do not introduce a custom DPI/layout system for a normal form.

## UI work

Keep event handlers thin. Call Management/application services from the owning feature.

Do not put SQL, provider transport, or authorization rules into reusable UI controls.

Use async application APIs for database/network/provider work and pass cancellation tokens when the owning contract supports them.

## Dialogs

Use HiveMessageBox for Hive dialogs.

For destructive operations use a Danger action and confirmation.

## Verification

When the active slice requires manual UI verification, check the affected behavior in Light/Dark/System themes, resize, keyboard/focus states, and CRUD/dialog states as applicable.
