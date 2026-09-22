# Hive Forms — Agent Reference

## List page

```text
HiveForm
└─ BodyPanel
   └─ HiveListPageLayout
      ├─ HeaderPanel
      ├─ ActionBarPanel
      └─ ContentPanel
```

Use `HiveCrudPage<TItem>` inside ContentPanel for generic CRUD.

## Editor page

```text
HiveForm
└─ BodyPanel
   └─ HiveEditorLayout
      ├─ FieldsPanel
      └─ FooterPanel
```

Use `AddField(...)` and `AddActionButton(...)`.

## Theme

```csharp
IHiveThemeManager
```

Members: `Mode`, `Theme`, `ThemeChanged`, `SetMode(...)`, `Apply(Control)`.

Modes: `Light`, `Dark`, `System`.

After constructing form content:
```csharp
ThemeManager.Apply(BodyPanel);
```

Before showing a dynamically created view:
```csharp
ThemeManager.Apply(view);
host.Controls.Add(view);
```

## Layout

Use normal WinForms:
`Dock`, `Anchor`, `TableLayoutPanel`, `FlowLayoutPanel`.

## Ownership

The owner of a dynamically replaced child disposes the previous child.

UI controls should consume application/Management APIs; do not put SQL or provider transport into reusable UI controls.
