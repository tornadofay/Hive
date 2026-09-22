# Hive WinForms UI — API Quick Reference

Source code is authoritative for exact signatures.

## HiveForm

Derive feature forms from HiveForm:

```csharp
public sealed class ProviderForm : HiveForm
{
    public ProviderForm()
        : base(
            "Providers",
            "Manage providers",
            new Size(1120, 720),
            new Size(900, 600))
    {
        BuildUi();
        ThemeManager.Apply(BodyPanel);
    }
}
```

Constructor:
```csharp
HiveForm(
    string title,
    string subtitle = "",
    Size? initialSize = null,
    Size? minimumSize = null,
    IHiveThemeManager? themeManager = null)
```

Useful members:
- protected BodyPanel
- ThemeManager
- Theme
- ConfigureHeader(...)
- SetHeaderText(...)
- SetBodyPadding(...)
- SetThemeManager(...)
- OnThemeChanged(...)

## HiveButton

```csharp
var save = new HiveButton
{
    Text = "Save",
    Style = HiveButtonStyle.Primary,
    Width = 104,
    Height = 36
};
```

Styles:
- Primary
- Secondary
- Navigation
- NavigationSelected
- Danger

Use native Button when it already satisfies the requirement.

## HiveMessageBox

```csharp
HiveMessageBox.ShowInformation(this, "Saved.");
HiveMessageBox.ShowWarning(this, "Invalid input.");
HiveMessageBox.ShowError(this, "Operation failed.");
var result = HiveMessageBox.ShowQuestion(this, "Delete item?", "Delete");
```

For technical details:
```csharp
HiveMessageBox.Show(
    this,
    new HiveMessageOptions(
        "Operation failed",
        "The operation could not be completed.",
        HiveMessageType.Error,
        MessageBoxButtons.OK,
        details));
```

Do not put secrets in Details.

## HiveListPageLayout

```csharp
var layout = new HiveListPageLayout();
layout.HeaderPanel.Controls.Add(...);
layout.ActionBarPanel.Controls.Add(...);
layout.SetContent(content);
```

Members:
- HeaderPanel
- ActionBarPanel
- ContentPanel
- HeaderHeight
- ActionBarHeight
- SetContent(Control)

## HiveCrudPage<TItem>

Use for generic list CRUD UI:

```csharp
var page = new HiveCrudPage<ProviderItem>
{
    Title = "Providers",
    Description = "Configured providers",
    PageSize = 25,
    AllowAdd = true,
    AllowEdit = true,
    AllowDelete = true,
    ShowRefresh = true,
    ShowSearch = true
};

page.SetColumns(
    new HiveCrudColumn<ProviderItem>("Name", 220, x => x.Name),
    new HiveCrudColumn<ProviderItem>("Kind", 160, x => x.Kind));

page.LoadItemsAsync = LoadAsync;
page.EditItemAsync = EditAsync;
page.DeleteItemAsync = DeleteAsync;
page.GetItemDisplayName = x => x.Name;

await page.RefreshAsync();
```

`HiveCrudColumn<TItem>` constructor:
```csharp
(string header, int width, Func<TItem, string?> valueSelector)
```

Important:
- search is case-insensitive and client-side;
- paging is over the loaded item snapshot;
- EditItemAsync(null, ...) means Add;
- returning a non-null edited item reloads the list;
- Delete uses built-in confirmation;
- subscribe to OperationFailed when the feature must handle failures itself.

## HiveEditorLayout

```csharp
var layout = new HiveEditorLayout();
layout.AddField("Name", "Provider display name.", textBox);

var save = layout.AddActionButton(
    "Save",
    HiveButtonStyle.Primary,
    104);
```

Members:
- FieldsPanel
- FooterPanel
- LabelColumnWidth
- ClearFields()
- AddField(...)
- AddActionButton(...)

The layout owns the added editor control.

## HivePaginationBar

Properties:
- PageNumber
- CanGoPrevious
- CanGoNext
- PageText

Events:
- PreviousRequested
- NextRequested

It does not load data.

## HiveNavigationTree

Use for the current Example Host hierarchical navigation. Do not rebuild selection/scroll state on theme change.

## HiveListView

Use for lightweight native multi-column ListView screens.

Use DataGridView when richer grid behavior is required.

## HiveExampleTestSurface

Use for interactive Example scenarios:

```csharp
surface.SetInformation(
    "What this demonstrates.",
    "Expected result.");

surface.CodeSnippet = """
// Public API example
""";

surface.ConfigureRun(
    RunScenarioAsync,
    output,
    owner);
```

Useful members:
- InputText
- CodeSnippet
- RunButtonText
- Description
- ExpectedResult
- NoteTitle
- NoteText
- SetInformation(...)
- SetStatus(...)
- ConfigureRun(...)
- Cancel()
- RunAsync(...)

The action must execute the real API.

## IHiveExampleOutput

```csharp
public interface IHiveExampleOutput
{
    void Clear();
    void Write(string title, string value);
    void Append(string value);
}
```

Use the Example Host supplied implementation; do not create another global output surface.
