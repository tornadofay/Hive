using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly ListBox _navigation;
    private readonly Panel _content;
    private readonly Label _title;
    private readonly Label _description;

    private HiveProviderSettingsView? _providerView;
    private HivePersistenceSettingsView? _persistenceView;
    private CancellationTokenSource? _initializationCts;

    public HiveSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);

        _title = new Label
        {
            Text = "Settings",
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _description = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "Hive platform configuration. Settings pages use Hive.Management; database, secret, and provider implementation details remain outside the UI.",
            Margin = new Padding(0, 4, 0, 12),
            Padding = Padding.Empty,
            AutoEllipsis = true
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _navigation = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 38,
            Margin = new Padding(0, 0, 12, 0),
            AccessibleName = "Settings navigation"
        };
        _navigation.Items.Add(new SettingsPage(
            "Provider",
            "Providers, accounts, targets and connection tests."));
        _navigation.Items.Add(new SettingsPage(
            "Persistence",
            "SQL Server / LocalDB settings and database/schema status."));
        _navigation.DrawItem += DrawNavigationItem;
        _navigation.SelectedIndexChanged += (_, _) => ShowSelectedPage();

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 0, 0),
            AccessibleName = "Settings content"
        };

        body.Controls.Add(_navigation, 0, 0);
        body.Controls.Add(_content, 1, 0);

        Controls.Add(body);
        Controls.Add(_description);
        Controls.Add(_title);

        _themeManager.Apply(this);
        _navigation.SelectedIndex = 0;
        Load += async (_, _) => await InitializeAsync();
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        _initializationCts?.Cancel();
        _initializationCts?.Dispose();
        _initializationCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        try
        {
            if (_providerView is null)
            {
                _providerView = new HiveProviderSettingsView(
                    _management,
                    _accessContext,
                    _themeManager);
                _themeManager.Apply(_providerView);
            }

            if (_persistenceView is null)
            {
                _persistenceView = new HivePersistenceSettingsView(
                    _management,
                    _accessContext,
                    _themeManager);
                _themeManager.Apply(_persistenceView);
            }

            await _persistenceViewInitializeAsync(
                _initializationCts.Token).ConfigureAwait(true);

            if (_providerView is not null)
                await _providerView.InitializeAsync(
                    _initializationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_initializationCts.IsCancellationRequested)
        {
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _initializationCts?.Cancel();
            _initializationCts?.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task _persistenceViewInitializeAsync(
        CancellationToken cancellationToken)
    {
        if (_persistenceView is not null)
        {
            // Loading settings never creates a database, applies migrations,
            // or resolves secret material.
            await _persistenceView.InitializeAsync(cancellationToken)
                .ConfigureAwait(true);
        }
    }

    private void ShowSelectedPage()
    {
        var page = _navigation.SelectedItem as SettingsPage;
        if (page is null)
            return;

        Control control = page.Name switch
        {
            "Provider" => _providerView ??=
                new HiveProviderSettingsView(
                    _management,
                    _accessContext,
                    _themeManager),

            "Persistence" => _persistenceView ??=
                new HivePersistenceSettingsView(
                    _management,
                    _accessContext,
                    _themeManager),

            _ => throw new InvalidOperationException(
                $"Unknown Settings page '{page.Name}'.")
        };

        _content.SuspendLayout();
        try
        {
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
        }
        finally
        {
            _content.ResumeLayout(true);
        }

        _themeManager.Apply(control);
    }

    private void DrawNavigationItem(
        object? sender,
        DrawItemEventArgs e)
    {
        if (e.Index < 0 ||
            e.Index >= _navigation.Items.Count)
            return;

        var theme = _themeManager.Theme;
        var selected = (e.State & DrawItemState.Selected) != 0;

        using var background = new SolidBrush(
            selected
                ? theme.VisualStates.NavigationSelected
                : theme.VisualStates.NavigationBackground);

        using var foreground = new SolidBrush(
            selected
                ? theme.VisualStates.NavigationSelectedText
                : theme.VisualStates.NavigationText);

        e.Graphics.FillRectangle(background, e.Bounds);

        var page = (SettingsPage)_navigation.Items[e.Index];

        using var titleFont = new Font(
            Font,
            selected ? FontStyle.Bold : FontStyle.Regular);

        e.Graphics.DrawString(
            page.Name,
            titleFont,
            foreground,
            e.Bounds.Left + 12,
            e.Bounds.Top + 6);

        using var descriptionBrush = new SolidBrush(theme.Palette.MutedText);

        using var descriptionFont = new Font(
            Font.FontFamily,
            8f);

        e.Graphics.DrawString(
            page.Description,
            descriptionFont,
            descriptionBrush,
            e.Bounds.Left + 12,
            e.Bounds.Top + 22);

        e.DrawFocusRectangle();
    }

    private sealed record SettingsPage(
        string Name,
        string Description);
}
