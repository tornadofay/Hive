using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleHostForm : HiveForm
{
    private const int NavigationWidth = 236;
    private const int CompactNavigationWidth = 214;
    private const int OutputExpandedHeight = 232;
    private const int OutputOverlayMargin = 0;
    private const int OutputButtonWidth = 118;
    private const int OutputButtonHeight = 36;
    private const int OutputButtonMargin = 12;

    private readonly IHiveThemeManager _themeManager;
    private readonly HiveExampleServices _services;
    private readonly IReadOnlyList<IHiveExample> _examples;
    private readonly Panel _navigationSurface;
    private readonly Panel _navigationSeparator;
    private readonly HiveNavigationTree _navigation;
    private readonly Label _navigationTitle;
    private readonly Label _navigationDescription;
    private readonly Label _viewTitle;
    private readonly Label _viewSubtitle;
    private readonly Panel _viewHost;
    private readonly TableLayoutPanel _contentLayout;
    private readonly TableLayoutPanel _shell;
    private readonly HiveExampleOutputView _outputView;
    private readonly HiveButton _outputRevealButton;
    private readonly Font _navigationTitleFont;
    private readonly Font _navigationDescriptionFont;
    private readonly Font _viewTitleFont;
    private readonly Font _viewSubtitleFont;

    private UserControl? _activeView;
    private bool _responsiveLayoutReady;

    public HiveExampleHostForm()
        : base(
            "Hive Example",
            "Developer-facing Example Host",
            new Size(1180, 760),
            new Size(960, 620))
    {
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: true,
            allowHelp: false);

        _themeManager = ThemeManager;
        _outputView = new HiveExampleOutputView();
        _outputRevealButton = new HiveButton
        {
            Text = "Show Output",
            Style = HiveButtonStyle.Secondary,
            Size = new Size(OutputButtonWidth, OutputButtonHeight),
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            TabIndex = 0
        };
        _outputRevealButton.Click += (_, _) =>
            _outputView.SetCollapsed(false);

        _outputView.CollapseStateChanged += OutputViewOnCollapseStateChanged;
        _outputView.OutputAvailabilityChanged += OutputViewOnOutputAvailabilityChanged;

        _services = new HiveExampleServices(
            _themeManager,
            _outputView);
        _examples = HiveExampleDiscovery.Discover(
            Assembly.GetExecutingAssembly());

        if (_examples.Count == 0)
            throw new InvalidOperationException(
                "No IHiveExample implementations were discovered in the Example assembly.");

        _navigationTitleFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _navigationDescriptionFont = new Font("Segoe UI", 8.4f);
        _viewTitleFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
        _viewSubtitleFont = new Font("Segoe UI", 8.9f);

        _shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, NavigationWidth));
        _shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 1));
        _shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        _navigationSurface = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 16, 12, 12),
            AccessibleName = "Example navigation panel"
        };

        var navigationLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        navigationLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 22));
        navigationLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 20));
        navigationLayout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        _navigationTitle = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = _navigationTitleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Examples",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _navigationDescription = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = _navigationDescriptionFont,
            Margin = new Padding(0, 0, 0, 6),
            Padding = Padding.Empty,
            Text = $"{_examples.Count:N0} example{(_examples.Count == 1 ? string.Empty : "s")} available",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _navigation = new HiveNavigationTree
        {
            Dock = DockStyle.Fill
        };
        _navigation.AfterSelect += NavigationAfterSelect;

        navigationLayout.Controls.Add(_navigationTitle, 0, 0);
        navigationLayout.Controls.Add(_navigationDescription, 0, 1);
        navigationLayout.Controls.Add(_navigation, 0, 2);
        _navigationSurface.Controls.Add(navigationLayout);

        _navigationSeparator = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(24, 18, 24, 20)
        };
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _viewTitle = new Label
        {
            AutoSize = true,
            Font = _viewTitleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Example"
        };

        _viewSubtitle = new Label
        {
            AutoSize = true,
            Font = _viewSubtitleFont,
            Margin = new Padding(0, 5, 0, 16),
            Padding = Padding.Empty
        };

        _viewHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty,
            AccessibleName = "Example content"
        };
        _viewHost.Resize += (_, _) => UpdateOutputOverlayBounds();

        _contentLayout.Controls.Add(_viewTitle, 0, 0);
        _contentLayout.Controls.Add(_viewSubtitle, 0, 1);
        _contentLayout.Controls.Add(_viewHost, 0, 2);

        _shell.Controls.Add(_navigationSurface, 0, 0);
        _shell.Controls.Add(_navigationSeparator, 1, 0);
        _shell.Controls.Add(_contentLayout, 2, 0);

        BodyPanel.Padding = Padding.Empty;
        BodyPanel.Controls.Add(_shell);

        _viewHost.Controls.Add(_outputRevealButton);
        _viewHost.Controls.Add(_outputView);
        _outputView.BringToFront();
        _outputRevealButton.BringToFront();

        BuildNavigation();
        _themeManager.Apply(BodyPanel);
        OnThemeChanged(_themeManager.Theme);
        _responsiveLayoutReady = true;
        UpdateResponsiveLayout();
        UpdateOutputOverlayBounds();
        OutputViewOnCollapseStateChanged(_outputView, EventArgs.Empty);
        SelectFirstExample();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateResponsiveLayout();
        UpdateOutputOverlayBounds();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _navigation.AfterSelect -= NavigationAfterSelect;
            _outputView.CollapseStateChanged -= OutputViewOnCollapseStateChanged;
            _outputView.OutputAvailabilityChanged -= OutputViewOnOutputAvailabilityChanged;
            _outputRevealButton.Dispose();
            DisposeActiveView();
        }

        base.Dispose(disposing);

        if (disposing)
        {
            _navigationTitleFont.Dispose();
            _navigationDescriptionFont.Dispose();
            _viewTitleFont.Dispose();
            _viewSubtitleFont.Dispose();
        }
    }

    protected override void OnThemeChanged(HiveThemeDefinition theme)
    {
        _navigationSurface.BackColor = theme.VisualStates.NavigationBackground;
        _navigationSeparator.BackColor = theme.VisualStates.NavigationBorder;
        _navigationTitle.ForeColor = theme.VisualStates.NavigationText;
        _navigationDescription.ForeColor = theme.Palette.MutedText;
        _viewTitle.ForeColor = theme.Palette.Text;
        _viewSubtitle.ForeColor = theme.Palette.MutedText;
        _viewHost.BackColor = theme.Palette.Surface;
    }

    private void UpdateResponsiveLayout()
    {
        if (!_responsiveLayoutReady ||
            ClientSize.Width <= 0 ||
            _shell.ColumnStyles.Count == 0)
            return;

        var width = ClientSize.Width < 1080
            ? CompactNavigationWidth
            : NavigationWidth;

        if (Math.Abs(_shell.ColumnStyles[0].Width - width) < 0.1f)
            return;

        _shell.ColumnStyles[0].Width = width;
    }

    private void UpdateOutputOverlayBounds()
    {
        // WinForms may raise OnResize while the base HiveForm constructor is
        // still constructing this derived form. Do not access overlay controls
        // until the Example Host layout has been initialized.
        if (!_responsiveLayoutReady ||
            _viewHost.ClientSize.Width <= 0 ||
            _viewHost.ClientSize.Height <= 0)
            return;

        var availableHeight =
            Math.Max(
                0,
                _viewHost.ClientSize.Height -
                (OutputOverlayMargin * 2));

        var outputHeight = Math.Min(
            OutputExpandedHeight,
            Math.Max(
                120,
                (int)Math.Round(availableHeight * 0.46)));

        var overlayWidth = Math.Max(
            0,
            _viewHost.ClientSize.Width -
            (OutputOverlayMargin * 2));

        var overlayHeight = Math.Max(0, outputHeight);

        _outputView.Bounds = new Rectangle(
            OutputOverlayMargin,
            Math.Max(
                OutputOverlayMargin,
                _viewHost.ClientSize.Height -
                overlayHeight -
                OutputOverlayMargin),
            overlayWidth,
            overlayHeight);

        _outputRevealButton.Bounds = new Rectangle(
            Math.Max(
                OutputOverlayMargin,
                _viewHost.ClientSize.Width -
                OutputButtonWidth -
                OutputButtonMargin),
            Math.Max(
                OutputOverlayMargin,
                _viewHost.ClientSize.Height -
                OutputButtonHeight -
                OutputButtonMargin),
            OutputButtonWidth,
            OutputButtonHeight);
    }

    private void BuildNavigation()
    {
        TreeNode? currentCategory = null;
        TreeNode? currentSubcategory = null;

        foreach (var example in _examples)
        {
            if (currentCategory is null ||
                !string.Equals(
                    currentCategory.Text,
                    example.Category,
                    StringComparison.OrdinalIgnoreCase))
            {
                currentCategory = new TreeNode(example.Category);
                _navigation.Nodes.Add(currentCategory);
                currentSubcategory = null;
            }

            if (currentSubcategory is null ||
                !string.Equals(
                    currentSubcategory.Text,
                    example.Subcategory,
                    StringComparison.OrdinalIgnoreCase))
            {
                currentSubcategory = new TreeNode(example.Subcategory);
                currentCategory.Nodes.Add(currentSubcategory);
            }

            currentSubcategory.Nodes.Add(
                new TreeNode(example.Title)
                {
                    Tag = example
                });
        }

        _navigation.CollapseAll();
    }

    private void SelectFirstExample()
    {
        foreach (TreeNode category in _navigation.Nodes)
        {
            var firstExample = FindFirstExampleNode(category);
            if (firstExample?.Tag is not IHiveExample)
                continue;

            _navigation.CollapseAll();
            ExpandNavigationParents(firstExample);
            _navigation.SelectedNode = firstExample;
            firstExample.EnsureVisible();
            return;
        }
    }

    private static void ExpandNavigationParents(TreeNode node)
    {
        var parent = node.Parent;
        while (parent is not null)
        {
            parent.Expand();
            parent = parent.Parent;
        }
    }

    private static TreeNode? FindFirstExampleNode(TreeNode parent)
    {
        foreach (TreeNode child in parent.Nodes)
        {
            if (child.Tag is IHiveExample)
                return child;

            var nested = FindFirstExampleNode(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void NavigationAfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is IHiveExample example)
            ShowExample(example);
    }

    private void ShowExample(IHiveExample example)
    {
        var nextView = example.CreateView(_services);
        ArgumentNullException.ThrowIfNull(nextView);

        nextView.Dock = DockStyle.Fill;

        try
        {
            var previousView = _activeView;

            // Theme the new view before it enters the visible host surface so
            // switching examples never exposes an unthemed frame.
            _themeManager.Apply(nextView);

            _viewHost.SuspendLayout();
            try
            {
                _viewHost.Controls.Add(nextView);
                _activeView = nextView;

                if (previousView is not null)
                {
                    _viewHost.Controls.Remove(previousView);
                    previousView.Dispose();
                }
            }
            finally
            {
                _viewHost.ResumeLayout(true);
            }

            _viewTitle.Text = example.Title;
            _viewSubtitle.Text =
                $"{example.Category} / {example.Subcategory}";

            _outputView.Clear();
            _outputView.SetCollapsed(true);
        }
        catch
        {
            if (_viewHost.Controls.Contains(nextView))
                _viewHost.Controls.Remove(nextView);

            nextView.Dispose();
            throw;
        }

        _outputView.BringToFront();
        _outputRevealButton.BringToFront();
        UpdateOutputOverlayBounds();
    }

    private void OutputViewOnCollapseStateChanged(object? sender, EventArgs e)
    {
        _outputRevealButton.Visible =
            _outputView.IsCollapsed &&
            _outputView.OutputTextBox.TextLength > 0;

        if (!_outputView.IsCollapsed)
            _outputView.BringToFront();
        else if (_outputRevealButton.Visible)
            _outputRevealButton.BringToFront();
    }

    private void OutputViewOnOutputAvailabilityChanged(object? sender, EventArgs e)
    {
        if (_outputView.OutputTextBox.TextLength == 0)
            return;

        _outputView.SetCollapsed(false);
        _outputView.BringToFront();
    }

    private void DisposeActiveView()
    {
        var activeView = _activeView;
        _activeView = null;

        if (activeView is null)
            return;

        _viewHost.Controls.Remove(activeView);
        activeView.Dispose();
    }
}
