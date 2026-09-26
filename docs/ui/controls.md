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
- `SetContent(Control)` disposes the previously hosted content control when it is replaced.

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
- `OperationFailed` exposes operation failures. Subscribing is optional; when no subscriber exists, the control keeps the failure inside the UI operation and reports it through `HiveUiErrorReporter` when hosted by a form.
- `SetStatus(text, tone)` can explicitly select `HiveStatusTone.Neutral`, `Information`, `Success`, `Warning`, or `Error`; the existing `SetStatus(text)` remains neutral for backward compatibility.
- the shared search TextBox uses a fixed compact presentation without native AutoSize; the optional status-filter ComboBox keeps native WinForms height and is vertically centered within the toolbar rhythm.
- list rebuild summaries are neutral-toned; transient operation Information/Success/Warning/Error tones do not remain attached to the steady-state count/filter summary.

## HiveEditorLayout

```csharp
var layout = new HiveEditorLayout();
layout.AddField("Name", "Provider name.", textBox);
var save = layout.AddActionButton("Save", HiveButtonStyle.Primary);
```

Members: `FieldsPanel`, `FooterPanel`, `LabelColumnWidth`, `ClearFields()`, `AddField(...)`, `AddActionButton(...)`. Field descriptions use the shared tooltip when their visible text is ellipsized.
- `ClearFields()` disposes the field containers and editors previously added to the layout.
- single-line editors use a 32px compact layout slot; native controls such as ComboBox retain their platform-defined control height and are vertically centered within that slot, while controls such as TextBox use the full slot height. Composite fields that embed native editors must preserve the same compact input sizing explicitly.

## HivePaginationBar

Properties: `PageNumber`, `CanGoPrevious`, `CanGoNext`, `PageText`.

Events: `PreviousRequested`, `NextRequested`.

## HiveNavigationTree

Use for Example Host hierarchical navigation.

## HiveListView

Use for lightweight multi-column ListView. Use DataGridView for richer grid behavior. Selected keyboard focus is indicated across the selected row, not only the first column.

## HiveExampleTestSurface

```csharp
surface.SetInformation("Description", "Expected result");
surface.CodeSnippet = """
// public API
""";
surface.ConfigureRun(RunScenarioAsync, output, owner);
```

Members: `InputText`, `CodeSnippet`, `RunButtonText`, `Description`, `ExpectedResult`, `NoteTitle`, `NoteText`, `SetInformation(...)`, `SetStatus(...)`, `ConfigureRun(...)`, `Cancel()`, `RunAsync(...)`.
- the Run action is disabled until `ConfigureRun(...)` supplies an executable action; it becomes the Cancel action while a run is active.

## IHiveExampleOutput

```csharp
void Clear();
void Write(string title, string value);
void Append(string value);
```


## WinForms host-integration base controls

Phase 1.14 provides a bounded set of native-control-derived integration bases:

```csharp
HiveForm
HiveTextBox
HiveComboBox
HiveCheckBox
HiveDateTimePicker
HiveNumericUpDown
HiveDataGridView
```

They preserve the normal WinForms control API while exposing Hive-owned integration metadata. Hosts do not need one-off wrappers merely to obtain the common integration behavior.

For field controls, use `HiveField` for explicit semantic overrides:

```csharp
var customer = new HiveTextBox
{
    Name = "customer"
};
customer.HiveField.Required = true;

var total = new HiveTextBox
{
    Name = "total"
};
total.HiveField.Computed = true;
```

For data surfaces, use `HiveDataSurface`:

```csharp
var grid = new HiveDataGridView
{
    Name = "invoiceLines"
};

grid.HiveDataSurface.SurfaceId = "invoiceLines";
grid.HiveDataSurface.PrimaryKeyField = "Id";
grid.HiveDataSurface.ConfigureField("Id").IsPrimaryKey = true;
grid.HiveDataSurface.ConfigureField("ProductId").Lookup = lookup;
grid.HiveDataSurface.ParentSurfaceId = "surface:invoice";
grid.HiveDataSurface.ParentKeyField = "Id";
grid.HiveDataSurface.ChildKeyField = "InvoiceId";
```

For a `HiveForm`, `HiveHostIntegration.HostName` can override the host display identity without changing the normal WinForms form lifecycle.

Automatic metadata uses safe deterministic conventions first and explicit Hive metadata where supplied. Base metadata does not grant authorization and does not turn `HiveDataGridView` into a database or business-write engine. Row operations, lookup execution, validation/save behavior, and business actions remain host-owned through the existing bounded semantic-provider/operation path.
