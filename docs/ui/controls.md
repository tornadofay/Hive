
# Hive WinForms UI Controls

This document describes the consumer-facing controls in Hive.Host.WinForms.UI.Controls.

## HiveForm

HiveForm is the standard Hive-owned application form shell.

It provides:

- borderless rounded-window presentation;
- Hive window header;
- theme integration;
- DPI-aware WinForms scaling;
- window movement and resizing;
- shared body surface;
- theme-change propagation;
- minimum and initial size conventions.

Subclass it for Hive forms.

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
    }

    private void BuildUi()
    {
        // Add feature UI to BodyPanel.
    }
}
~~~

The base constructor accepts title, subtitle, optional initial size, optional minimum size, and an optional IHiveThemeManager.

Use these protected/public members:

- BodyPanel — main form content surface.
- ConfigureHeader(...) — configure close, minimize, maximize, help, theme-toggle, and movement behavior.
- SetHeaderText(...) — change title/subtitle.
- SetBodyPadding(...) — intentionally change body padding.
- ThemeManager — current IHiveThemeManager.
- Theme — current HiveThemeDefinition.
- SetThemeManager(...) — replace the theme manager when an explicitly shared manager is required.
- OnThemeChanged(...) — override only when the feature owns additional visual state that needs repainting.

Do not recreate the Hive window header in a feature form. The header implementation is internal to HiveForm.

## HiveButton

HiveButton is the Hive-owned action button.

Styles:

- Primary
- Secondary
- Navigation
- NavigationSelected
- Danger

Use semantic styles:

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

HiveButton already handles hover, pressed, disabled, focus, keyboard Enter/Space activation, and theme-aware rendering.

Use a native Button when the native control is sufficient and no Hive-owned button contract is required.

## HiveMessageBox

Use HiveMessageBox instead of direct System.Windows.Forms.MessageBox for Hive UI messaging.

Common methods:

- ShowInformation(...)
- ShowSuccess(...)
- ShowWarning(...)
- ShowError(...)
- ShowQuestion(...)
- Show(...) with HiveMessageOptions

Example:

~~~csharp
var result = HiveMessageBox.ShowQuestion(
    this,
    "Delete the selected provider?",
    "Delete Provider");

if (result != DialogResult.Yes)
    return;
~~~

For technical failures, use HiveMessageOptions.Details for diagnostics that are safe to expose to the developer/user.

Never expose secrets, credentials, or unnecessary sensitive payloads in message details.

## HiveListPageLayout

HiveListPageLayout establishes the standard three-region list page:

~~~text
HeaderPanel
ActionBarPanel
ContentPanel
~~~

Use it for CRUD, settings, configuration, and other list screens.

~~~csharp
var layout = new HiveListPageLayout();

layout.HeaderPanel.Controls.Add(...);
layout.ActionBarPanel.Controls.Add(...);
layout.SetContent(contentControl);
~~~

Do not place competing DockStyle.Top controls directly over the content sibling. The three-region contract exists to prevent header/action/content overlap.

## HiveCrudPage<TItem>

HiveCrudPage<TItem> provides generic CRUD interaction orchestration. It is not an ORM, repository, authorization service, or domain model.

It owns:

- list presentation;
- selection;
- search;
- client-side page slicing of the loaded snapshot;
- Add/Edit/Delete/Refresh actions;
- busy state;
- empty/no-match state;
- status/pagination presentation;
- operation failure notification;
- keyboard interaction.

The consuming feature owns:

- item/domain semantics;
- validation;
- authorization;
- persistence;
- specialized editors;
- loading/edit/delete implementation.

Basic pattern:

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
~~~

await page.RefreshAsync();

`HiveCrudColumn<TItem>` requires a header, a positive display width, and a value selector. Use the actual public constructor rather than bypassing the column contract.

Do not put SQL, management authorization, or domain validation into HiveCrudPage<TItem>.

## HiveEditorLayout

HiveEditorLayout provides the standard labelled editor composition:

- labelled field area;
- responsive label column;
- scrollable fields;
- bottom action footer;
- consistent field rhythm.

Add fields through AddField(title, description, editor, height).

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

Use native editors inside the layout unless Hive already provides a contract-specific editor.

Validation and authorization remain outside HiveEditorLayout.

## HivePaginationBar

HivePaginationBar is the shared paging footer.

Use:

- PageNumber
- CanGoPrevious
- CanGoNext
- PageText

Handle:

- PreviousRequested
- NextRequested

The control does not load data itself.

## HiveNavigationTree

HiveNavigationTree is the themed TreeView used for hierarchical Example Host navigation.

It expects ordinary TreeView nodes and preserves selection/focus/scroll state across theme changes.

Do not rebuild or reassign SelectedNode or TopNode merely because a theme changes.

For ordinary feature navigation outside the Example Host, use the appropriate existing navigation contract rather than treating this TreeView as universal application navigation.

## HiveListView

HiveListView is Hive's themed ListView for lightweight multi-column list surfaces.

It retains native ListView behavior and uses Hive-owned drawing for the visual surface.

Prefer it when the screen needs:

- lightweight multi-column lists;
- single selection;
- native ListView behavior;
- Hive visual states.

Use DataGridView when richer native tabular behavior is specifically required.

## HiveExampleTestSurface

HiveExampleTestSurface is the standard interactive verification surface for reproducible developer examples.

It provides:

- Run/Cancel;
- status;
- test input;
- C# reproduction snippet;
- description;
- expected result;
- optional notes;
- output integration;
- cancellation-aware execution.

Typical pattern:

~~~csharp
var surface = new HiveExampleTestSurface();

surface.SetInformation(
    "Creates a Provider through the public management contract.",
    "A Provider resource is created and then read back.");

surface.InputText = "Example input";
surface.CodeSnippet = """
// Reproduction code
""";

surface.ConfigureRun(
    async cancellationToken =>
    {
        await RunScenarioAsync(cancellationToken);
    },
    output,
    owner);
~~~

Examples should demonstrate real public behavior, not fake the success state.

## HiveExampleOutputView and IHiveExampleOutput

The Example Host provides a shared output surface.

Public contract:

~~~csharp
public interface IHiveExampleOutput
{
    void Clear();
    void Write(string title, string value);
    void Append(string value);
}
~~~

Use IHiveExampleOutput supplied by the Example Host rather than creating a second global output window.

The concrete HiveExampleOutputView supports clear, copy, show/hide, write, append, and line-count/status behavior.

## Internal controls

Some controls are implementation details of the Hive UI foundation and must not become consuming dependencies.

Examples:

- HiveWindowHeader
- HiveBorderPanel

Use the public consumer-facing controls/layouts instead.

## Accessibility

New UI should preserve:

- meaningful AccessibleName;
- useful AccessibleDescription where appropriate;
- keyboard activation;
- visible focus;
- understandable disabled state;
- logical tab order.

Do not add visual-only interactions that cannot be operated through the supported keyboard model.

## UI implementation rule

First compose existing Hive primitives and ordinary WinForms controls. Create a new Hive-owned control only after establishing a real reusable consumer-facing contract that existing primitives cannot satisfy.
