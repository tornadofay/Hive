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

Styles: `Primary`, `Secondary`, `Navigation`, `NavigationSelected`, `Danger`, `Administrative`. `Administrative` is for non-destructive administrative entry points that need stronger visual emphasis than normal secondary actions.

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

It writes sanitized technical exception diagnostics to the Output panel when an `IHiveExampleOutput` sink is available and shows a themed error MessageBox with expandable details. The sanitized diagnostics include exception types and redacted messages but do not expose raw stack traces or common credential forms. Do not pass secrets or credential material.

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

For an already-loaded client-side data source that needs presentation-only filtering without invoking the asynchronous load operation, `SetItemsForView(items)` replaces the displayed CRUD items directly while retaining the shared CRUD/list presentation.

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
- single-line editors use a compact layout slot; `HiveComboBox` uses the Hive-owned composite field sizing while ordinary native ComboBox retains its platform-defined control height and is vertically centered within the slot. Composite fields that embed native editors must preserve the same compact input sizing explicitly.

## HivePaginationBar

Properties: `PageNumber`, `CanGoPrevious`, `CanGoNext`, `PageText`.

Events: `PreviousRequested`, `NextRequested`.

## HiveComboBox

`HiveComboBox` is the single public Hive selection control. Its visible field, dropdown arrow, popup surface, filter field, result rows, selection visuals, and popup scrolling are Hive-rendered; it is no longer required to derive from native `ComboBox`.

```csharp
var combo = new HiveComboBox
{
    DisplayMember = nameof(Option.Name),
    ValueMember = nameof(Option.Id),
    DropDownStyle = ComboBoxStyle.DropDownList
};

combo.DataSource = options;
combo.SelectedValue = 42;

combo.SelectedIndexChanged += (_, _) => { /* selection changed */ };
combo.ShowDropDown();
```

Public selection/data members include `Items`, `DataSource`, `DisplayMember`, `ValueMember`, `SelectedIndex`, `SelectedItem`, `SelectedValue`, `Text`, `DropDownStyle`, `ReadOnly`, `DropDownWidth`, `MaxDropDownItems`, and `ItemHeight`.

The control preserves deterministic source ordering. Popup filtering is transient UI state, uses ordinal case-insensitive matching, and does not mutate the caller's item collection or overwrite the committed selection. Display text and selection identity are separate, so duplicate display text remains unambiguous when `ValueMember` identifies the underlying item.

Long popup lists reuse `HiveScrollHost` / `HiveScrollBar`. Keyboard interaction supports opening/closing, filtering, Up/Down navigation, PageUp/PageDown, Home/End, Enter commit, and Escape cancel. Popup placement is bounded to the current screen working area and flips above the field when there is insufficient room below.

The existing `HiveIntegration` and `HiveField` metadata properties remain part of the public control contract. Ordinary native WinForms `ComboBox` controls remain supported independently by the host integration adapter path.

## HiveScrollBar

`HiveScrollBar` is the reusable Hive-painted scroll surface used by `HiveScrollHost`.

```csharp
var scrollbar = new HiveScrollBar
{
    Orientation = Orientation.Vertical,
    MinimumThumbSize = 20
};

scrollbar.SetState(
    HiveScrollState.Create(
        Orientation.Vertical,
        minimum: 0,
        maximum: contentHeight,
        value: currentPosition,
        viewportSize: viewportHeight,
        smallChange: 16,
        largeChange: viewportHeight));
```

The normalized state treats `Maximum` as the content extent endpoint and derives the effective scroll maximum from `Maximum - ViewportSize`. `Value` is clamped to that effective maximum. The control supports vertical/horizontal orientation, proportional and minimum thumb sizing, track paging, dragging, wheel input, and keyboard navigation. Theme resources are owned and disposed by the control.

## Slice 4 integration

