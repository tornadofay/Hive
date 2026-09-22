# Hive Forms, Layout, Theme, and UI Lifecycle

This guide covers the normal lifecycle for Hive WinForms forms and composed UserControls.

Source remains authoritative for exact behavior.

## Form lifecycle

Hive application forms should normally derive from HiveForm.

HiveForm owns:

- Hive window header;
- shared theme manager subscription;
- body surface;
- borderless rounded-window behavior;
- shared resize handling;
- deterministic theme unsubscription/disposal.

A derived form should primarily compose feature content.

Do not reimplement:

- a second Hive header;
- a second theme manager;
- custom rounded-form hit testing;
- duplicate window movement/resize logic.

## Recommended construction pattern

Build the feature UI after the base constructor and then explicitly apply the shared theme to the composed content.

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

        ThemeManager.Apply(BodyPanel);
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

The explicit ThemeManager.Apply call is important because HiveForm's initial theme application occurs before the derived form has finished adding its feature controls.

For a child view created dynamically:

~~~csharp
var view = new ProviderView(...);
ThemeManager.Apply(view);
viewHost.Controls.Add(view);
~~~

The Example Host follows the same principle: the new view is themed before it becomes the visible active view.

## Theme manager

The public contract is IHiveThemeManager.

It exposes:

- Mode;
- Theme;
- ThemeChanged;
- SetMode(HiveThemeMode);
- Apply(Control).

Modes:

- Light;
- Dark;
- System.

Example:

~~~csharp
ThemeManager.SetMode(HiveThemeMode.Dark);
~~~

Use the shared manager from the form or Example Host. Do not create feature-local theme managers unless the architecture explicitly requires a separate theme boundary.

### What Apply does

ThemeManager.Apply(Control root) recursively applies Hive theme state to the supplied control tree.

It updates colors and Hive-owned themed controls; it is not a layout engine.

Use it:

- after composing a new form body;
- after constructing a dynamic UserControl tree;
- when a feature adds a detached control subtree that needs immediate theme application.

Do not call Apply repeatedly from every child control just to make them look themed. Apply at the owner/composition boundary.

## Theme-change safety

Theme changes are visual-state updates, not a reason to reconstruct the screen.

Do not:

- recreate controls;
- rebuild Example Host navigation;
- reset TreeView selection;
- reset scroll positions;
- change form geometry merely because the theme changed;
- repeatedly dispose/recreate resources in consuming forms when the owning control already manages them;
- trigger unnecessary layout work.

The current theme manager intentionally avoids rewriting layout geometry while applying colors.

For HiveNavigationTree specifically, theme application repaints the existing native tree and avoids reassigning selection or TopNode.

## Form header

Use ConfigureHeader when a form needs a different allowed header action set.

Example:

~~~csharp
ConfigureHeader(
    allowMove: true,
    allowClose: true,
    allowMinimize: true,
    allowMaximize: true,
    allowHelp: false,
    allowThemeToggle: true);
~~~

Do not create another header row just to add these capabilities.

SetHeaderText is the supported way to update the form title/subtitle after construction.

## Body padding

BodyPanel is the protected form content surface.

Use SetBodyPadding when the form needs a deliberate padding change.

Keep the body composition inside BodyPanel and let nested controls own their own internal layout.

Do not use the form's outer window region as a substitute for content spacing.

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

For generic CRUD behavior, place HiveCrudPage<TItem> inside the body and let it own selection/search/paging/action orchestration.

Feature-specific logic should provide the data and operations rather than editing HiveCrudPage internals.

For a specialized list where generic CRUD behavior does not fit, use HiveListPageLayout directly.

## Editor pages

Prefer:

~~~text
HiveForm
  └── BodyPanel
       └── HiveEditorLayout
~~~

Use AddField for normal labelled fields and AddActionButton for standard bottom actions.

The editor layout provides a scrollable field area and a responsive label column. Do not rebuild these mechanics on every form.

For a long form, rely on the layout's scrolling behavior unless the feature has a specific layout requirement that cannot be expressed by the existing contract.

## Responsive sizing

Use normal WinForms layout:

- Dock;
- Anchor;
- TableLayoutPanel;
- FlowLayoutPanel;
- bounded minimum sizes.

Do not build a custom DPI system.

