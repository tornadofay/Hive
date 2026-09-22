# Hive WinForms UI Controls

This document describes the current consumer-facing controls and the important behaviors that a Hive UI consumer needs to know.

Source code remains authoritative for the exact API surface.

## HiveForm

HiveForm is the standard Hive-owned application form shell.

It currently provides:

- borderless rounded-window presentation;
- Hive window header;
- theme-manager integration;
- DPI-aware WinForms scaling;
- window movement and resize behavior;
- shared body surface;
- theme-change propagation;
- minimum and initial size defaults.

HiveForm is abstract. Derive a feature form from it.

~~~csharp
public sealed class ProvidersForm : HiveForm
{
    public ProvidersForm()
        : base(
            "Providers",
            "Manage Hive provider resources",
            new Size(1120, 720),
            new Size(900, 600))
    {
        BuildUi();
        ThemeManager.Apply(BodyPanel);
    }

    private void BuildUi()
    {
        // Add feature UI to BodyPanel.
    }
}
~~~

The protected base constructor accepts:

- title;
- optional subtitle;
- optional initial size;
- optional minimum size;
- optional IHiveThemeManager.

Consumer-facing members include:

- ThemeManager — current shared theme manager.
- Theme — current theme definition.
- BodyPanel — protected main content surface.
- ConfigureHeader(...) — controls header capabilities such as move, close, minimize, maximize, help, and theme toggle.
- SetHeaderText(...) — updates title and subtitle.
- SetBodyPadding(...) — changes body padding intentionally.
- SetThemeManager(...) — replaces the theme manager when the form must explicitly switch to another shared manager.
- OnThemeChanged(...) — protected virtual hook for feature-owned visual state.

Do not recreate the Hive window header, rounded-window behavior, or theme manager in a feature form.

### Important construction rule

HiveForm applies its theme during base construction, before the derived constructor has finished adding the feature controls.

After the feature controls are composed, call:

~~~csharp
ThemeManager.Apply(BodyPanel);
~~~

For a dynamically created child UserControl:

~~~csharp
ThemeManager.Apply(childView);
~~~

This is especially important for Hive-owned controls such as HiveButton, HiveListView, HiveNavigationTree, HiveEditorLayout, HiveCrudPage<TItem>, and HiveExampleTestSurface.

## HiveButton

HiveButton is the Hive-owned semantic action button.

Styles:

- Primary
- Secondary
- Navigation
- NavigationSelected
- Danger

Example:

~~~csharp
var save = new HiveButton
{
    Text = "Save",
    Style = HiveButtonStyle.Primary,
    Width = 104,
    Height = 36
};

var cancel = new HiveButton
{
    Text = "Cancel",
    Style = HiveButtonStyle.Secondary,
    Width = 104,
    Height = 36
};

var delete = new HiveButton
{
    Text = "Delete",
    Style = HiveButtonStyle.Danger,
    Width = 104,
    Height = 36
};
~~~

HiveButton provides themed hover, pressed, disabled, and focus states. Enter and Space activate the button through the supported keyboard interaction.

Use a native WinForms Button when it already satisfies the feature's behavior and the shared Hive theme is sufficient. Do not wrap a native Button only to rename it.

The control has a default minimum size of 88 x 36. Feature layouts may choose larger widths to keep labels readable.

## HiveMessageBox and HiveMessageOptions

Use HiveMessageBox for Hive-owned dialogs instead of direct System.Windows.Forms.MessageBox when the interaction belongs to the Hive UI surface.

Common helpers include:

- ShowInformation(...)
- ShowSuccess(...)
- ShowWarning(...)
- ShowError(...)
- ShowQuestion(...)
- Show(...) with HiveMessageOptions
- Show(...) overloads that map a standard MessageBoxIcon to a Hive message type

Example:

~~~csharp
var result = HiveMessageBox.ShowQuestion(
    this,
    "Delete the selected provider?",
    "Delete Provider");

if (result != DialogResult.Yes)
    return;
~~~

HiveMessageOptions can carry:

- title;
- message;
- Hive message type;
- button set;
- optional technical details;
- whether technical details start expanded.

Use Details for diagnostic information that is safe to expose. Never put secrets, credentials, tokens, or unnecessary sensitive data into dialog details.

## HiveListPageLayout

HiveListPageLayout is the standard three-region list/configuration layout:

~~~text
HeaderPanel
ActionBarPanel
ContentPanel
~~~

Example:

~~~csharp
var layout = new HiveListPageLayout();

layout.HeaderPanel.Controls.Add(...);
layout.ActionBarPanel.Controls.Add(...);
layout.SetContent(contentControl);
~~~

Consumer-facing members include:

- HeaderPanel
- ActionBarPanel
- ContentPanel
- HeaderHeight
- ActionBarHeight
- SetContent(Control)

HeaderHeight and ActionBarHeight can be adjusted when a feature has a real layout requirement.

SetContent replaces the controls currently held by ContentPanel and docks the supplied control to Fill. Treat displaced controls as the caller's responsibility for lifetime unless the feature explicitly owns/disposes them elsewhere; do not assume SetContent is a disposal API.

