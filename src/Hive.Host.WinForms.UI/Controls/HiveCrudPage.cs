using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public enum HiveCrudOperation
{
    Load,
    Edit,
    Delete
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
    private const int ActionButtonWidth = 84;
    private const int ActionBarActionsWidth = 380;
    private const int PaginationWidth = 230;
    private const int DefaultPageSize = 25;

    private readonly HiveListPageLayout _pageLayout;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _searchLabel;
    private readonly TextBox _searchBox;
    private readonly TableLayoutPanel _actionLayout;
    private readonly FlowLayoutPanel _searchPanel;
    private readonly FlowLayoutPanel _actionButtons;
    private readonly Label _statusLabel;
    private readonly HiveButton _addButton;
    private readonly HiveButton _editButton;
    private readonly HiveButton _deleteButton;
    private readonly HiveButton _refreshButton;
    private readonly ListView _list;
    private readonly Label _emptyStateLabel;
    private readonly HivePaginationBar _pagination;
    private readonly Font _titleFont;
    private readonly Font _descriptionFont;
    private readonly Font _searchLabelFont;
    private readonly Font _emptyStateFont;
    private readonly List<HiveCrudColumn<TItem>> _columns = new();

    private IReadOnlyList<TItem> _items = Array.Empty<TItem>();
    private Func<CancellationToken, Task<IReadOnlyList<TItem>>>? _loadItemsAsync;
    private Func<TItem?, CancellationToken, Task<TItem?>>? _editItemAsync;
    private Func<TItem, CancellationToken, Task>? _deleteItemAsync;
    private Func<TItem, string>? _getItemDisplayName;
    private CancellationTokenSource? _operationCancellation;
    private string _searchText = string.Empty;
    private int _pageSize = DefaultPageSize;
    private bool _busy;
    private bool _compactToolbar = false;

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

        _titleFont = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
        _descriptionFont = new Font("Segoe UI", 8.9f);
        _searchLabelFont = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
        _emptyStateFont = new Font("Segoe UI", 9.5f);

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

        _pageLayout.HeaderPanel.Padding = new Padding(0, 4, 0, 4);
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
        _actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ActionBarActionsWidth));

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
            Width = 210,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 6),
            PlaceholderText = "Search...",
            TabIndex = 1
        };
        _searchBox.TextChanged += SearchBoxOnTextChanged;
        _searchBox.KeyDown += SearchBoxOnKeyDown;

        _searchPanel.Controls.Add(_searchLabel);
        _searchPanel.Controls.Add(_searchBox);

        _actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(4, 4, 0, 4)
        };

        _addButton = CreateActionButton("Add", HiveButtonStyle.Primary);
        _editButton = CreateActionButton("Edit", HiveButtonStyle.Secondary);
        _deleteButton = CreateActionButton("Delete", HiveButtonStyle.Danger);
        _refreshButton = CreateActionButton("Refresh", HiveButtonStyle.Secondary);

        _addButton.Click += async (_, _) => await EditAsync(null);
        _editButton.Click += async (_, _) => await EditAsync(SelectedItem);
        _deleteButton.Click += async (_, _) => await DeleteAsync();
        _refreshButton.Click += async (_, _) => await RefreshAsync();

        _actionButtons.Controls.Add(_addButton);
        _actionButtons.Controls.Add(_editButton);
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

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(1)
        };

        _list = new HiveListView
        {
            Dock = DockStyle.Fill,
            TabIndex = 2
        };
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

        var footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PaginationWidth));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            Padding = new Padding(4, 0, 0, 0)
        };

        _pagination = new HivePaginationBar
        {
            Dock = DockStyle.Fill,
            Height = FooterHeight,
            TabStop = true
        };
        _pagination.PreviousRequested += (_, _) => ChangePage(-1);
        _pagination.NextRequested += (_, _) => ChangePage(1);

        footerLayout.Controls.Add(_statusLabel, 0, 0);
        footerLayout.Controls.Add(_pagination, 1, 0);

        contentLayout.Controls.Add(listHost, 0, 0);
        contentLayout.Controls.Add(footerLayout, 0, 1);

        _pageLayout.SetContent(contentLayout);
        Controls.Add(_pageLayout);

        _getItemDisplayName = item => item?.ToString() ?? "item";
        _pagination.PageNumber = 1;
        UpdateActionState();
        UpdateToolbarLayout();
        UpdateEmptyState(0);
        UpdateStatusSummary();
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
        set => _pagination.Visible = value;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _operationCancellation?.Cancel();

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

    private void UpdateToolbarLayout()
    {
        var compact = ClientSize.Width > 0 && ClientSize.Width < 760;
        if (_compactToolbar == compact &&
            _actionLayout.ColumnCount == (compact ? 1 : 2))
            return;

        _compactToolbar = compact;

        _actionLayout.SuspendLayout();
        try
        {
            _actionLayout.Controls.Remove(_searchPanel);
            _actionLayout.Controls.Remove(_actionButtons);
            _actionLayout.ColumnStyles.Clear();
            _actionLayout.RowStyles.Clear();

            if (compact)
            {
                _pageLayout.ActionBarHeight = 88;
                _actionLayout.ColumnCount = 1;
                _actionLayout.RowCount = 2;
                _actionLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f));
                _actionLayout.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 40f));
                _actionLayout.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 44f));
                _actionLayout.Controls.Add(_searchPanel, 0, 0);
                _actionLayout.Controls.Add(_actionButtons, 0, 1);
            }
            else
            {
                _pageLayout.ActionBarHeight = ActionBarHeight;
                _actionLayout.ColumnCount = 2;
                _actionLayout.RowCount = 1;
                _actionLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f));
                _actionLayout.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, ActionBarActionsWidth));
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
    }

    private async Task DeleteAsync()
    {
        var item = SelectedItem;
        if (item is null ||
            _deleteItemAsync is null ||
            _busy)
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

    private async Task LoadItemsCoreAsync(CancellationToken cancellationToken)
    {
        if (_loadItemsAsync is null)
            throw new InvalidOperationException(
                "LoadItemsAsync must be configured before refreshing the CRUD page.");

        SetStatus("Loading...");
        var items = await _loadItemsAsync(cancellationToken);
        _items = items ?? throw new InvalidOperationException(
            "LoadItemsAsync returned null.");

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
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        var hasSelection = SelectedItem is not null;
        _addButton.Enabled = !_busy && _editItemAsync is not null;
        _editButton.Enabled = !_busy && hasSelection && _editItemAsync is not null;
        _deleteButton.Enabled = !_busy && hasSelection && _deleteItemAsync is not null;
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

            _statusLabel.Text = BuildStatusText(matchingCount, visibleCount);
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

    private bool TryBuildVisibleRow(TItem item, string[] values)
    {
        var matches = string.IsNullOrWhiteSpace(_searchText);

        for (var index = 0; index < _columns.Count; index++)
        {
            var value = _columns[index].ValueSelector(item) ?? string.Empty;
            values[index] = value;

            if (!matches &&
                value.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                matches = true;
        }

        return matches;
    }

    private bool MatchesSearch(TItem item)
    {
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
                : "No items match the current search.";
    }

    private void UpdateStatusSummary(int? visibleCount = null)
    {
        RebuildItems();
    }

    private static string BuildStatusText(int totalCount, int visibleCount)
    {
        if (totalCount == 0)
            return "0 items";

        return totalCount == visibleCount
            ? $"{totalCount:N0} items"
            : $"{totalCount:N0} items";
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
