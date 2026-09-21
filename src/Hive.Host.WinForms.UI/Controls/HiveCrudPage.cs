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
    private readonly HiveListPageLayout _pageLayout;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly FlowLayoutPanel _actionButtons;
    private readonly Label _statusLabel;
    private readonly HiveButton _addButton;
    private readonly HiveButton _editButton;
    private readonly HiveButton _deleteButton;
    private readonly HiveButton _refreshButton;
    private readonly ListView _list;
    private readonly List<HiveCrudColumn<TItem>> _columns = new();

    private IReadOnlyList<TItem> _items = Array.Empty<TItem>();
    private Func<CancellationToken, Task<IReadOnlyList<TItem>>>? _loadItemsAsync;
    private Func<TItem?, CancellationToken, Task<TItem?>>? _editItemAsync;
    private Func<TItem, CancellationToken, Task>? _deleteItemAsync;
    private Func<TItem, string>? _getItemDisplayName;
    private CancellationTokenSource? _operationCancellation;
    private bool _busy;

    public HiveCrudPage()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _pageLayout = new HiveListPageLayout
        {
            Dock = DockStyle.Fill
        };

        _titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Items"
        };

        _descriptionLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 8.8f),
            Margin = new Padding(0, 5, 0, 0),
            Padding = Padding.Empty
        };

        _pageLayout.HeaderPanel.Padding = new Padding(0, 2, 0, 0);
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
        actionLayout.ColumnStyles.Add(new ColumnStyle.Absolute, 220f);

        _actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 5, 0, 5)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = Padding.Empty,
            Padding = new Padding(8, 0, 0, 0)
        };

        _addButton = CreateActionButton("Add", HiveButtonStyle.Primary);
        _editButton = CreateActionButton("Edit", HiveButtonStyle.Secondary);
        _deleteButton = CreateActionButton("Delete", HiveButtonStyle.Secondary);
        _refreshButton = CreateActionButton("Refresh", HiveButtonStyle.Secondary);

        _addButton.Click += async (_, _) => await EditAsync(null);
        _editButton.Click += async (_, _) => await EditAsync(SelectedItem);
        _deleteButton.Click += async (_, _) => await DeleteAsync();
        _refreshButton.Click += async (_, _) => await RefreshAsync();

        _actionButtons.Controls.Add(_addButton);
        _actionButtons.Controls.Add(_editButton);
        _actionButtons.Controls.Add(_deleteButton);
        _actionButtons.Controls.Add(_refreshButton);

        actionLayout.Controls.Add(_actionButtons, 0, 0);
        actionLayout.Controls.Add(_statusLabel, 1, 0);
        _pageLayout.ActionBarPanel.Controls.Add(actionLayout);

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
            Margin = Padding.Empty
        };
        _list.SelectedIndexChanged += (_, _) => UpdateActionState();
        _list.DoubleClick += async (_, _) =>
        {
            if (SelectedItem is not null)
                await EditAsync(SelectedItem);
        };

        _pageLayout.SetContent(_list);
        Controls.Add(_pageLayout);

        _getItemDisplayName = item => item?.ToString() ?? "item";
        UpdateActionState();
    }

    public event EventHandler<HiveCrudOperationFailedEventArgs>? OperationFailed;

    public HiveListPageLayout PageLayout => _pageLayout;

    public ListView ListView => _list;

    public Panel HeaderPanel => _pageLayout.HeaderPanel;

    public Panel ActionBarPanel => _pageLayout.ActionBarPanel;

    public Label StatusLabel => _statusLabel;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBusy => _busy;

    public IReadOnlyList<TItem> Items => _items;

    public TItem? SelectedItem =>
        _list.SelectedItems.Count == 0
            ? default
            : (TItem?)_list.SelectedItems[0].Tag;

    public bool AllowAdd
    {
        get => _addButton.Visible;
        set
        {
            _addButton.Visible = value;
            UpdateActionState();
        }
    }

    public bool AllowEdit
    {
        get => _editButton.Visible;
        set
        {
            _editButton.Visible = value;
            UpdateActionState();
        }
    }

    public bool AllowDelete
    {
        get => _deleteButton.Visible;
        set
        {
            _deleteButton.Visible = value;
            UpdateActionState();
        }
    }

    public bool ShowRefresh
    {
        get => _refreshButton.Visible;
        set => _refreshButton.Visible = value;
    }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value ?? string.Empty;
    }

    public string Description
    {
        get => _descriptionLabel.Text;
        set => _descriptionLabel.Text = value ?? string.Empty;
    }

    public IReadOnlyList<HiveCrudColumn<TItem>> Columns => _columns;

    public Func<CancellationToken, Task<IReadOnlyList<TItem>>>? LoadItemsAsync
    {
        get => _loadItemsAsync;
        set
        {
            _loadItemsAsync = value;
            UpdateActionState();
        }
    }

    public Func<TItem?, CancellationToken, Task<TItem?>>? EditItemAsync
    {
        get => _editItemAsync;
        set
        {
            _editItemAsync = value;
            UpdateActionState();
        }
    }

    public Func<TItem, CancellationToken, Task>? DeleteItemAsync
    {
        get => _deleteItemAsync;
        set
        {
            _deleteItemAsync = value;
            UpdateActionState();
        }
    }

    public Func<TItem, string>? GetItemDisplayName
    {
        get => _getItemDisplayName;
        set => _getItemDisplayName = value;
    }

    public void SetColumns(
        IReadOnlyList<HiveCrudColumn<TItem>> columns)
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

    public void SetColumns(
        params HiveCrudColumn<TItem>[] columns) =>
        SetColumns((IReadOnlyList<HiveCrudColumn<TItem>>)columns);

    public void SetStatus(string text) =>
        _statusLabel.Text = text ?? string.Empty;

    public async Task RefreshAsync(
        CancellationToken cancellationToken = default)
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
        }

        base.Dispose(disposing);
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
                {
                    SetStatus("No changes.");
                    return;
                }

                await LoadItemsCoreAsync(token);
                SetStatus(item is null ? "Added." : "Updated.");
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
                SetStatus("Deleted.");
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
        SetStatus(_items.Count == 0
            ? "No items."
            : $"{_items.Count} item(s).");
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
            OperationFailed?.Invoke(
                this,
                new HiveCrudOperationFailedEventArgs(operation, exception));
        }
        finally
        {
            if (ReferenceEquals(_operationCancellation, source))
            {
                _operationCancellation = null;
            }

            source.Dispose();
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _list.Enabled = !busy;
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
            _list.Items.Clear();

            foreach (var item in _items)
            {
                var values = new string[_columns.Count];
                for (var index = 0; index < _columns.Count; index++)
                    values[index] = _columns[index].ValueSelector(item) ?? string.Empty;

                var row = new ListViewItem(values)
                {
                    Tag = item
                };
                _list.Items.Add(row);
            }
        }
        finally
        {
            _list.EndUpdate();
        }

        UpdateActionState();
    }

    private static HiveButton CreateActionButton(
        string text,
        HiveButtonStyle style) =>
        new()
        {
            Text = text,
            Style = style,
            Width = 112,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
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