Do not use absolute child positioning as the standard page-layout technique.

When a reusable Hive layout already has compact breakpoints, use those behaviors instead of introducing another feature-specific breakpoint system.

Examples:

- HiveCrudPage moves its search/actions arrangement when the available width becomes compact.
- HiveExampleTestSurface changes its input/code arrangement below its current compact breakpoint.
- HiveEditorLayout reduces its label column at smaller widths.
- HiveExampleHostForm narrows its navigation column for smaller supported host widths.

These are current implementation behaviors, not permission to introduce a generic responsive framework.

## UI thread and asynchronous work

WinForms controls belong to their owning UI thread.

Database, network, filesystem, provider, and other blocking work must not run synchronously from a UI event handler.

Prefer:

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

The actual persistence/provider/application operation belongs in the owning Management/application service or other authorized boundary.

Do not use Task.Run merely to hide synchronous I/O or compensate for a blocking application design.

Use cancellation tokens for external work when the owning application contract supports cancellation.

## Dynamic views and theme application

The normal sequence for a dynamically created UserControl is:

1. create the view;
2. configure it;
3. apply the shared theme;
4. attach it to the visible host;
5. let the view participate in the normal UI lifecycle.

Example:

~~~csharp
var view = example.CreateView(services);
ArgumentNullException.ThrowIfNull(view);

ThemeManager.Apply(view);
viewHost.Controls.Add(view);
~~~

The current Example Host also disposes the previous active view when it replaces it. Feature forms that create and own dynamic child controls should use equally explicit lifetime ownership.

## Resource ownership

Custom UI owns the resources it creates.

Dispose owned:

- Font;
- Brush;
- Pen;
- GraphicsPath;
- Region;
- Image;
- timers;
- CancellationTokenSource instances;
- event subscriptions;
- child controls that the feature explicitly owns outside the normal WinForms parent/child lifetime.

WinForms parent controls normally own their child controls once those children are in the control tree. Do not dispose an object that another component owns.

Do not assume Controls.Clear() is a disposal API for every removed control. When a feature replaces a child view, make the lifetime ownership explicit and dispose it when the owner is responsible.

## Dialogs and destructive actions

Use HiveMessageBox for Hive-owned dialogs.

Destructive actions should use:

- semantic Danger styling for the destructive action;
- explicit confirmation before irreversible work;
- safe technical details when reporting failures.

Do not put credentials, provider secrets, or sensitive payloads into Details.

## Standard list UX

List/configuration pages should normally have:

1. title and description;
2. one compact search/action area;
3. the primary data surface;
4. a compact status/pagination footer.

Avoid competing toolbars and excessive explanatory chrome.

For CRUD, prefer HiveCrudPage<TItem> rather than recreating search, paging, empty-state, confirmation, and busy-state plumbing in the feature form.

## Navigation state

Theme changes and ordinary visual updates should preserve:

- selected item;
- keyboard focus;
- TreeView scroll position;
- expanded/collapsed hierarchy where the control already owns it;
- ListView selection where the feature owns it;
- useful form input state.

Do not reset navigation state simply to force a repaint.

## Error handling

UI code should not swallow failures.

Map feature/application errors at an owning boundary. Reusable UI controls should expose their documented failure events/results rather than inventing feature-specific policies.

For generic CRUD:

- HiveCrudPage exposes OperationFailed;
- operation failures are not silently ignored;
- the feature can map the operation and exception to its application-level error policy.

For Example Host execution:

- HiveExampleTestSurface reports cancellation/failure in its standard interactive surface;
- technical exception details can be sent to the shared output;
- the user is shown a Hive error dialog for failed example execution.

## Manual UI verification

User-facing UI changes require actual developer/manual verification when the active slice requires it.

Verify the applicable states rather than only checking that a window opens:

- Light, Dark, and System themes where supported;
- selected/focused/hovered/pressed/disabled states;
- compact and normal window sizes;
- keyboard activation and tab order;
- loading/empty/no-match/failure states;
- dialog sizing and details behavior;
- CRUD confirmation and operation failure behavior;
- navigation and scroll state preservation;
- dynamic view replacement/disposal;
- resource or handle behavior when theme changes repeatedly.

Static source inspection is not visual verification.

Do not record a manual-verification result until the developer has actually run the application and observed the target behavior.