Existing Hive Settings/configuration selectors and the shared `HiveCrudPage` status filter use `HiveComboBox` where the interaction is selection-oriented. Free-form fields that depend on native editable/autocomplete behavior remain native. The Example Host's configured-agent selector also uses `HiveComboBox` because it is a selection surface. Settings Overview content uses `HiveScrollHost`. The Advanced Provider Configuration surface uses `HiveTabControl` for Providers, Accounts / Credentials, Execution Targets, and Model Information navigation; it has no separate Overview tab. Model Information presents its page header before the selectors/inspection filters; it uses compact `HiveComboBox` selectors, the shared `HiveCrudPage` / `HiveListView` presentation for the discovered-model catalog, a single confirmed `Add to Favorites` CRUD action, decision-oriented Model, Price / 1M, Context, Text, Vision, Tools, and Reasoning columns, two compact min/max comparable token-price sliders using $0.01 increments with a data-sized practical maximum, a capability/state filter, and a fixed-width-right Hive scroll surface. A maximum price of $0 is the free-model view: it includes explicit free-pricing evidence or explicitly zero comparable input/output token pricing; missing pricing is not treated as free. The comparable filter uses the highest USD input/output token rate after provider-unit normalization. Structured model information uses a lightweight fixed-width details surface with a title/summary and three lazy HiveTabControl pages: Overview (decision data first), Details (identity/reasoning/limits), and Technical (raw pricing/operational/provider evidence). Detail values are line-oriented and constrained to the available details width so long text wraps and the shared vertical scroll surface remains usable. Provider JSON is rendered indented and separated by property. The page does not use decorative bordered detail cards. Only the initially selected Overview page is materialized at startup; later pages are created on first selection and then reused. Selection changes update existing labels rather than rebuilding nested controls. Model Information also bounds its CRUD search width independently of the shared default so the model-selection surface leaves room for its decision filters; those filter controls wrap responsively and both pricing-evidence checkboxes remain visible. Favorite-backed models display a leading `★` marker. Hive-owned multiline `TextBoxBase` surfaces, `HiveNavigationTree`, and `HiveListView`/CRUD list surfaces are hosted through `HiveScrollHost` where their visible scrollbars use the shared Hive scrollbar treatment. `DataGridView` scrolling remains native.

## HiveTabControl

HiveTabControl is the single public Hive tab-selection control. It is a Hive-owned composite rather than a native TabControl subclass: the tab headers are fully Hive-rendered while conventional TabPage instances remain the page/content model.

```csharp
var tabs = new HiveTabControl();

var overview = tabs.TabPages.Add("Overview");
var details = tabs.TabPages.Add("Details");

tabs.SelectedIndex = 0;
tabs.SelectedTab = details;

tabs.SelectedIndexChanged += (_, _) => { /* selection changed */ };
```

Public members include TabPages, SelectedIndex, SelectedTab, HeaderHeight, SelectNextTab(), SelectPreviousTab(), SelectedIndexChanged, and SelectedTabChanged.

The control preserves TabPage instances while switching pages. Page controls remain ordinary WinForms controls, and removing/clearing pages does not dispose those caller-owned page instances. Selecting a disabled page is ignored for user navigation; programmatic SelectedIndex changes remain available.

Headers support Hive Light/Dark/System rendering, normal/hover/selected/focused/pressed/disabled states, DPI-aware sizing, keyboard navigation, and deterministic horizontal overflow. The selected state uses the shared surface with accent text and a restrained bottom indicator rather than a filled native-style tab, while hover/pressed states provide lightweight feedback. Overflow uses HiveScrollHost / HiveScrollBar; selecting a tab keeps its header visible without forcing the user's scroll position to move merely because the mouse hovers another header. Conventional TabPage content is hosted by an internal WinForms TabControl required by the framework, with its native tab/page chrome kept outside the visible Hive page surface.

Accessibility exposes the control as a page-tab list and reports the selected tab text as its accessible value. The control is not assignable to native TabControl; existing native TabControl consumers remain supported separately until a later explicitly authorized migration.

## HiveScrollHost

Use `HiveScrollHost` for Hive-owned scrollable content where custom scrolling can be controlled without replacing a specialized native control's viewport behavior.

```csharp
var host = new HiveScrollHost();
host.Attach(content);

host.SetScrollPosition(
    horizontal: 0,
    vertical: 120);

var state = host.VerticalScrollState;
```

Members: `Content`, `HorizontalScrollState`, `VerticalScrollState`, `HorizontalScrollPosition`, `VerticalScrollPosition`, `Attach(Control)`, `Detach()`, `Synchronize()`, `SetScrollPosition(...)`, and `ScrollPositionChanged`.

The host accepts one explicit content control and does not dispose a detached/replaced control. While content is attached, normal WinForms parent/child disposal semantics apply. Attaching content that already belongs to another parent is rejected rather than silently reparenting it. The host overlays Hive scrollbars without reserving layout space for them, synchronizes after resize/content changes, routes mouse-wheel input from the attached control subtree into the Hive scroll position, and prevents scrollbar/content feedback loops. Directly hosted native TextBoxBase, TreeView, and ListView controls additionally intercept their Win32 wheel messages before native scrolling consumes them. Native specialized content is not resized or scrollbar-suppressed during transient zero-sized layout states; native suppression begins only after the adapter has acquired authoritative horizontal and vertical state. When a native control does not expose a usable page size, the adapter can recover the standard scrollbar range and position and derive the viewport from the hosted control's client extent.

