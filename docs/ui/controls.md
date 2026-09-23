# Hive WinForms Controls — Agent Reference

Source is authoritative for exact signatures.

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

Use: `BodyPanel`, `ThemeManager`, `Theme`, `ConfigureHeader(...)`, `SetHeaderText(...)`, `SetBodyPadding(...)`, `SetThemeManager(...)`, `OnThemeChanged(...)`.

## HiveButton

```csharp
new HiveButton
{
    Text = "Save",
    Style = HiveButtonStyle.Primary
};
```

Styles: `Primary`, `Secondary`, `Navigation`, `NavigationSelected`, `Danger`.

## HiveMessageBox

```csharp
HiveMessageBox.ShowInformation(this, "Saved.");
HiveMessageBox.ShowError(this, "Failed.");
var result = HiveMessageBox.ShowQuestion(this, "Delete?", "Delete");
```

Custom:
```csharp
HiveMessageBox.Show(
    this,
    new HiveMessageOptions(
        "Failed",
        "Operation failed.",
        HiveMessageType.Error,
        MessageBoxButtons.OK,
        details));
```

## HiveUiErrorReporter

Use for unexpected user-visible failures:

```csharp
HiveUiErrorReporter.Report(
    this,
    exception,
    "Operation failed",
    "The operation could not be completed.",
    output,
    themeManager);
```

It writes technical exception details to the Output panel when an `IHiveExampleOutput` sink is available and shows a themed error MessageBox with expandable details. Never pass secrets or credential material.

## HiveListPageLayout

```csharp
var layout = new HiveListPageLayout();
layout.HeaderPanel.Controls.Add(header);
layout.ActionBarPanel.Controls.Add(actions);
layout.SetContent(content);
```

Members: `HeaderPanel`, `ActionBarPanel`, `ContentPanel`, `HeaderHeight`, `ActionBarHeight`, `SetContent(Control)`.

## HiveCrudPage<TItem>

```csharp
var page = new HiveCrudPage<Item>
{
    Title = "Items",
    PageSize = 25,
    AllowAdd = true,
    AllowEdit = true,
    AllowDelete = true,
    ShowRefresh = true,
    ShowSearch = true
};

page.SetColumns(
    new HiveCrudColumn<Item>("Name", 220, x => x.Name),
    new HiveCrudColumn<Item>("Kind", 160, x => x.Kind));

page.LoadItemsAsync = LoadAsync;
page.EditItemAsync = EditAsync;
page.DeleteItemAsync = DeleteAsync;
page.GetItemDisplayName = x => x.Name;

await page.RefreshAsync();
```

Column constructor:
```csharp
HiveCrudColumn<TItem>(
    string header,
    int width,
    Func<TItem, string?> valueSelector)
```

Facts:
- search = case-insensitive, loaded items only;
- paging = client-side;
- `EditItemAsync(null, ...)` = Add;
- non-null edit result reloads;
- Delete confirms;
- `OperationFailed` exposes operation failures.

## HiveEditorLayout

```csharp
var layout = new HiveEditorLayout();
layout.AddField("Name", "Provider name.", textBox);
var save = layout.AddActionButton("Save", HiveButtonStyle.Primary);
```

Members: `FieldsPanel`, `FooterPanel`, `LabelColumnWidth`, `ClearFields()`, `AddField(...)`, `AddActionButton(...)`.

## HivePaginationBar

Properties: `PageNumber`, `CanGoPrevious`, `CanGoNext`, `PageText`.

Events: `PreviousRequested`, `NextRequested`.

## HiveNavigationTree

Use for Example Host hierarchical navigation.

## HiveListView

Use for lightweight multi-column ListView. Use DataGridView for richer grid behavior.

## HiveExampleTestSurface

```csharp
surface.SetInformation("Description", "Expected result");
surface.CodeSnippet = """
// public API
""";
surface.ConfigureRun(RunScenarioAsync, output, owner);
```

Members: `InputText`, `CodeSnippet`, `RunButtonText`, `Description`, `ExpectedResult`, `NoteTitle`, `NoteText`, `SetInformation(...)`, `SetStatus(...)`, `ConfigureRun(...)`, `Cancel()`, `RunAsync(...)`.

## IHiveExampleOutput

```csharp
void Clear();
void Write(string title, string value);
void Append(string value);
```
