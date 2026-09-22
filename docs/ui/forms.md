
# Hive Forms, Layout, Theme, and UI Lifecycle

## Form lifecycle

Hive application forms should normally derive from HiveForm.

HiveForm owns:

- Hive window header;
- theme manager subscription;
- body surface;
- window region;
- shared resize behavior;
- deterministic theme unsubscription/disposal.

A derived form should primarily build its feature content.

Do not reimplement:

- a second Hive header;
- a second theme manager;
- custom rounded-form hit testing;
- duplicate window movement/resize logic.

## Recommended construction pattern

Keep construction deterministic.

~~~csharp
public sealed class ProviderEditorForm : HiveForm
{
    private readonly HiveEditorLayout _layout;
    private readonly TextBox _name;

    public ProviderEditorForm()
        : base(
            "Provider",
            "Create or edit a provider",
            new Size(900, 620),
            new Size(760, 520))
    {
        _name = new TextBox();

        _layout = new HiveEditorLayout();

        _layout.AddField(
            "Name",
            "Provider display name.",
            _name);

        var save = _layout.AddActionButton(
            "Save",
            HiveButtonStyle.Primary);

        save.Click += SaveClicked;

        BodyPanel.Controls.Add(_layout);
    }

    private async void SaveClicked(object? sender, EventArgs e)
    {
        await SaveAsync();
    }

    private Task SaveAsync()
    {
        return Task.CompletedTask;
    }
}
~~~

Event handlers must remain thin. Domain/application behavior belongs in the owning Management/application service rather than being implemented inside click handlers.

## List and CRUD pages

Prefer:

~~~text
HiveForm
  └── BodyPanel
       └── HiveListPageLayout
            ├── HeaderPanel
            ├── ActionBarPanel
            └── ContentPanel
~~~

For generic CRUD interaction, prefer HiveCrudPage<TItem>.

Feature-specific logic supplies data and operations.

For specialized lists where CRUD orchestration is not appropriate, use HiveListPageLayout directly.

## Editor pages

Prefer:

~~~text
HiveForm
  └── BodyPanel
       └── HiveEditorLayout
~~~

Use AddField for normal labelled fields and AddActionButton for the standard action footer.

Long forms should use the layout's scrolling behavior rather than inventing manual nested scrolling unless the feature has a specific requirement.

## Responsive sizing

Hive uses normal WinForms layout:

- Dock;
- Anchor;
- TableLayoutPanel;
- FlowLayoutPanel;
- bounded minimum sizes.

Do not build a custom DPI system.

Do not use absolute child positioning as the normal page layout mechanism.

When a shared layout control already handles compact resizing, use it rather than adding another feature-specific responsive implementation.

## Theme system

The public theme contract is IHiveThemeManager.

It exposes:

- Mode;
- Theme;
- ThemeChanged;
- SetMode(HiveThemeMode);
- Apply(Control).

Theme modes:

- Light;
- Dark;
- System.

Normal forms receive a theme manager through HiveForm.

Example:

~~~csharp
ThemeManager.SetMode(HiveThemeMode.Dark);
~~~

Do not cache hard-coded light/dark colors in feature forms when the shared theme contract already provides the required semantic value.

## Theme-change safety

Theme changes are paint/state updates, not an excuse to rebuild the UI.

Do not:

- recreate controls;
- repopulate navigation;
- reset TreeView selection;
- reset scroll position;
- change form geometry;
- recreate resources unnecessarily;
- force expensive control-tree/layout churn.

The existing theme manager intentionally avoids geometry mutations during theme application.

## Resource ownership

Custom UI owns the resources it creates.

For custom forms and controls:

- dispose owned Font, Brush, Pen, GraphicsPath, Region, Image, and similar objects;
- detach event subscriptions;
- cancel/dispose owned CancellationTokenSource instances;
- let WinForms own child controls that were added to the control tree.

Do not dispose objects that the feature does not own.

## UI thread

WinForms controls belong to their UI thread.

Database, network, filesystem, and other blocking work must not execute synchronously on the UI thread.

Use asynchronous APIs.

~~~csharp
private async void RefreshClicked(object? sender, EventArgs e)
{
    _refreshButton.Enabled = false;

    try
    {
        await RefreshAsync();
    }
    finally
    {
        _refreshButton.Enabled = true;
    }
}
~~~

The actual application/management operation should remain outside the UI control where the architecture places it.

## Dialogs and destructive actions

Use HiveMessageBox for Hive dialogs.

Destructive operations use semantic Danger styling and require confirmation.

Do not create message-box variants that bypass the shared semantic dialog contract without an architectural reason.

## Standard list UX

List/configuration pages should normally follow:

1. title/description;
2. one compact search/action row;
3. primary data surface;
4. compact status/pagination footer.

Avoid competing toolbars and excessive explanatory chrome.

## Manual UI verification

User-facing UI changes require actual developer/manual verification when the active slice requires it.

Depending on the change, verify:

- light/dark/system themes;
- selection/focus/hover/disabled states;
- resize behavior;
- compact window behavior;
- dialogs;
- CRUD states;
- loading/empty/no-match/failure states;
- keyboard behavior;
- navigation state preservation.

Static source inspection does not establish visual correctness or usability.
