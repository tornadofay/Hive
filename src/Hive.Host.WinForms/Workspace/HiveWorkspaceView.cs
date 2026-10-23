using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

/// <summary>
/// The embeddable Workspace shell: a navigation list plus a content host for Workspace modules.
/// It contains no feature logic. Module views are created lazily and loaded on first activation.
/// </summary>
public sealed class HiveWorkspaceView : UserControl
{
    private readonly HiveWorkspaceContext _context;
    private readonly IReadOnlyList<IHiveWorkspaceModule> _modules;
    private readonly ListBox _navigation;
    private readonly Panel _content;
    private readonly Font _headingFont;
    private readonly Dictionary<string, Control> _views = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private IHiveWorkspaceModule? _activeModule;
    private bool _suppressNavigation;
    private bool _disposed;

    public HiveWorkspaceView(HiveWorkspaceContext context)
        : this(context, CreateDefaultModules())
    {
    }

    internal HiveWorkspaceView(
        HiveWorkspaceContext context,
        IEnumerable<IHiveWorkspaceModule> modules)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(modules);
        _modules = ValidateModules(modules);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(12);
        AccessibleName = "Hive Workspace";

        _headingFont = new Font(Font, FontStyle.Bold);

        _navigation = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            HorizontalScrollbar = false,
            AccessibleName = "Workspace modules",
            AccessibleRole = AccessibleRole.List
        };

        foreach (var module in _modules)
            _navigation.Items.Add(new ModuleItem(module));

        _navigation.SelectedIndexChanged += NavigationSelectedIndexChanged;

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = _headingFont,
            Text = "Workspace",
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleRole = AccessibleRole.StaticText
        };

        var navigationPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 190,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 10, 0)
        };
        navigationPanel.Controls.Add(_navigation);
        navigationPanel.Controls.Add(heading);

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        Controls.Add(_content);
        Controls.Add(navigationPanel);

        _context.ManagementChanged += ContextOnManagementChanged;
        _context.ThemeManager.Apply(this);
    }

    internal IReadOnlyList<IHiveWorkspaceModule> Modules => _modules;

    internal string? ActiveModuleId => _activeModule?.Id;

    /// <summary>Refreshes the module that is currently shown.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _activeModule is null)
            return;

        if (_views.TryGetValue(_activeModule.Id, out var view) &&
            view is IHiveWorkspaceRefreshable refreshable)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCts.Token,
                cancellationToken);

            await refreshable.RefreshAsync(linked.Token).ConfigureAwait(true);
        }
    }

    /// <summary>Shows a module without loading data. Used for structure and tests.</summary>
    internal void ShowModule(string moduleId) =>
        ShowModuleCore(FindModule(moduleId));

    /// <summary>Shows a module and loads its data the first time it is shown.</summary>
    internal async Task ActivateModuleAsync(
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        var module = FindModule(moduleId);
        var created = ShowModuleCore(module);

        if (created &&
            _views.TryGetValue(module.Id, out var view) &&
            view is IHiveWorkspaceRefreshable refreshable)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCts.Token,
                cancellationToken);

            await refreshable.RefreshAsync(linked.Token).ConfigureAwait(true);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_activeModule is null && _modules.Count > 0)
            _ = ActivateModuleSafeAsync(_modules[0].Id);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _context.ManagementChanged -= ContextOnManagementChanged;
            _navigation.SelectedIndexChanged -= NavigationSelectedIndexChanged;
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
        }

        base.Dispose(disposing);

        if (disposing)
            _headingFont.Dispose();
    }

    private bool ShowModuleCore(IHiveWorkspaceModule module)
    {
        var created = false;

        if (!_views.TryGetValue(module.Id, out var view))
        {
            view = module.CreateView(_context);
            view.Dock = DockStyle.Fill;
            _views.Add(module.Id, view);
            _content.Controls.Add(view);
            created = true;
        }

        foreach (var pair in _views)
        {
            pair.Value.Visible = string.Equals(
                pair.Key,
                module.Id,
                StringComparison.Ordinal);
        }

        view.BringToFront();
        _activeModule = module;
        SelectNavigation(module);

        return created;
    }

    private void SelectNavigation(IHiveWorkspaceModule module)
    {
        var index = -1;
        for (var i = 0; i < _modules.Count; i++)
        {
            if (ReferenceEquals(_modules[i], module))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || _navigation.SelectedIndex == index)
            return;

        _suppressNavigation = true;
        try
        {
            _navigation.SelectedIndex = index;
        }
        finally
        {
            _suppressNavigation = false;
        }
    }

    private IHiveWorkspaceModule FindModule(string moduleId)
    {
        foreach (var module in _modules)
        {
            if (string.Equals(module.Id, moduleId, StringComparison.Ordinal))
                return module;
        }

        throw new ArgumentException(
            $"The Workspace has no module with id '{moduleId}'.",
            nameof(moduleId));
    }

    private async void NavigationSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressNavigation ||
            _disposed ||
            _navigation.SelectedItem is not ModuleItem item)
        {
            return;
        }

        await ActivateModuleSafeAsync(item.Module.Id).ConfigureAwait(true);
    }

    private async Task ActivateModuleSafeAsync(string moduleId)
    {
        try
        {
            await ActivateModuleAsync(moduleId).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "The Workspace module could not be opened.");
        }
    }

    private void ContextOnManagementChanged(object? sender, EventArgs e)
    {
        if (_disposed || IsDisposed || Disposing)
            return;

        if (InvokeRequired)
        {
            if (IsHandleCreated)
                BeginInvoke(new Action(() => ContextOnManagementChanged(sender, e)));

            return;
        }

        _ = RefreshAfterManagementChangeAsync();
    }

    private async Task RefreshAfterManagementChangeAsync()
    {
        try
        {
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "The Workspace could not reload after the Hive configuration changed.");
        }
    }

    private void ReportFailure(Exception exception, string message)
    {
        if (_disposed || IsDisposed || Disposing)
            return;

        HiveUiErrorReporter.Report(
            FindForm(),
            exception,
            "Workspace",
            message,
            _context.Output,
            _context.ThemeManager);
    }

    private static IEnumerable<IHiveWorkspaceModule> CreateDefaultModules() =>
        new IHiveWorkspaceModule[]
        {
            new DirectLlmWorkspaceModule(),
            new WorkItemsWorkspaceModule()
        };

    private static IReadOnlyList<IHiveWorkspaceModule> ValidateModules(
        IEnumerable<IHiveWorkspaceModule> modules)
    {
        var supplied = modules.ToList();

        if (supplied.Count == 0)
        {
            throw new ArgumentException(
                "At least one Workspace module is required.",
                nameof(modules));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in supplied)
        {
            if (module is null)
            {
                throw new ArgumentException(
                    "Workspace modules cannot contain null values.",
                    nameof(modules));
            }

            if (string.IsNullOrWhiteSpace(module.Id))
            {
                throw new ArgumentException(
                    "Every Workspace module needs a non-empty id.",
                    nameof(modules));
            }

            if (!ids.Add(module.Id))
            {
                throw new ArgumentException(
                    $"Duplicate Workspace module id '{module.Id}'.",
                    nameof(modules));
            }
        }

        return supplied
            .OrderBy(static module => module.Order)
            .ThenBy(static module => module.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record ModuleItem(IHiveWorkspaceModule Module)
    {
        public override string ToString() => Module.Title;
    }
}
