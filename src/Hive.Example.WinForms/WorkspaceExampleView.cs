using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Example.WinForms;

/// <summary>
/// Explains the Workspace and opens the real Workspace window, the same way Example Configuration
/// opens Hive Settings. The page does not own or duplicate Workspace state.
/// </summary>
internal sealed class WorkspaceExampleView : UserControl
{
    private const string SampleImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private readonly HiveExampleServices _services;
    private readonly IHiveThemeManager _themeManager;
    private readonly Font _titleFont;
    private readonly Font _sectionFont;
    private readonly HiveButton _openButton;
    private readonly HiveButton _sampleButton;

    public WorkspaceExampleView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services.GetHiveExampleServices();
        _themeManager = _services.GetThemeManager();

        Dock = DockStyle.Fill;
        Padding = new Padding(2, 2, 2, 18);

        var family = _themeManager.Theme.Typography.FontFamily;
        _titleFont = new Font(family, 19f, FontStyle.Bold);
        _sectionFont = new Font(family, 10.5f, FontStyle.Bold);

        _openButton = new HiveButton
        {
            Text = "Open Hive Workspace",
            Style = HiveButtonStyle.Primary,
            Width = 190,
            Height = 38,
            Margin = new Padding(0, 0, 12, 0)
        };
        _openButton.Click += (_, _) => OpenWorkspace();

        _sampleButton = new HiveButton
        {
            Text = "Create sample WorkItem",
            Style = HiveButtonStyle.Secondary,
            Width = 200,
            Height = 38,
            Margin = Padding.Empty
        };
        _sampleButton.Click += async (_, _) => await CreateSampleAsync();

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 16, 0, 16),
            Padding = Padding.Empty
        };
        actions.Controls.Add(_openButton);
        actions.Controls.Add(_sampleButton);

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Font = _titleFont,
            Margin = new Padding(0, 0, 0, 8),
            Text = "Workspace",
            AccessibleRole = AccessibleRole.StaticText
        });
        root.Controls.Add(CreateBody(
            "The Hive Workspace is the human-facing operational surface over Hive.Management. " +
            "It opens in its own window, like Hive Settings, so it can stay open next to your application. " +
            "This page does not own or duplicate Workspace state."));
        root.Controls.Add(actions);

        root.Controls.Add(CreateSectionHeader("Direct LLM"));
        root.Controls.Add(CreateBody(
            "Chat with an exact ExecutionTarget. When you have saved favorites (Settings, Providers, " +
            "Favorite Execution Targets) the picker lists only those favorites; with no favorites it lists " +
            "every active, eligible target. Conversations and request outcomes are saved and reload after a restart."));

        root.Controls.Add(CreateSectionHeader("Work Items"));
        root.Controls.Add(CreateBody(
            "Create image-backed WorkItems, inspect their activity, request approval, and approve or reject them."));

        root.Controls.Add(CreateSectionHeader("Grows with Hive"));
        root.Controls.Add(CreateBody(
            "Later phases add modules to the same window without changing the shell, for example Agent " +
            "interaction, diagnostics, and Hive coordination."));

        Controls.Add(root);
        _themeManager.Apply(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _sectionFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OpenWorkspace()
    {
        if (FindForm() is HiveExampleHostForm host)
        {
            host.OpenHiveWorkspace();
            return;
        }

        HiveUiErrorReporter.Report(
            FindForm(),
            "The Hive Workspace can only be opened from the Hive Example Host.",
            "Hive Workspace",
            null,
            _themeManager);
    }

    private async Task CreateSampleAsync()
    {
        try
        {
            var result = await _services.GetManagementFacade().CreateImageWorkItemAsync(
                new WorkItemImageSubmission(
                    "sample-invoice.png",
                    "image/png",
                    Convert.FromBase64String(SampleImageBase64)),
                _services.AccessContext);

            if (result.IsFailure)
            {
                HiveMessageBox.ShowError(
                    FindForm(),
                    result.Error!.Message,
                    "WorkItem creation failed",
                    _themeManager);
                return;
            }

            (FindForm() as HiveExampleHostForm)?.NotifyWorkspaceDataChanged();

            HiveMessageBox.ShowSuccess(
                FindForm(),
                "Sample WorkItem created. Open the Workspace and choose Work Items to see it.",
                "Sample WorkItem",
                _themeManager);
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Workspace example failed",
                "The sample WorkItem could not be created.",
                null,
                _themeManager);
        }
    }

    private Label CreateSectionHeader(string text) =>
        new()
        {
            AutoSize = true,
            Font = _sectionFont,
            Margin = new Padding(0, 14, 0, 2),
            Text = text,
            AccessibleRole = AccessibleRole.StaticText
        };

    private static Label CreateBody(string text) =>
        new()
        {
            AutoSize = true,
            MaximumSize = new Size(880, 0),
            Margin = new Padding(0, 2, 0, 0),
            Text = text
        };
}
