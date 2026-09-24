using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public enum HiveCrudOperation
{
    Load,
    Edit,
    Delete,
    Activate
}

public sealed class HiveCrudOperationFailedEventArgs : EventArgs
{
    public HiveCrudOperationFailedEventArgs(
        HiveCrudOperation operation,
        Exception exception)
    {
        Operation = operation;
        Exception = exception;
    }

    public HiveCrudOperation Operation { get; }

    public Exception Exception { get; }
}

public sealed class HiveCrudPage<TItem> : UserControl where TItem : class
{
    private const int HeaderHeight = 64;
    private const int ActionBarHeight = 46;
    private const int FooterHeight = 42;
    private const int ActionButtonWidth = 92;
    private const int CompactActionButtonWidth = 84;
    private const int ActionButtonSpacing = 8;
    private const int InitialActionBarActionsWidth =
        (ActionButtonWidth + ActionButtonSpacing) * 4;
    private const int PaginationWidth = 276;
    private const int DefaultPageSize = 25;
    private const int MinimumSearchWidth = 180;
    private const int MaximumSearchWidth = 420;

    private readonly HiveListPageLayout _pageLayout;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _searchLabel;
    private readonly TextBox _searchBox;
    private readonly Label _statusFilterLabel;
    private readonly ComboBox _statusFilterBox;
    private readonly TableLayoutPanel _actionLayout;
    private readonly FlowLayoutPanel _searchPanel;
    private readonly FlowLayoutPanel _actionButtons;
    private readonly TableLayoutPanel _footerLayout;
    private readonly Label _statusLabel;
    private readonly HiveButton _addButton;
    private readonly HiveButton _editButton;
    private readonly HiveButton _activateButton;
    private readonly HiveButton _deleteButton;
    private readonly HiveButton _refreshButton;
    private readonly ListView _list;
    private readonly Label _emptyStateLabel;
    private readonly HivePaginationBar _pagination;
    private Font _titleFont;
    private Font _descriptionFont;
    private Font _searchLabelFont;
    private Font _emptyStateFont;
    private readonly List<HiveCrudColumn<TItem>> _columns = new();

    private IReadOnlyList<TItem> _items = Array.Empty<TItem>();
    private Func<CancellationToken, Task<IReadOnlyList<TItem>>>? _loadItemsAsync;
    private Func<TItem?, CancellationToken, Task<TItem?>>? _editItemAsync;
    private Func<TItem, CancellationToken, Task>? _deleteItemAsync;
    private Func<TItem, string>? _getItemDisplayName;
    private Func<TItem, bool>? _canEditItem;
    private Func<TItem, bool>? _canDeleteItem;
    private Func<TItem, bool>? _canActivateItem;
    private Func<TItem, CancellationToken, Task>? _activateItemAsync;
    private Func<TItem, string?>? _statusSelector;
    private bool _updatingStatusFilter;
    private CancellationTokenSource? _operationCancellation;
    private string _searchText = string.Empty;
    private const string AllStatusFilter = "All";
    private int _pageSize = DefaultPageSize;
    private bool _busy;
    private bool _compactToolbar = false;
    private bool _typographyReady;

    public HiveCrudPage()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _pageLayout = new HiveListPageLayout
        {
            Dock = DockStyle.Fill,
            HeaderHeight = HeaderHeight,
            ActionBarHeight = ActionBarHeight
        };

        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        _titleFont = new Font(fallbackFont.FontFamily, 15f, FontStyle.Bold);
        _descriptionFont = new Font(fallbackFont.FontFamily, 8.9f);
        _searchLabelFont = new Font(fallbackFont.FontFamily, 8.8f, FontStyle.Bold);
        _emptyStateFont = new Font(fallbackFont.FontFamily, 9.5f);

