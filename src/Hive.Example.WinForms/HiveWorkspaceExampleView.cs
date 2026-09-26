using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Management;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class HiveWorkspaceExampleView : UserControl
{
    private readonly IServiceProvider _services;
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _context;
    private readonly HiveWorkspaceView _workspace;
    private readonly HiveButton _sampleButton;
    private readonly HiveButton _captureOutputButton;
    private readonly IHiveExampleOutput _output;

    public HiveWorkspaceExampleView(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _management = services.GetManagementFacade();
        _output = services.GetExampleOutput();

        _context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        _sampleButton = new HiveButton
        {
            Text = "Create sample WorkItem",
            Style = HiveButtonStyle.Primary,
            Dock = DockStyle.Right,
            Width = 190
        };
        _sampleButton.Click += async (_, _) => await CreateSampleAsync();

        _captureOutputButton = new HiveButton
        {
            Text = "Capture output",
            Style = HiveButtonStyle.Secondary,
            Width = 130
        };
        _captureOutputButton.Click += async (_, _) => await CaptureOutputAsync();

        _workspace = new HiveWorkspaceView(
            _management,
            _context,
            services.GetThemeManager());

        var description = new Label
        {
            Dock = DockStyle.Fill,
            Text = "V1 Workspace over Hive.Management. Create an image-backed WorkItem, request approval, then Approve or Reject it. The execution/provider panel remains intentionally inactive until later V1 pipeline slices.",
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 12, 0)
        };

        var bannerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = false,
            Width = 330,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        bannerActions.Controls.Add(_sampleButton);
        bannerActions.Controls.Add(_captureOutputButton);

        var banner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(0, 0, 0, 10)
        };
        banner.Controls.Add(description);
        banner.Controls.Add(bannerActions);

        var content = new Panel
        {
            Dock = DockStyle.Fill
        };
        content.Controls.Add(_workspace);

        Controls.Add(content);
        Controls.Add(banner);

        services.GetThemeManager().Apply(this);
    }

    private async Task CaptureOutputAsync()
    {
        var result = await _management.ListWorkItemsAsync(_context);

        if (result.IsFailure)
        {
            _output.Write("Workspace output", $"ERROR: {result.Error!.Message}");
            return;
        }

        var workItems = result.Value!
            .OrderByDescending(item => item.Resource.Provenance.CreatedAtUtc)
            .ToArray();

        _output.Clear();
        _output.Write(
            "Workspace / WorkItem Operations",
            $"Visible WorkItems: {workItems.Length}{Environment.NewLine}" +
            $"Captured at UTC: {DateTimeOffset.UtcNow:O}");

        foreach (var workItem in workItems)
        {
            var attachment = workItem.Attachment is null
                ? "None"
                : $"{workItem.Attachment.FileName} | {workItem.Attachment.MediaType} | " +
                  $"{workItem.Attachment.ContentLength} bytes | SHA-256 {workItem.Attachment.Sha256}";

            _output.Append(
                Environment.NewLine + Environment.NewLine +
                $"WorkItem: {workItem.Id}{Environment.NewLine}" +
                $"Status: {workItem.Status}{Environment.NewLine}" +
                $"Version: {workItem.Resource.Version}{Environment.NewLine}" +
                $"Attachment: {attachment}");

            var activity = await _management.GetWorkItemActivityAsync(
                workItem.Id,
                _context);

            if (activity.IsFailure)
            {
                _output.Append(
                    Environment.NewLine +
                    $"Activity: ERROR: {activity.Error!.Message}");
                continue;
            }

            _output.Append(
                Environment.NewLine +
                "Activity:");

            foreach (var entry in activity.Value!)
            {
                _output.Append(
                    Environment.NewLine +
                    $"  {entry.OccurredAtUtc:O} | v{entry.Version} | " +
                    $"{entry.EventType}" +
                    (entry.Status is null ? string.Empty : $" | {entry.Status}") +
                    $" | {entry.Message}");
            }
        }
    }

    private async Task CreateSampleAsync()
    {
        try
        {
            var options = HiveDatabaseOptions.LocalDevelopment();
            var migration = await new HiveDatabaseMigrator(options)
                .MigrateAsync();

            if (migration.IsFailure)
            {
                HiveMessageBox.ShowError(
                    FindForm(),
                    migration.Error!.Message,
                    "Workspace setup failed",
                    _services.GetThemeManager());
                return;
            }

            var result = await _management.CreateImageWorkItemAsync(
                new WorkItemImageSubmission(
                    "sample-invoice.png",
                    "image/png",
                    Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")),
                _context);

            if (result.IsFailure)
            {
                HiveMessageBox.ShowError(
                    FindForm(),
                    result.Error!.Message,
                    "WorkItem creation failed",
                    _services.GetThemeManager());
                return;
            }

            await _workspace.RefreshAsync();
            await CaptureOutputAsync();
        }
        catch (Exception exception)
        {
            HiveMessageBox.ShowError(
                FindForm(),
                exception.Message,
                "Workspace example failed",
                _services.GetThemeManager());
        }
    }
}
