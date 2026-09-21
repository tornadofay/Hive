using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleHostForm : HiveForm
{
    private const int NavigationWidth = 250;

    private readonly IHiveThemeManager _themeManager;
    private readonly HiveExampleServices _services;
    private readonly IReadOnlyList<IHiveExample> _examples;
    private readonly TreeView _navigation;
    private readonly Label _viewTitle;
    private readonly Label _viewSubtitle;
    private readonly Panel _viewHost;
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
        _themeManager = ThemeManager;
        _services = new HiveExampleServices(_themeManager);
        _examples = HiveExampleDiscovery.Discover(
            Assembly.GetExecutingAssembly());

        if (_examples.Count == 0)
            throw new InvalidOperationException(
                "No IHiveExample implementations were discovered in the Example assembly.");

        _viewTitleFont = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
        _viewSubtitleFont = new Font("Segoe UI", 8.8f);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, NavigationWidth));
        shell.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        _navigation = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            FullRowSelect = true,
            HideSelection = false,
            HotTracking = false,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = false,
            Indent = 18,
            ItemHeight = 26,
            Margin = Padding.Empty,
            Padding = new Padding(12, 12, 8, 12)
        };
        _navigation.AfterSelect += NavigationAfterSelect;

        var navigationHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 1, 0)
        };
        navigationHost.Controls.Add(_navigation);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(28, 22, 28, 24)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

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
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        content.Controls.Add(_viewTitle, 0, 0);
        content.Controls.Add(_viewSubtitle, 0, 1);
        content.Controls.Add(_viewHost, 0, 2);

        shell.Controls.Add(navigationHost, 0, 0);
        shell.Controls.Add(content, 1, 0);

        BodyPanel.Padding = Padding.Empty;
        BodyPanel.Controls.Add(shell);

        BuildNavigation();
        ApplyShellTheme(_themeManager.Theme);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;

        SelectFirstExample();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            DisposeActiveView();
            _viewTitleFont.Dispose();
            _viewSubtitleFont.Dispose();
        }

        base.Dispose(disposing);
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

            var exampleNode = new TreeNode(example.Title)
            {
                Tag = example
            };
            currentSubcategory.Nodes.Add(exampleNode);
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
        if (e.Node.Tag is IHiveExample example)
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
            _activeView = nextView;
            _viewHost.Controls.Add(nextView);

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

    private void DisposeActiveView()
    {
        var activeView = _activeView;
        _activeView = null;

        if (activeView is null)
            return;

        _viewHost.Controls.Clear();
        activeView.Dispose();
    }

    private void ApplyShellTheme(HiveThemeDefinition theme)
    {
        _navigation.BackColor = theme.VisualStates.NavigationBackground;
        _navigation.ForeColor = theme.VisualStates.NavigationText;
        _navigation.LineColor = theme.VisualStates.NavigationBorder;

        _viewTitle.ForeColor = theme.Palette.Text;
        _viewSubtitle.ForeColor = theme.Palette.MutedText;
        _viewHost.BackColor = theme.Palette.Surface;
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyShellTheme(_themeManager.Theme);
        if (_activeView is not null)
            _themeManager.Apply(_activeView);
    }
}