The three-region layout exists to keep page hierarchy and spacing predictable. Do not layer competing top-docked toolbars over the same content region.

## HiveCrudPage<TItem>

HiveCrudPage<TItem> provides generic CRUD interaction orchestration. It is not an ORM, repository, authorization service, domain model, or persistence abstraction.

### It owns

- list presentation;
- single-item selection;
- case-insensitive client-side search;
- client-side paging of the loaded item snapshot;
- Add/Edit/Delete/Refresh action presentation;
- busy state;
- empty and no-match states;
- status and pagination presentation;
- built-in delete confirmation;
- keyboard Enter/Delete interaction on the list;
- operation failure notification.

### The consuming feature owns

- domain semantics;
- validation;
- authorization;
- persistence;
- specialized editors;
- the actual load/edit/delete implementation;
- any server-side filtering/paging strategy that is required by a larger dataset or remote service.

The page searches across the values returned by every configured HiveCrudColumn<TItem>. Search is case-insensitive and operates only on the currently loaded IReadOnlyList<TItem>. It is not server-side filtering.

### Basic configuration

~~~csharp
var page = new HiveCrudPage<ProviderListItem>
{
    Title = "Providers",
    Description = "Configured provider resources",
    PageSize = 25,
    AllowAdd = true,
    AllowEdit = true,
    AllowDelete = true,
    ShowRefresh = true,
    ShowSearch = true
};

page.SetColumns(
    new HiveCrudColumn<ProviderListItem>(
        "Name",
        220,
        item => item.Name),
    new HiveCrudColumn<ProviderListItem>(
        "Kind",
        180,
        item => item.Kind));

page.LoadItemsAsync = LoadProvidersAsync;
page.EditItemAsync = EditProviderAsync;
page.DeleteItemAsync = DeleteProviderAsync;
page.GetItemDisplayName = item => item.Name;

await page.RefreshAsync();
~~~

HiveCrudColumn<TItem> requires:

~~~csharp
public HiveCrudColumn(
    string header,
    int width,
    Func<TItem, string?> valueSelector)
~~~

The header must be non-empty, width must be positive, and the selector is required.

### Edit delegate semantics

EditItemAsync is used for both creation and editing:

- null input means "add a new item";
- a non-null input means "edit this item";
- returning null means no replacement item was produced;
- returning a non-null item causes the page to reload the item snapshot.

### Operation failure behavior

Subscribe to OperationFailed when the consuming feature needs to classify or present failures itself.

~~~csharp
page.OperationFailed += (_, e) =>
{
    // Map e.Operation and e.Exception to the owning application's
    // error/diagnostic policy.
};
~~~

If no OperationFailed handler is attached, the control rethrows the operation exception after updating its failure status. Do not assume failures disappear silently.

The event identifies the operation through HiveCrudOperation:

- Load
- Edit
- Delete

### Useful public state

HiveCrudPage<TItem> also exposes:

- PageLayout;
- ListView;
- HeaderPanel;
- ActionBarPanel;
- StatusLabel;
- SearchBox;
- PaginationBar;
- Items;
- SelectedItem;
- IsBusy;
- SearchText;
- SearchPlaceholder;
- PageSize;
- PageNumber;
- ShowPagination;
- Columns;
- LoadItemsAsync;
- EditItemAsync;
- DeleteItemAsync;
- GetItemDisplayName;
- SetColumns(...);
- SetStatus(...);
- RefreshAsync(...)

The page ignores new CRUD commands while busy. RefreshAsync accepts a CancellationToken and requires LoadItemsAsync to be configured. Edit/Delete operations use the page's internal operation lifecycle and cancellation handling.

### Responsive behavior

The action bar switches to a compact layout below its current width breakpoint. When search is visible, the compact layout moves search above the action buttons instead of compressing everything into one row.

Do not replace this with a second feature-specific responsive toolbar unless the screen genuinely has requirements outside the reusable contract.

### Delete behavior

Delete is never performed directly from the button click. The page:

1. requires a selected item;
2. obtains its display name;
3. asks for confirmation through HiveMessageBox;
4. calls DeleteItemAsync only after confirmation;
5. reloads the item snapshot after successful deletion.

Do not duplicate the same confirmation flow in the feature unless the feature's contract explicitly requires a different user interaction.

## HiveEditorLayout

HiveEditorLayout provides a standard labelled editor composition:

- responsive label column;
- scrollable field area;
- consistent label/description treatment;
- bottom action footer;
- standard HiveButton action creation.

Example:

~~~csharp
var editor = new TextBox();

var layout = new HiveEditorLayout();

layout.AddField(
    "Name",
    "Provider display name.",
    editor);

var save = layout.AddActionButton(
    "Save",
    HiveButtonStyle.Primary,
    104);

save.Click += async (_, _) => await SaveAsync();
~~~

AddField accepts:

- title;
- description;
- editor control;
- optional row height.

The layout reparents the editor into its own field host and sets the editor to DockStyle.Fill. After AddField returns, treat the layout as the owner of that child control unless you explicitly remove it.

