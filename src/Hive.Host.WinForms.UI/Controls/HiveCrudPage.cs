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
    private const int ActionBarHeight = 48;
    private const int StatusHeight = 28;
    private const int ActionButtonWidth = 88;
    private const int ActionBarActionsWidth = 400;

    private readonly HiveListPageLayout _pageLayout;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _searchLabel;
    private readonly TextBox _searchBox;
    private readonly FlowLayoutPanel _actionButtons;
    private readonly Label _statusLabel;
    private readonly HiveButton _addButton;
    private readonly HiveButton _editButton;
    private readonly HiveButton _deleteButton;
    private readonly HiveButton _refreshButton;
    private readonly ListView _list;
    private readonly Label _emptyStateLabel;
    private readonly Font _titleFont;
    private readonly Font _descriptionFont;
    private readonly Font _searchLabelFont;
    private readonly List<HiveCrudColumn<TItem>> _columns = new();

    private IReadOnlyList<TItem> _items = Array.Empty<TItem>();
    private Func<CancellationToken, Task<IReadOnlyList<TItem>>>? _loadItemsAsync;
    private Func<TItem?, CancellationToken, Task<TItem?>>? _editItemAsync;
    private Func<TItem, CancellationToken, Task>? _deleteItemAsync;
    private Func<TItem, string>? _getItemDisplayName;
    private CancellationTokenSource? _operationCancellation;
    private string _searchText = string.Empty;
    private bool _busy;

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

        _titleFont = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold);
        _descriptionFont = new Font("Segoe UI", 8.9f);
        _searchLabelFont = new Font("Segoe UI Semibold", 8.7f, FontStyle.Bold);

        _titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = _titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Items"
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

        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ActionBarActionsWidth));

        var searchPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 12, 6)
        };

        _searchLabel = new Label
        {
            AutoSize = false,
            Font = _searchLabelFont,
            Text = "Search",
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 48,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0),
            Padding = Padding.Empty
        };

        _searchBox = new TextBox
        {
            Width = 180,
            Height = 36,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            Padding = new Padding(8, 7, 8, 7),
            PlaceholderText = "Search by any visible value..."
        };
        _searchBox.TextChanged += SearchBoxOnTextChanged;
        _searchBox.KeyDown += SearchBoxOnKeyDown;

        searchPanel.Controls.Add(_searchLabel);
        searchPanel.Controls.Add(_searchBox);

        _actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 0, 6)
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

        actionLayout.Controls.Add(searchPanel, 0, 0);
        actionLayout.Controls.Add(_actionButtons, 1, 0);
        _pageLayout.ActionBarPanel.Controls.Add(actionLayout);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusHeight));

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(1)
        };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            GridLines = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.FixedSingle,
            LabelWrap = false,
            Margin = Padding.Empty
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
            Margin = Padding.Empty,
            Padding = new Padding(16),
            Visible = false
        };

        listHost.Controls.Add(_list);
        listHost.Controls.Add(_emptyStateLabel);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            Padding = new Padding(2, 0, 0, 0)
        };

        contentLayout.Controls.Add(listHost, 0, 0);
        contentLayout.Controls.Add(_statusLabel, 0, 1);

        _pageLayout.SetContent(contentLayout);
        Controls.Add(_pageLayout);

        _getItemDisplayName = item => item?.ToString() ?? "item";
        UpdateActionState();
        UpdateEmptyState(0);
        UpdateStatusSummary(0);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _titleFont.Dispose();
            _descriptionFont.Dispose();
            _searchLabelFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SearchBoxOnTextChanged(object? sender, EventArgs e)
    {
        var value = _searchBox.Text;
        if (string.Equals(_searchText, value, StringComparison.Ordinal))
            return;

        _searchText = value;
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
            UpdateStatusSummary();
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
        _list.BeginUpdate();
        try
        {
            _list.SelectedItems.Clear();
            _list.Items.Clear();

            var visibleCount = 0;

            foreach (var item in _items)
            {
                var values = new string[_columns.Count];
                var matches = string.IsNullOrWhiteSpace(_searchText);

                for (var index = 0; index < _columns.Count; index++)
                {
                    var value = _columns[index].ValueSelector(item) ?? string.Empty;
                    values[index] = value;

                    if (!matches &&
                        value.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                        matches = true;
                }

                if (!matches)
                    continue;

                _list.Items.Add(new ListViewItem(values)
                {
                    Tag = item
                });
                visibleCount++;
            }

            _statusLabel.Text = BuildStatusText(_items.Count, visibleCount);
            UpdateEmptyState(visibleCount);
        }
        finally
        {
            _list.EndUpdate();
        }

        UpdateActionState();
    }

    private void UpdateEmptyState(int visibleCount)
    {
        var show = visibleCount == 0;
        _emptyStateLabel.Visible = show;
        _emptyStateLabel.Text =
            _items.Count == 0
                ? "No items to display."
                : "No items match the current search.";
    }

    private void UpdateStatusSummary(int? visibleCount = null)
    {
        var count = visibleCount ?? _list.Items.Count;
        _statusLabel.Text = BuildStatusText(_items.Count, count);
        UpdateEmptyState(count);
    }

    private static string BuildStatusText(int totalCount, int visibleCount)
    {
        if (totalCount == 0)
            return "0 items";

        return totalCount == visibleCount
            ? $"{totalCount:N0} items"
            : $"{visibleCount:N0} of {totalCount:N0} items";
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