When the directly attached content is a multiline `TextBoxBase`, `TreeView`, or `ListView`, HiveScrollHost uses a bounded native-scroll adapter: the native viewport and keyboard/mouse behavior remain intact, native scrollbars are hidden, and the HiveScrollBar pair mirrors and controls the native scroll position. `ListView` position changes use the control's native scroll mechanism rather than replacing its item/selection implementation. For `HiveListView`, host attachment suppresses the native scrollbar presentation while preserving the ListView's native scrolling mechanism. Before a handle is created, `HiveScrollHost` arms this suppression before inserting the ListView into its viewport, so the initial native handle/non-client lifecycle cannot expose the standard scrollbar layer before Hive takes ownership. Suppression hides both native standard scrollbars with one `SB_BOTH` Win32 visibility operation and removes the native `WS_HSCROLL` / `WS_VSCROLL` style bits while the control is hosted. During native `WM_NCCALCSIZE` processing, Hive removes those standard scrollbar style bits before the ListView's non-client recalculation and verifies them again after native processing, so a maximize/resize recalculation cannot recreate the native scrollbar layer. Attach, resize, handle-creation, and native style-change lifecycle paths retain deterministic suppression, while high-frequency scrolling and non-client repaint do not perform scrollbar suppression work. During host synchronization, the native ListView viewport and authoritative scroll state are finalized first; Hive scrollbar state/visibility and overlay layout are then updated; native scrollbar suppression remains the final synchronous step of that native-content layout boundary. Native position changes update only the affected adapter-backed Hive scrollbar state instead of running the full resize/layout synchronization path, reducing scroll-time layout churn. In Details/report-style ListView usage, vertical scroll state is normalized from the visible top-item row and row height because the native control scrolls vertically in whole-line increments. This keeps the specialized control implementation native while replacing only its visible scrollbar presentation.

`HiveEditorLayout` uses `HiveScrollHost` for its field region. Hive-owned multiline text surfaces and `HiveNavigationTree` use the same adapter-backed host where custom scrollbar presentation is required. Specialized `ListView` and `DataGridView` scrolling remains native by design.
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


## WinForms host-integration controls

Phase 1.14 provides a bounded set of Hive-owned **host-integration controls**. These are native-control-derived integration bases whose purpose is to make an existing WinForms application understandable and safely interactive through Hive's host-integration contract:

```csharp
HiveForm
HostTextBox
HostComboBox
HostCheckBox
HostDateTimePicker
HostNumericUpDown
HostDataGridView
```

Use these controls when the host application can change its control inheritance and wants the low-code integration path. They expose Hive integration metadata and bounded semantic defaults; they do not move host business semantics into Hive.

HostComboBox is the host-integration ComboBox. Its value is handled through the ordinary native `ComboBox` adapter path.

`HiveComboBox` is separate and remains the single public Hive selection control. It owns Hive's presentation, popup, filtering, selection visuals, and theme behavior. It is not a host-integration base control and is not assignable to native `ComboBox`.

For field controls, use `HiveField` for explicit semantic overrides:

```csharp
var customer = new HostTextBox
{
    Name = "customer"
};
customer.HiveField.Required = true;

var total = new HostTextBox
{
    Name = "total"
};
total.HiveField.Computed = true;

var secret = new HostTextBox
{
    Name = "secret"
};
secret.HiveField.Sensitive = true;
```

Set `Sensitive = true` for field values that must not appear in passive host-context snapshots or captured current-value metadata. Sensitive values remain distinct from authorization; an explicit authorized `ReadControl` operation may still read the live control value when the host exposes that capability.

For data surfaces, use `HiveDataSurface`:

```csharp
var grid = new HostDataGridView
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

Automatic metadata uses safe deterministic conventions first and explicit Hive metadata where supplied. Base metadata does not grant authorization and does not turn `HostDataGridView` into a database or business-write engine. Row operations, lookup execution, validation/save behavior, and business actions remain host-owned through the existing bounded semantic-provider/operation path.

Existing native or custom/third-party controls remain supported through the compatibility adapter and semantic-provider path when a host cannot or should not derive from a Hive host-integration base.