The label column is responsive. The current implementation uses approximately 168 pixels at normal widths, 136 pixels below 720, and 112 pixels below 560. LabelColumnWidth is exposed for consumer control, but responsive resizing can change the effective width again.

Other useful members:

- FieldsPanel;
- FooterPanel;
- LabelColumnWidth;
- ClearFields();
- AddField(...);
- AddActionButton(...).

Validation, authorization, persistence, and domain semantics remain outside HiveEditorLayout.

## HivePaginationBar

HivePaginationBar is the shared paging footer.

Public state:

- PageNumber;
- CanGoPrevious;
- CanGoNext;
- PageText.

Events:

- PreviousRequested;
- NextRequested.

The control does not load data. The consumer responds to the events and updates the page state.

PageNumber must be at least 1. Changing PageNumber or navigation state updates the default page text; set custom PageText after updating the page number if the feature needs different wording.

## HiveNavigationTree

HiveNavigationTree is the Hive-themed TreeView used by the current Example Host hierarchy.

It expects normal TreeNode objects and provides Hive-specific rendering, selection/focus visuals, hover visuals, and expand/collapse glyphs.

Theme application repaints the existing tree; it does not intentionally reassign SelectedNode or TopNode. Do not rebuild the tree or reset selection/scroll merely because the theme changed.

It is not universal application navigation. The current architecture uses it as the Example Host navigation surface. Other application navigation should use the appropriate application/navigation contract.

## HiveListView

HiveListView is the Hive-themed native WinForms ListView for lightweight multi-column list surfaces.

Current defaults include:

- Details view;
- full-row selection;
- single selection;
- no gridlines;
- non-clickable column headers;
- Hive owner-drawn row/header visuals;
- hover, selected, focus, and disabled visual states.

Prefer it when the screen needs a lightweight native ListView with Hive styling.

Use DataGridView when richer tabular behavior such as richer native grid interaction is actually required. HiveListView does not provide a sortable DataGridView-style contract.

## HiveExampleTestSurface

HiveExampleTestSurface is the shared interactive surface for reproducible Example Host scenarios.

It provides:

- Run / Cancel action;
- status;
- editable test input;
- read-only C# reproduction snippet;
- Description;
- ExpectedResult;
- optional NoteTitle / NoteText;
- cancellation-aware execution;
- failure reporting.

Useful members include:

~~~csharp
surface.InputText
surface.CodeSnippet
surface.RunButtonText
surface.Description
surface.ExpectedResult
surface.NoteTitle
surface.NoteText

surface.SetInformation(...)
surface.SetStatus(...)
surface.ConfigureRun(...)
surface.Cancel()
await surface.RunAsync(...)
~~~

Typical setup:

~~~csharp
var surface = new HiveExampleTestSurface();

surface.SetInformation(
    "Creates a Provider through the public management/persistence contract.",
    "The resource is persisted and the expected identity/version state is shown.");

surface.CodeSnippet = """
// Public API reproduction
""";

surface.ConfigureRun(
    RunScenarioAsync,
    output,
    owner);
~~~

RunAsync catches cancellation and operation failures for the interactive surface. On failure it writes exception details to the supplied output when available and shows a Hive error dialog through the owning form. Do not build an unrelated background thread around RunAsync merely to make it asynchronous; the action itself should be asynchronous and cancellation-aware.

RequireInput(string) is available when an example needs a simple non-empty input guard.

## HiveExampleOutputView and IHiveExampleOutput

IHiveExampleOutput is the small public output contract:

~~~csharp
public interface IHiveExampleOutput
{
    void Clear();
    void Write(string title, string value);
    void Append(string value);
}
~~~

HiveExampleOutputView is the Example Host's concrete shared output surface.

It supports:

- clear;
- copy;
- show/hide;
- line count/status metadata;
- replacement output through Write;
- incremental output through Append.

Write replaces the current output and prepends a local timestamp.
Append adds text exactly as supplied; include your own newline when you want one.

Use the shared IHiveExampleOutput supplied by the Example Host instead of creating another global output window.

## Internal foundation controls

Some implementation controls are intentionally not consumer contracts, including:

- HiveWindowHeader;
- HiveBorderPanel when it is serving only as an internal rendering/layout helper.

Do not build application dependencies on internal implementation controls simply because they are visible in the source tree.

## Theme and custom controls

When adding a control that needs Hive theme colors or states:

1. prefer existing Hive controls/theme semantics;
2. add a real consumer-facing behavior contract if a new control is justified;
3. implement theme application through the existing IHiveThemeManager path;
4. dispose owned System.Drawing resources deterministically;
5. preserve UI-thread affinity;
6. do not make a custom control responsible for persistence, authorization, or domain policy.

## Accessibility

New user-facing UI should preserve, where applicable:

- AccessibleName;
- AccessibleDescription;
- a meaningful AccessibleRole;
- visible focus;
- keyboard activation;
- logical tab order;
- understandable disabled state.

Do not add mouse-only interactions when the same supported action should be keyboard-operable.