        _titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Items",
            TabIndex = 0
        };

        _descriptionLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = _descriptionFont,
            Margin = new Padding(0, 4, 0, 0),
            Padding = Padding.Empty
        };

        _pageLayout.HeaderPanel.Padding = new Padding(12, 4, 12, 4);
        _pageLayout.HeaderPanel.Controls.Add(_descriptionLabel);
        _pageLayout.HeaderPanel.Controls.Add(_titleLabel);

        _actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, InitialActionBarActionsWidth));

        _searchPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 12, 4)
        };

        _searchLabel = new Label
        {
            AutoSize = false,
            Font = _searchLabelFont,
            Text = "Search",
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 48,
            Height = 32,
            Margin = new Padding(0, 0, 6, 0),
            Padding = Padding.Empty
        };

        _searchBox = new TextBox
        {
            Width = 280,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 6),
            PlaceholderText = "Search...",
            TabIndex = 1,
            AccessibleName = "Search items",
            AccessibleDescription = "Filter the current list by the visible column values."
        };
        _searchBox.AccessibleRole = AccessibleRole.Text;
        _searchBox.TextChanged += SearchBoxOnTextChanged;
        _searchBox.KeyDown += SearchBoxOnKeyDown;

        _statusFilterLabel = new Label
        {
            AutoSize = false,
            Font = _searchLabelFont,
            Text = "Status",
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 48,
            Height = 32,
            Margin = new Padding(16, 0, 6, 0),
            Padding = Padding.Empty,
            Visible = false
        };

        _statusFilterBox = new ComboBox
        {
            Width = 132,
            Height = 32,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = Padding.Empty,
            Visible = false,
            AccessibleName = "Status filter",
            AccessibleDescription = "Filter the current list by resource lifecycle status."
        };
        _statusFilterBox.SelectedIndexChanged += StatusFilterBoxOnSelectedIndexChanged;

        _searchPanel.Controls.Add(_searchLabel);
        _searchPanel.Controls.Add(_searchBox);
        _searchPanel.Controls.Add(_statusFilterLabel);
        _searchPanel.Controls.Add(_statusFilterBox);

        _actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 4)
        };

        _addButton = CreateActionButton("Add", HiveButtonStyle.Primary);
        _editButton = CreateActionButton("Edit", HiveButtonStyle.Secondary);
        _activateButton = CreateActionButton("Activate", HiveButtonStyle.Secondary);
        _activateButton.Visible = false;
        _deleteButton = CreateActionButton("Delete", HiveButtonStyle.Danger);
        _refreshButton = CreateActionButton("Refresh", HiveButtonStyle.Secondary);

        _addButton.AccessibleName = "Add item";
        _addButton.AccessibleDescription = "Create a new item.";
        _editButton.AccessibleName = "Edit selected item";
        _editButton.AccessibleDescription = "Edit the currently selected item.";
        _activateButton.AccessibleName = "Activate selected item";
        _activateButton.AccessibleDescription = "Reactivate the currently selected retired item.";
        _deleteButton.AccessibleName = "Delete selected item";
        _deleteButton.AccessibleDescription = "Delete the currently selected item.";
        _refreshButton.AccessibleName = "Refresh items";
        _refreshButton.AccessibleDescription = "Reload the current item list.";

        _addButton.Click += async (_, _) => await EditAsync(null);
        _editButton.Click += async (_, _) => await EditAsync(SelectedItem);
        _activateButton.Click += async (_, _) => await ActivateAsync(SelectedItem);
        _deleteButton.Click += async (_, _) => await DeleteAsync();
        _refreshButton.Click += async (_, _) => await RefreshAsync();

        _actionButtons.Controls.Add(_addButton);
        _actionButtons.Controls.Add(_editButton);
        _actionButtons.Controls.Add(_activateButton);
        _actionButtons.Controls.Add(_deleteButton);
        _actionButtons.Controls.Add(_refreshButton);

        _actionLayout.Controls.Add(_searchPanel, 0, 0);
        _actionLayout.Controls.Add(_actionButtons, 1, 0);
        _pageLayout.ActionBarPanel.Controls.Add(_actionLayout);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        var listHost = new HiveBorderPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            CornerRadius = 7
        };

        _list = new HiveListView
        {
            Dock = DockStyle.Fill,
            TabIndex = 2
        };
        _list.AccessibleName = "CRUD item list";
        _list.SelectedIndexChanged += (_, _) => UpdateActionState();
        _list.ItemActivate += async (_, _) =>
        {
            if (SelectedItem is not null)
                await EditAsync(SelectedItem);
        };
        _list.KeyDown += ListOnKeyDown;

        _emptyStateLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = _emptyStateFont,
            Margin = Padding.Empty,
            Padding = new Padding(16),
            Visible = false
        };

        listHost.Controls.Add(_list);
        listHost.Controls.Add(_emptyStateLabel);

        _footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PaginationWidth));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            Padding = new Padding(4, 0, 0, 0),
            AccessibleRole = AccessibleRole.StatusBar,
            AccessibleName = "List status"
        };

        _pagination = new HivePaginationBar
        {
            Dock = DockStyle.Fill,
            Height = FooterHeight,
            TabStop = true
        };
        _pagination.PreviousRequested += (_, _) => ChangePage(-1);
        _pagination.NextRequested += (_, _) => ChangePage(1);

        _footerLayout.Controls.Add(_statusLabel, 0, 0);
        _footerLayout.Controls.Add(_pagination, 1, 0);

        contentLayout.Controls.Add(listHost, 0, 0);
        contentLayout.Controls.Add(_footerLayout, 0, 1);

        _pageLayout.SetContent(contentLayout);
        Controls.Add(_pageLayout);

        _getItemDisplayName = item => item?.ToString() ?? "item";
        _pagination.PageNumber = 1;
        UpdateActionState();
        UpdateToolbarLayout();
        UpdateFooterLayout();
        UpdateEmptyState(0);
        UpdateStatusSummary();
        _typographyReady = true;
        ApplyThemeTypography();
    }

    public event EventHandler<HiveCrudOperationFailedEventArgs>? OperationFailed;

    public HiveListPageLayout PageLayout => _pageLayout;

    public ListView ListView => _list;

    public Panel HeaderPanel => _pageLayout.HeaderPanel;

    public Panel ActionBarPanel => _pageLayout.ActionBarPanel;

    public Label StatusLabel => _statusLabel;

    public TextBox SearchBox => _searchBox;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBusy => _busy;

    public IReadOnlyList<TItem> Items => _items;

    public TItem? SelectedItem =>
        _list.SelectedItems.Count == 0
            ? default
            : (TItem?)_list.SelectedItems[0].Tag;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowAdd
    {
        get => _addButton.Visible;
        set
        {
            _addButton.Visible = value;
            UpdateToolbarLayout();
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowEdit
    {
        get => _editButton.Visible;
        set
        {
            _editButton.Visible = value;
            UpdateToolbarLayout();
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowDelete
    {
        get => _deleteButton.Visible;
        set
        {
            _deleteButton.Visible = value;
            UpdateToolbarLayout();
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowRefresh
    {
        get => _refreshButton.Visible;
        set
        {
            _refreshButton.Visible = value;
            UpdateToolbarLayout();
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowSearch
    {
        get => _searchBox.Visible;
        set
        {
            _searchBox.Visible = value;
            _searchLabel.Visible = value;
            _searchPanel.Visible = value;
            UpdateToolbarLayout();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
                return;

            _searchText = normalized;
            _pagination.PageNumber = 1;
            if (!string.Equals(_searchBox.Text, normalized, StringComparison.Ordinal))
                _searchBox.Text = normalized;
            else
                RebuildItems();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SearchPlaceholder
    {
        get => _searchBox.PlaceholderText;
        set => _searchBox.PlaceholderText = value ?? string.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageSize
    {
        get => _pageSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_pageSize == value)
                return;

            _pageSize = value;
            _pagination.PageNumber = 1;
            RebuildItems();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageNumber
    {
        get => _pagination.PageNumber;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _pagination.PageNumber = value;
            RebuildItems();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowPagination
    {
        get => _pagination.Visible;
        set
        {
            if (_pagination.Visible == value)
                return;

            _pagination.Visible = value;
            UpdateFooterLayout();
        }
    }

    public HivePaginationBar PaginationBar => _pagination;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value ?? string.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Description
    {
        get => _descriptionLabel.Text;
        set => _descriptionLabel.Text = value ?? string.Empty;
    }

    public IReadOnlyList<HiveCrudColumn<TItem>> Columns => _columns;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<CancellationToken, Task<IReadOnlyList<TItem>>>? LoadItemsAsync
    {
        get => _loadItemsAsync;
        set
        {
            _loadItemsAsync = value;
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem?, CancellationToken, Task<TItem?>>? EditItemAsync
    {
        get => _editItemAsync;
        set
        {
            _editItemAsync = value;
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, CancellationToken, Task>? DeleteItemAsync
    {
        get => _deleteItemAsync;
        set
        {
            _deleteItemAsync = value;
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, string>? GetItemDisplayName
    {
        get => _getItemDisplayName;
        set => _getItemDisplayName = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, bool>? CanEditItem
    {
        get => _canEditItem;
        set
        {
            _canEditItem = value;
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, bool>? CanDeleteItem
    {
        get => _canDeleteItem;
        set
        {
            _canDeleteItem = value;
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, bool>? CanActivateItem
    {
        get => _canActivateItem;
        set
        {
            _canActivateItem = value;
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, CancellationToken, Task>? ActivateItemAsync
    {
        get => _activateItemAsync;
        set
        {
            _activateItemAsync = value;
            _activateButton.Visible = value is not null;
            UpdateToolbarLayout();
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, string?>? StatusSelector
    {
        get => _statusSelector;
        set
        {
            _statusSelector = value;
            var visible = value is not null;
            _statusFilterLabel.Visible = visible;
            _statusFilterBox.Visible = visible;

            if (visible)
                RebuildStatusFilterOptions();
            else
                _statusFilterBox.Items.Clear();

            UpdateSearchBoxWidth();
            RebuildItems();
        }
    }

    public void SetColumns(IReadOnlyList<HiveCrudColumn<TItem>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        _columns.Clear();
        foreach (var column in columns)
        {
            ArgumentNullException.ThrowIfNull(column);
            _columns.Add(column);
        }

        RebuildColumns();
        RebuildItems();
    }

    public void SetColumns(params HiveCrudColumn<TItem>[] columns) =>
        SetColumns((IReadOnlyList<HiveCrudColumn<TItem>>)columns);

    public void SetStatus(string text) =>
        _statusLabel.Text = text ?? string.Empty;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_loadItemsAsync is null)
            throw new InvalidOperationException(
                "LoadItemsAsync must be configured before refreshing the CRUD page.");

        if (_busy)
            return;

        await ExecuteAsync(
            HiveCrudOperation.Load,
            LoadItemsCoreAsync,
            cancellationToken);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateToolbarLayout();
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        UpdateThemeSubscription();
        ApplyThemeTypography();
        ApplyStatusColor();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyThemeTypography();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyThemeTypography();
    }

    private void ApplyThemeTypography()
    {
        if (!_typographyReady ||
            FindForm() is not HiveForm hiveForm)
        {
            return;
        }

        var typography = hiveForm.Theme.Typography;
        var family = typography.FontFamily;

        var previousTitle = ReplaceFontIfNeeded(
            ref _titleFont,
            family,
            typography.TitleSize,
            FontStyle.Bold);
        var previousDescription = ReplaceFontIfNeeded(
            ref _descriptionFont,
            family,
            typography.SmallSize,
            FontStyle.Regular);
        var previousSearchLabel = ReplaceFontIfNeeded(
            ref _searchLabelFont,
            family,
            typography.SectionSize,
            FontStyle.Bold);
        var previousEmptyState = ReplaceFontIfNeeded(
            ref _emptyStateFont,
            family,
            typography.BodySize,
            FontStyle.Regular);

        _titleLabel.Font = _titleFont;
        _descriptionLabel.Font = _descriptionFont;
        _searchLabel.Font = _searchLabelFont;
        _statusFilterLabel.Font = _searchLabelFont;
        _emptyStateLabel.Font = _emptyStateFont;

        previousTitle?.Dispose();
        previousDescription?.Dispose();
        previousSearchLabel?.Dispose();
        previousEmptyState?.Dispose();
    }

    private static Font? ReplaceFontIfNeeded(
        ref Font current,
        string family,
        float size,
        FontStyle style)
    {
        if (string.Equals(current.FontFamily.Name, family, StringComparison.Ordinal) &&
            Math.Abs(current.Size - size) <= 0.01f &&
            current.Style == style)
        {
            return null;
        }

        var previous = current;
        current = new Font(family, size, style);
        return previous;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationCancellation?.Cancel();
            _statusFilterBox.SelectedIndexChanged -= StatusFilterBoxOnSelectedIndexChanged;
            _searchBox.TextChanged -= SearchBoxOnTextChanged;
            _searchBox.KeyDown -= SearchBoxOnKeyDown;
        }

        base.Dispose(disposing);

        if (disposing)
        {
            _operationCancellation?.Dispose();
            _titleFont.Dispose();
            _descriptionFont.Dispose();
            _searchLabelFont.Dispose();
            _emptyStateFont.Dispose();
        }
    }

    private void UpdateFooterLayout()
    {
        var visible = _pagination.Visible;

        _footerLayout.SuspendLayout();
        try
        {
            _footerLayout.Controls.Clear();
            _footerLayout.ColumnStyles.Clear();

            if (visible)
            {
                _footerLayout.ColumnCount = 2;
                _footerLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f));
                _footerLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, PaginationWidth));
                _footerLayout.Controls.Add(_statusLabel, 0, 0);
                _footerLayout.Controls.Add(_pagination, 1, 0);
            }
            else
            {
                _footerLayout.ColumnCount = 1;
                _footerLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f));
                _footerLayout.Controls.Add(_statusLabel, 0, 0);
            }
        }
        finally
        {
            _footerLayout.ResumeLayout(true);
        }
    }

    private void UpdateToolbarLayout()
    {
        var compact = ClientSize.Width > 0 &&
                      ClientSize.Width < GetWideToolbarMinimumWidth();
        var expectedRows = compact && _searchBox.Visible ? 2 : 1;
        var expectedColumns = compact ? 1 : 2;
        var actionWidth = GetVisibleActionBarWidth(compact);
        var compactActionRows = compact
            ? GetCompactActionRowCount()
            : 1;
        var expectedActionBarHeight = compact
            ? (_searchBox.Visible ? 44 : 0) + (compactActionRows * 44)
            : ActionBarHeight;

        if (_compactToolbar == compact &&
            _actionLayout.ColumnCount == expectedColumns &&
            _actionLayout.RowCount == expectedRows &&
            _actionButtons.WrapContents == compact &&
            _pageLayout.ActionBarHeight == expectedActionBarHeight &&
            (compact ||
             _actionLayout.ColumnStyles.Count < 2 ||
             Math.Abs(_actionLayout.ColumnStyles[1].Width - actionWidth) < 0.1f))
        {
            UpdateSearchBoxWidth();
            return;
        }

        _compactToolbar = compact;

        _actionLayout.SuspendLayout();
        try
        {
            _actionLayout.Controls.Remove(_searchPanel);
            _actionLayout.Controls.Remove(_actionButtons);
            _actionLayout.ColumnStyles.Clear();
            _actionLayout.RowStyles.Clear();

            var actionButtonWidth = compact
                ? CompactActionButtonWidth
                : ActionButtonWidth;

            _addButton.Width = actionButtonWidth;
            _editButton.Width = actionButtonWidth;
            _activateButton.Width = actionButtonWidth;
            _deleteButton.Width = actionButtonWidth;
            _refreshButton.Width = actionButtonWidth;

            _actionButtons.WrapContents = compact;
            _searchLabel.Visible = _searchBox.Visible && !compact;

            if (compact)
            {
                _pageLayout.ActionBarHeight = expectedActionBarHeight;
                _actionLayout.ColumnCount = 1;

                if (_searchBox.Visible)
                {
                    _actionLayout.RowCount = 2;
                    _actionLayout.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100f));
                    _actionLayout.RowStyles.Add(
                        new RowStyle(SizeType.Absolute, 44f));
                    _actionLayout.RowStyles.Add(
                        new RowStyle(
                            SizeType.Absolute,
                            compactActionRows * 44f));
                    _actionLayout.Controls.Add(_searchPanel, 0, 0);
                    _actionLayout.Controls.Add(_actionButtons, 0, 1);
                }
                else
                {
                    _actionLayout.RowCount = 1;
                    _actionLayout.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100f));
                    _actionLayout.RowStyles.Add(
                        new RowStyle(SizeType.Absolute, compactActionRows * 44f));
                    _actionLayout.Controls.Add(_actionButtons, 0, 0);
                }
            }
            else
            {
                _pageLayout.ActionBarHeight = ActionBarHeight;
                _actionLayout.ColumnCount = 2;
                _actionLayout.RowCount = 1;
                _actionLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f));
                _actionLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, actionWidth));
                _actionLayout.RowStyles.Add(
                    new RowStyle(SizeType.Percent, 100f));
                _actionLayout.Controls.Add(_searchPanel, 0, 0);
                _actionLayout.Controls.Add(_actionButtons, 1, 0);
            }
        }
        finally
        {
            _actionLayout.ResumeLayout(true);
        }

        UpdateSearchBoxWidth();
    }

    private int GetCompactActionRowCount()
    {
        var visibleCount =
            (_addButton.Visible ? 1 : 0) +
            (_editButton.Visible ? 1 : 0) +
            (_activateButton.Visible ? 1 : 0) +
            (_deleteButton.Visible ? 1 : 0) +
            (_refreshButton.Visible ? 1 : 0);

        if (visibleCount == 0)
            return 1;

        var availableWidth = Math.Max(
            1,
            ClientSize.Width -
            _actionButtons.Padding.Left -
            _actionButtons.Padding.Right);

        var buttonWidth =
            CompactActionButtonWidth + ActionButtonSpacing;

        return Math.Max(
            1,
            (int)Math.Ceiling(
                visibleCount * buttonWidth /
                (double)availableWidth));
    }

    private void UpdateSearchBoxWidth()
    {
        if (!_searchPanel.Visible ||
            _searchPanel.ClientSize.Width <= 0)
            return;

        var availableWidth =
            _searchPanel.ClientSize.Width -
            _searchPanel.Padding.Left -
            _searchPanel.Padding.Right;

        if (_searchLabel.Visible)
        {
            availableWidth -=
                _searchLabel.Width +
                _searchLabel.Margin.Left +
                _searchLabel.Margin.Right;
        }

        if (_statusFilterLabel.Visible)
        {
            availableWidth -=
                _statusFilterLabel.Width +
                _statusFilterLabel.Margin.Left +
                _statusFilterLabel.Margin.Right +
                _statusFilterBox.Width +
                _statusFilterBox.Margin.Left +
                _statusFilterBox.Margin.Right;
        }

        var minimumWidth = _compactToolbar
            ? 112
            : MinimumSearchWidth;

        var targetWidth = availableWidth >= minimumWidth
            ? Math.Min(availableWidth, MaximumSearchWidth)
            : Math.Max(0, availableWidth);

        if (_searchBox.Width != targetWidth)
            _searchBox.Width = targetWidth;
    }

    private int GetWideToolbarMinimumWidth()
    {
        var requiredWidth = GetVisibleActionBarWidth(compact: false) + 24;

        if (!_searchPanel.Visible)
            return requiredWidth;

        requiredWidth +=
            _searchLabel.Width +
            _searchLabel.Margin.Left +
            _searchLabel.Margin.Right +
            MinimumSearchWidth +
            _searchPanel.Padding.Left +
            _searchPanel.Padding.Right;

        if (_statusFilterLabel.Visible)
        {
            requiredWidth +=
                _statusFilterLabel.Width +
                _statusFilterLabel.Margin.Left +
                _statusFilterLabel.Margin.Right +
                _statusFilterBox.Width +
                _statusFilterBox.Margin.Left +
                _statusFilterBox.Margin.Right;
        }

        return requiredWidth;
    }

    private int GetVisibleActionBarWidth(bool compact)
    {
        if (compact)
            return 0;

        var buttonWidth = ActionButtonWidth;
        var visibleCount =
            (_addButton.Visible ? 1 : 0) +
            (_editButton.Visible ? 1 : 0) +
            (_activateButton.Visible ? 1 : 0) +
            (_deleteButton.Visible ? 1 : 0) +
            (_refreshButton.Visible ? 1 : 0);

        return visibleCount * (buttonWidth + ActionButtonSpacing);
    }

    private void StatusFilterBoxOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_statusSelector is null || _updatingStatusFilter)
            return;

        _pagination.PageNumber = 1;
        RebuildItems();
    }

    private void RebuildStatusFilterOptions()
    {
        if (_statusSelector is null)
            return;

        var previous = _statusFilterBox.SelectedItem as string;
        var values = _items
            .Select(_statusSelector)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _updatingStatusFilter = true;
        _statusFilterBox.BeginUpdate();
        try
        {
            _statusFilterBox.Items.Clear();
            _statusFilterBox.Items.Add(AllStatusFilter);

            foreach (var value in values)
                _statusFilterBox.Items.Add(value);

            var selected = !string.IsNullOrWhiteSpace(previous) &&
                           _statusFilterBox.Items.Contains(previous)
                ? previous
                : AllStatusFilter;

            _statusFilterBox.SelectedItem = selected;
        }
        finally
        {
            _statusFilterBox.EndUpdate();
            _updatingStatusFilter = false;
        }
    }

    private bool MatchesStatusFilter(TItem item)
    {
        if (_statusSelector is null ||
            _statusFilterBox.SelectedItem is not string selected ||
            string.Equals(selected, AllStatusFilter, StringComparison.Ordinal))
        {
            return true;
        }

        var status = _statusSelector(item);
        return string.Equals(status, selected, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBoxOnTextChanged(object? sender, EventArgs e)
    {
        var value = _searchBox.Text;
        if (string.Equals(_searchText, value, StringComparison.Ordinal))
            return;

        _searchText = value;
        _pagination.PageNumber = 1;
        RebuildItems();
    }

    private void SearchBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape || string.IsNullOrEmpty(_searchBox.Text))
            return;

        _searchBox.Clear();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private async void ListOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_busy)
            return;

        if (e.KeyCode == Keys.Enter && SelectedItem is not null)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            await EditAsync(SelectedItem);
        }
        else if (e.KeyCode == Keys.Delete && SelectedItem is not null)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            await DeleteAsync();
        }
    }

    private async Task EditAsync(TItem? item)
    {
        if (_editItemAsync is null || _busy)
            return;

        if (item is null && !_addButton.Visible)
            return;

        if (item is not null &&
            _canEditItem is not null &&
            !_canEditItem(item))
            return;

        var scrollState = CaptureScrollState();

        await ExecuteAsync(
            HiveCrudOperation.Edit,
            async token =>
            {
                SetStatus(item is null ? "Adding..." : "Editing...");
                var result = await _editItemAsync(item, token);
                if (result is null)
                    return;

                await LoadItemsCoreAsync(token);
            },
            CancellationToken.None);

        RestoreScrollState(scrollState);
    }

    private List<(ScrollableControl Control, Point Position)> CaptureScrollState()
    {
        var state = new List<(ScrollableControl Control, Point Position)>();
        for (Control? control = this; control is not null; control = control.Parent)
        {
            if (control is ScrollableControl scrollable && scrollable.AutoScroll)
                state.Add((scrollable, scrollable.AutoScrollPosition));
        }

        return state;
    }

    private void RestoreScrollState(
        IReadOnlyList<(ScrollableControl Control, Point Position)> state)
    {
        if (state.Count == 0 || IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new MethodInvoker(() =>
            {
                if (IsDisposed || Disposing)
                    return;

                foreach (var (control, position) in state)
                {
                    if (control.IsDisposed || control.Disposing || !control.IsHandleCreated)
                        continue;

                    control.AutoScrollPosition = new Point(
                        -position.X,
                        -position.Y);
                }
            }));
        }
        catch (ObjectDisposedException)
        {
            System.Diagnostics.Debug.WriteLine(
                "HiveCrudPage scroll restoration skipped because the control is disposing.");
        }
        catch (InvalidOperationException) when (!IsHandleCreated || IsDisposed)
        {
            System.Diagnostics.Debug.WriteLine(
                "HiveCrudPage scroll restoration skipped because its handle is no longer available.");
        }
    }

    private async Task DeleteAsync()
    {
        var item = SelectedItem;
        if (item is null ||
            _deleteItemAsync is null ||
            _busy)
            return;

        if (_canDeleteItem is not null && !_canDeleteItem(item))
            return;

        var displayName = _getItemDisplayName?.Invoke(item) ?? "item";
        var result = HiveMessageBox.ShowQuestion(
            FindForm(),
            $"Delete '{displayName}'?",
            "Delete",
            MessageBoxButtons.YesNo,
            ThemeManager());

        if (result != DialogResult.Yes)
            return;

        await ExecuteAsync(
            HiveCrudOperation.Delete,
            async token =>
            {
                SetStatus("Deleting...");
                await _deleteItemAsync(item, token);
                await LoadItemsCoreAsync(token);
            },
            CancellationToken.None);
    }

    private async Task ActivateAsync(TItem? item)
    {
        if (item is null ||
            _activateItemAsync is null ||
            _busy)
            return;

        if (_canActivateItem is not null && !_canActivateItem(item))
            return;

        var displayName = _getItemDisplayName?.Invoke(item) ?? "item";
        var result = HiveMessageBox.ShowQuestion(
            FindForm(),
            $"Activate '{displayName}'?",
            "Activate",
            MessageBoxButtons.YesNo,
            ThemeManager());

        if (result != DialogResult.Yes)
            return;

        await ExecuteAsync(
            HiveCrudOperation.Activate,
            async token =>
            {
                SetStatus("Activating...");
                await _activateItemAsync(item, token);
                await LoadItemsCoreAsync(token);
            },
            CancellationToken.None);
    }

    private async Task LoadItemsCoreAsync(CancellationToken cancellationToken)
    {
        if (_loadItemsAsync is null)
            throw new InvalidOperationException(
                "LoadItemsAsync must be configured before refreshing the CRUD page.");

        SetStatus("Loading...");
        var items = await _loadItemsAsync(cancellationToken);
        _items = items ?? throw new InvalidOperationException(
            "LoadItemsAsync returned null.");

        RebuildStatusFilterOptions();
        RebuildItems();
    }

    private async Task ExecuteAsync(
        HiveCrudOperation operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = source;
        SetBusy(true);

        try
        {
            await action(source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception exception)
        {
            SetStatus("Operation failed.");
            RaiseOperationFailed(operation, exception);
        }
        finally
        {
            if (ReferenceEquals(_operationCancellation, source))
                _operationCancellation = null;

            source.Dispose();
            SetBusy(false);
        }
    }

    private void RaiseOperationFailed(
        HiveCrudOperation operation,
        Exception exception)
    {
        var handler = OperationFailed;
        if (handler is null)
        {
            System.Diagnostics.Debug.WriteLine(exception.ToString());
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(exception)
                .Throw();
        }

        handler(
            this,
            new HiveCrudOperationFailedEventArgs(
                operation,
                exception));
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _list.Enabled = !busy;
        _searchBox.Enabled = !busy;
        _statusFilterBox.Enabled = !busy;

        if (FindForm() is HiveForm hiveForm)
        {
            var theme = hiveForm.ThemeManager.Theme;
            _searchBox.BackColor = busy
                ? theme.Palette.DisabledBackground
                : theme.Palette.InputBackground;
            _searchBox.ForeColor = busy
                ? theme.Palette.DisabledText
                : theme.Palette.Text;
        }

        _pagination.Enabled = !busy;
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        var item = SelectedItem;
        var hasSelection = item is not null;
        var canEdit = hasSelection &&
            (_canEditItem?.Invoke(item!) ?? true);
        var canDelete = hasSelection &&
            (_canDeleteItem?.Invoke(item!) ?? true);
        var canActivate = hasSelection &&
            (_canActivateItem?.Invoke(item!) ?? true);

        _addButton.Enabled = !_busy && _editItemAsync is not null;
        _editButton.Enabled = !_busy && canEdit && _editItemAsync is not null;
        _activateButton.Enabled = !_busy && canActivate && _activateItemAsync is not null;
        _deleteButton.Enabled = !_busy && canDelete && _deleteItemAsync is not null;
        _refreshButton.Enabled = !_busy && _loadItemsAsync is not null;
    }

    private void RebuildColumns()
    {
        _list.BeginUpdate();
        try
        {
            _list.Columns.Clear();
            foreach (var column in _columns)
                _list.Columns.Add(column.Header, column.Width);
        }
        finally
        {
            _list.EndUpdate();
        }

        if (_list is HiveListView hiveList)
            hiveList.ResetColumnLayout();
    }

    private void RebuildItems()
    {
        var previouslySelected = SelectedItem;

        _list.BeginUpdate();
        try
        {
            _list.SelectedItems.Clear();
            _list.Items.Clear();

            var matchingCount = CountMatchingItems();
            var totalPages = matchingCount == 0
                ? 1
                : (matchingCount + _pageSize - 1) / _pageSize;

            if (_pagination.PageNumber > totalPages)
                _pagination.PageNumber = totalPages;

            var firstMatchIndex = (_pagination.PageNumber - 1) * _pageSize;
            var matchedIndex = 0;
            var visibleCount = 0;

            foreach (var item in _items)
            {
                if (!MatchesSearch(item))
                    continue;

                if (matchedIndex >= firstMatchIndex &&
                    visibleCount < _pageSize)
                {
                    var values = new string[_columns.Count];
                    for (var index = 0; index < _columns.Count; index++)
                        values[index] =
                            _columns[index].ValueSelector(item) ?? string.Empty;

                    var listItem = new ListViewItem(values)
                    {
                        Tag = item
                    };

                    for (var index = 0; index < _columns.Count; index++)
                    {
                        var foreground = _columns[index]
                            .ForegroundColorSelector?
                            .Invoke(item);

                        if (foreground.HasValue)
                            listItem.SubItems[index].ForeColor = foreground.Value;
                    }

                    _list.Items.Add(listItem);

                    if (previouslySelected is not null &&
                        Equals(previouslySelected, item))
                        listItem.Selected = true;

                    visibleCount++;
                }

                matchedIndex++;
                if (visibleCount >= _pageSize &&
                    matchedIndex >= firstMatchIndex + _pageSize)
                    break;
            }

            _pagination.CanGoPrevious = _pagination.PageNumber > 1;
            _pagination.CanGoNext = _pagination.PageNumber < totalPages;
            _pagination.PageText = matchingCount == 0
                ? "No pages"
                : $"Page {_pagination.PageNumber} of {totalPages}";

            _statusLabel.Text = BuildStatusText(
                matchingCount,
                visibleCount,
                _pagination.PageNumber,
                _pageSize);
            UpdateEmptyState(matchingCount);
        }
        finally
        {
            _list.EndUpdate();
        }

        UpdateActionState();
    }

    private int CountMatchingItems()
    {
        var count = 0;
        foreach (var item in _items)
        {
            if (MatchesSearch(item))
                count++;
        }

        return count;
    }

    private bool MatchesSearch(TItem item)
    {
        if (!MatchesStatusFilter(item))
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        for (var index = 0; index < _columns.Count; index++)
        {
            var value = _columns[index].ValueSelector(item);
            if (value?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    }

    private void UpdateEmptyState(int matchingCount)
    {
        var show = matchingCount == 0;
        _emptyStateLabel.Visible = show;
        _emptyStateLabel.Text =
            _items.Count == 0
                ? "No items to display."
                : "No items match the current filters.";
    }

    private void UpdateStatusSummary()
    {
        RebuildItems();
    }

    private static string BuildStatusText(
        int totalCount,
        int visibleCount,
        int pageNumber,
        int pageSize)
    {
        if (totalCount == 0)
            return "0 items";

        if (visibleCount >= totalCount)
            return $"{totalCount:N0} items";

        var firstVisible = ((pageNumber - 1) * pageSize) + 1;
        var lastVisible = firstVisible + visibleCount - 1;

        return $"Showing {firstVisible:N0}-{lastVisible:N0} of {totalCount:N0}";
    }

    private void ChangePage(int delta)
    {
        if (_busy)
            return;

        var nextPage = _pagination.PageNumber + delta;
        if (nextPage < 1)
            return;

        _pagination.PageNumber = nextPage;
        RebuildItems();
    }

    private static HiveButton CreateActionButton(
        string text,
        HiveButtonStyle style) =>
        new()
        {
            Text = text,
            Style = style,
            Width = ActionButtonWidth,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };

    private IHiveThemeManager? ThemeManager()
    {
        return FindForm() switch
        {
            HiveForm hiveForm => hiveForm.ThemeManager,
            _ => null
        };
    }
}
