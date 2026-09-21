using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleHostForm : HiveForm
{
    private const int NavigationWidth = 246;
    private const int OutputExpandedHeight = 190;
    private const int OutputCollapsedHeight = 44;

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
    private readonly HiveExampleOutputView _outputView;
    private readonly Font _navigationTitleFont;
    private readonly Font _navigationDescriptionFont;
    private readonly Font _viewTitleFont;
    private readonly Font _viewSubtitleFont;

    private UserControl? _activeView;

    public HiveExampleHostForm()
        : base(
            "Hive Example",
            "Developer-facing Example Host",
            new Size(1180, 760),
            new Size(960, 620))
    {
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: true,
            allowHelp: false);

        _themeManager = ThemeManager;
        _outputView = new HiveExampleOutputView();
        _outputView.CollapseStateChanged += OutputViewOnCollapseStateChanged;
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

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, NavigationWidth));
        shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 1));
        shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        _navigationSurface = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 16, 12, 12)
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
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(28, 22, 28, 24)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));

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
            Padding = Padding.Empty
        };

        _contentLayout.Controls.Add(_viewTitle, 0, 0);
        _contentLayout.Controls.Add(_viewSubtitle, 0, 1);
        _contentLayout.Controls.Add(_viewHost, 0, 2);
        _contentLayout.Controls.Add(_outputView, 0, 3);

        shell.Controls.Add(_navigationSurface, 0, 0);
        shell.Controls.Add(_navigationSeparator, 1, 0);
        shell.Controls.Add(_contentLayout, 2, 0);

        BodyPanel.Padding = Padding.Empty;
        BodyPanel.Controls.Add(shell);

        BuildNavigation();
        _themeManager.Apply(BodyPanel);
        SelectFirstExample();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeActiveView();

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

        foreach (TreeNode category in _navigation.Nodes)
        {
            category.Expand();
            foreach (TreeNode subcategory in category.Nodes)
                subcategory.Expand();
        }
    }

    private void SelectFirstExample()
    {
        foreach (TreeNode category in _navigation.Nodes)
        {
            var exampleNode = FindFirstExampleNode(category);
            if (exampleNode is null)
                continue;

            _navigation.SelectedNode = exampleNode;
            return;
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

        _viewHost.SuspendLayout();
        try
        {
            var previousView = _activeView;
            _viewHost.Controls.Clear();
            _viewHost.Controls.Add(nextView);
            _activeView = nextView;

            previousView?.Dispose();

            _viewTitle.Text = example.Title;
            _viewSubtitle.Text =
                $"{example.Category} / {example.Subcategory}";
        }
        catch
        {
            nextView.Dispose();
            throw;
        }
        finally
        {
            _viewHost.ResumeLayout(true);
        }

        _themeManager.Apply(nextView);
    }

    private void OutputViewOnCollapseStateChanged(object? sender, EventArgs e)
    {
        _contentLayout.RowStyles[3].Height =
            _outputView.IsCollapsed
                ? OutputCollapsedHeight
                : OutputExpandedHeight;

        _contentLayout.PerformLayout();
    }

    private void DisposeActiveView()
    {
        var activeView = _activeView;
        _activeView = null;

        if (activeView is null)
            return;

        _viewHost.Controls.Clear();
        activeView.Dispose();
    }
}
