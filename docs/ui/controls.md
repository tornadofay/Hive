# Hive WinForms Controls — API

Source code is authoritative for exact signatures.

## HiveForm

```csharp
public sealed class MyForm : HiveForm
{
    public MyForm()
        : base("Title", "Subtitle", new Size(900, 600), new Size(760, 520))
    {
        BodyPanel.Controls.Add(content);
        ThemeManager.Apply(BodyPanel);
    }
}
```

Members:
`BodyPanel`, `ThemeManager`, `Theme`, `ConfigureHeader(...)`, `SetHeaderText(...)`, `SetBodyPadding(...)`, `SetThemeManager(...)`, `OnThemeChanged(...)`.

## HiveButton

```csharp
new HiveButton
{
    Text = "Save",
    Style = HiveButtonStyle.Primary,
    Width = 104,
    Height = 36
};
```

Styles: `Primary`, `Secondary`, `Navigation`, `NavigationSelected`, `Danger`.

## HiveMessageBox

```csharp
HiveMessageBox.ShowInformation(this, "Saved.");
HiveMessageBox.ShowWarning(this, "Check the input.");
HiveMessageBox.ShowError(this, "Operation failed.");
var result = HiveMessageBox.ShowQuestion(this, "Delete?", "Delete");
```

Custom details:
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

Do not put secrets in `Details`.

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
    Description = "Configured items",
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

`HiveCrudColumn<TItem>`:
```csharp
(string header, int width, Func<TItem, string?> valueSelector)
```

Key behavior:
- search is case-insensitive over loaded items;
- paging is client-side over the loaded snapshot;
- `EditItemAsync(null, ...)` means Add;
- non-null edit result reloads the list;
- Delete uses confirmation;
- `OperationFailed` lets the feature handle operation errors.

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

It does not load data.

## HiveNavigationTree

Use for the Example Host's hierarchical navigation.

## HiveListView

Use for lightweight multi-column ListView screens. Use DataGridView for richer grid behavior.

## HiveExampleTestSurface

```csharp
surface.SetInformation(
    "What this demonstrates.",
    "Expected result.");

surface.CodeSnippet = """
// Public API reproduction
""";

surface.ConfigureRun(
    RunScenarioAsync,
    output,
    owner);
```

Useful members:
`InputText`, `CodeSnippet`, `RunButtonText`, `Description`, `ExpectedResult`, `NoteTitle`, `NoteText`, `SetInformation(...)`, `SetStatus(...)`, `ConfigureRun(...)`, `Cancel()`, `RunAsync(...)`.

## IHiveExampleOutput

```csharp
public interface IHiveExampleOutput
{
    void Clear();
    void Write(string title, string value);
    void Append(string value);
}
```
