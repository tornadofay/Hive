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

## Settings form

`HiveSettingsForm` is the application-window shell for the **global Hive package configuration center**. It is the single Settings entry point for durable Hive-owned package configuration; individual domains appear as pages inside it as their contracts become available:

```csharp
var form = new HiveSettingsForm(
    management,
    accessContext,
    themeManager);

form.ShowDialog(owner);
```

`HiveSettingsForm` owns window/header composition only. `HiveSettingsView` owns global Settings navigation and page composition. Provider, ProviderAccount, ExecutionTarget, and AgentDefinition are resource domains and therefore use dedicated CRUD pages with separate HiveEditorLayout-based editor dialogs. The Provider Account and Execution Target pages are scoped by their parent resources so the hierarchy is explicit instead of collapsing unrelated resources into one editor. Persistence is different: it is one global configuration document, so its leaf uses a dedicated editor rather than CRUD.

Settings pages call the appropriate public Management/application boundaries. Settings pages do not construct or own the host Hive service graph; host composition/lifetime remains outside the UI. Future durable Hive configuration domains extend this same Settings center rather than creating parallel top-level settings forms.

The Persistence Server / instance field is a normal free-form text box. It accepts local servers, named instances, remote hosts, IP addresses, and online SQL Server targets. Hive currently has no authoritative server-discovery/catalog contract, so the UI does not attempt to enumerate installed SQL Server instances. The Database field is read-only and assigned automatically to Hive's package database name. Save and Test are non-destructive. The `Initialize Hive` action is the explicit lifecycle operation that may create the configured database when allowed and applies Hive schema migrations; it must not be used as an implicit side effect of Save, Test, or normal Settings-page navigation.
