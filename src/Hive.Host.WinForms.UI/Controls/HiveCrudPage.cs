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

public enum HiveStatusTone
{
    Neutral,
    Information,
    Success,
    Warning,
    Error
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
    private readonly HiveCrudPageListController<TItem> _listController;
    private readonly HiveCrudPageOperationController _operationController;
    private readonly HiveCrudPageLayoutController? _layoutController;

    private Func<CancellationToken, Task<IReadOnlyList<TItem>>>? _loadItemsAsync;
    private Func<TItem?, CancellationToken, Task<TItem?>>? _editItemAsync;
    private Func<TItem, CancellationToken, Task>? _deleteItemAsync;
    private Func<TItem, string>? _getItemDisplayName;
    private Func<TItem, bool>? _canEditItem;
    private Func<TItem, bool>? _canDeleteItem;
    private Func<TItem, bool>? _canActivateItem;
    private Func<TItem, CancellationToken, Task>? _activateItemAsync;



    public HiveCrudPage()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _pageLayout = new HiveListPageLayout
        {
            Dock = DockStyle.Fill,
            HeaderHeight = HiveCrudPageLayoutController.HeaderHeight,
            ActionBarHeight = HiveCrudPageLayoutController.ActionBarHeight
        };

        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        var titleFont = new Font(fallbackFont.FontFamily, 15f, FontStyle.Bold);
        var descriptionFont = new Font(fallbackFont.FontFamily, 8.9f);
        var searchLabelFont = new Font(fallbackFont.FontFamily, 8.8f, FontStyle.Bold);
        var emptyStateFont = new Font(fallbackFont.FontFamily, 9.5f);

        _titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = titleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Items",
            TabIndex = 0
        };

        _descriptionLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = descriptionFont,
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
        _actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HiveCrudPageLayoutController.InitialActionBarActionsWidth));

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
            Font = searchLabelFont,
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
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 6),
            PlaceholderText = "Search...",
            TabIndex = 1,
            AccessibleName = "Search items",
            AccessibleDescription = "Filter the current list by the visible column values."
        };
        _searchBox.AccessibleRole = AccessibleRole.Text;
                _searchBox.KeyDown += SearchBoxOnKeyDown;

        _statusFilterLabel = new Label
        {
            AutoSize = false,
            Font = searchLabelFont,
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
            IntegralHeight = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 0, 4),
            Visible = false,
            AccessibleName = "Status filter",
            AccessibleDescription = "Filter the current list by resource lifecycle status."
        };
        
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
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, HiveCrudPageLayoutController.FooterHeight));

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
            Font = emptyStateFont,
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
        _footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HiveCrudPageLayoutController.PaginationWidth));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            Padding = new Padding(4, 0, 0, 0),
            AccessibleRole = AccessibleRole.StatusBar,
            AccessibleName = "List status",
            AutoEllipsis = true
        };

        _pagination = new HivePaginationBar
        {
            Dock = DockStyle.Fill,
            Height = HiveCrudPageLayoutController.FooterHeight,
            TabStop = true
        };
        _pagination.PreviousRequested += (_, _) => ChangePage(-1);
        _pagination.NextRequested += (_, _) => ChangePage(1);

        _footerLayout.Controls.Add(_statusLabel, 0, 0);
        _footerLayout.Controls.Add(_pagination, 1, 0);

        contentLayout.Controls.Add(listHost, 0, 0);
        contentLayout.Controls.Add(_footerLayout, 0, 1);

        _pageLayout.SetContent(contentLayout);

        _listController = new HiveCrudPageListController<TItem>(
            _list,
            _pagination,
            _statusLabel,
            _emptyStateLabel,
            _statusFilterBox,
            () => _layoutController!.ApplyStatusColor(),
            UpdateActionState,
            () => _operationController?.IsBusy == true);

        _operationController = new HiveCrudPageOperationController(
            this,
            _list,
            _searchBox,
            _statusFilterBox,
            _pagination,
            UpdateActionState,
            (text, tone) => _listController.SetStatus(text, tone),
            () => _layoutController!.ThemeManager(),
            this);

        _layoutController = new HiveCrudPageLayoutController(
            this,
            _pageLayout,
            _titleLabel,
            _descriptionLabel,
            _searchLabel,
            _searchBox,
            _statusFilterLabel,
            _statusFilterBox,
            _actionLayout,
            _searchPanel,
            _actionButtons,
            _footerLayout,
            _statusLabel,
            _emptyStateLabel,
            _addButton,
            _editButton,
            _activateButton,
            _deleteButton,
            _refreshButton,
            _pagination,
            titleFont,
            descriptionFont,
            searchLabelFont,
            emptyStateFont,
            () => _listController.StatusTone);

        _searchBox.TextChanged += SearchBoxOnTextChanged;
        _statusFilterBox.SelectedIndexChanged += StatusFilterBoxOnSelectedIndexChanged;

        Controls.Add(_pageLayout);

        _getItemDisplayName = item => item?.ToString() ?? "item";
        _pagination.PageNumber = 1;
        _listController.SetItems(Array.Empty<TItem>());
        _layoutController!.UpdateToolbarLayout();
        _layoutController!.UpdateFooterLayout();
        UpdateActionState();
        _layoutController!.ApplyThemeTypography();
    }

    public event EventHandler<HiveCrudOperationFailedEventArgs>? OperationFailed
    {
        add => _operationController.OperationFailed += value;
        remove => _operationController.OperationFailed -= value;
    }

    public HiveListPageLayout PageLayout => _pageLayout;
    public ListView ListView => _list;
    public Panel HeaderPanel => _pageLayout.HeaderPanel;
    public Panel ActionBarPanel => _pageLayout.ActionBarPanel;
    public Label StatusLabel => _statusLabel;
    public TextBox SearchBox => _searchBox;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBusy => _operationController.IsBusy;

    public IReadOnlyList<TItem> Items => _listController.Items;
    public TItem? SelectedItem => _listController.SelectedItem;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowAdd
    {
        get => _addButton.Visible;
        set { _addButton.Visible = value; _layoutController!.UpdateToolbarLayout(); UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowEdit
    {
        get => _editButton.Visible;
        set { _editButton.Visible = value; _layoutController!.UpdateToolbarLayout(); UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowDelete
    {
        get => _deleteButton.Visible;
        set { _deleteButton.Visible = value; _layoutController!.UpdateToolbarLayout(); UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowRefresh
    {
        get => _refreshButton.Visible;
        set { _refreshButton.Visible = value; _layoutController!.UpdateToolbarLayout(); UpdateActionState(); }
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
            _layoutController!.UpdateToolbarLayout();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SearchText
    {
        get => _listController.SearchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_listController.SearchText, normalized, StringComparison.Ordinal))
                return;
            _listController.SearchText = normalized;
            if (!string.Equals(_searchBox.Text, normalized, StringComparison.Ordinal))
                _searchBox.Text = normalized;
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
        get => _listController.PageSize;
        set => _listController.PageSize = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageNumber
    {
        get => _listController.PageNumber;
        set => _listController.PageNumber = value;
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
            _layoutController!.UpdateFooterLayout();
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

    public IReadOnlyList<HiveCrudColumn<TItem>> Columns => _listController.Columns;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<CancellationToken, Task<IReadOnlyList<TItem>>>? LoadItemsAsync
    {
        get => _loadItemsAsync;
        set { _loadItemsAsync = value; UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem?, CancellationToken, Task<TItem?>>? EditItemAsync
    {
        get => _editItemAsync;
        set { _editItemAsync = value; UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, CancellationToken, Task>? DeleteItemAsync
    {
        get => _deleteItemAsync;
        set { _deleteItemAsync = value; UpdateActionState(); }
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
        set { _canEditItem = value; UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, bool>? CanDeleteItem
    {
        get => _canDeleteItem;
        set { _canDeleteItem = value; UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, bool>? CanActivateItem
    {
        get => _canActivateItem;
        set { _canActivateItem = value; UpdateActionState(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, CancellationToken, Task>? ActivateItemAsync
    {
        get => _activateItemAsync;
        set
        {
            _activateItemAsync = value;
            _activateButton.Visible = value is not null;
            _layoutController!.UpdateToolbarLayout();
            UpdateActionState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TItem, string?>? StatusSelector
    {
        get => _listController.StatusSelector;
        set
        {
            _statusFilterLabel.Visible = value is not null;
            _statusFilterBox.Visible = value is not null;
            _listController.StatusSelector = value;
            _layoutController!.UpdateSearchBoxWidth();
        }
    }

    public void SetColumns(IReadOnlyList<HiveCrudColumn<TItem>> columns) =>
        _listController.SetColumns(columns);

    public void SetColumns(params HiveCrudColumn<TItem>[] columns) =>
        SetColumns((IReadOnlyList<HiveCrudColumn<TItem>>)columns);

    public void SetStatus(string text) =>
        SetStatus(text, HiveStatusTone.Neutral);

    public void SetStatus(string text, HiveStatusTone tone) =>
        _listController.SetStatus(text, tone);

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_loadItemsAsync is null)
            throw new InvalidOperationException(
                "LoadItemsAsync must be configured before refreshing the CRUD page.");
        if (_operationController.IsBusy)
            return Task.CompletedTask;
        return _operationController.ExecuteAsync(
            HiveCrudOperation.Load,
            LoadItemsCoreAsync,
            cancellationToken);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _layoutController!.UpdateToolbarLayout();
    }


    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        _layoutController!.UpdateThemeSubscription();
        _layoutController!.ApplyThemeTypography();
        _layoutController!.ApplyStatusColor();
    }


    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        _layoutController!.UpdateThemeSubscription();
        _layoutController!.ApplyThemeTypography();
        _layoutController!.ApplyStatusColor();
    }


    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _layoutController?.OwnerFontChanged(this, e);
    }

    private void StatusFilterBoxOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        _listController.StatusFilterBoxOnSelectedIndexChanged();
    }

    private void SearchBoxOnTextChanged(object? sender, EventArgs e)
    {
        _listController.SearchText = _searchBox.Text;
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
        if (_operationController.IsBusy)
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
        if (_editItemAsync is null || _operationController.IsBusy)
            return;

        if (item is null && !_addButton.Visible)
            return;

        if (item is not null &&
            _canEditItem is not null &&
            !_canEditItem(item))
            return;

        var scrollState = CaptureScrollState();

        await _operationController.ExecuteAsync(
            HiveCrudOperation.Edit,
            async token =>
            {
                SetStatus(
                    item is null ? "Adding..." : "Editing...",
                    HiveStatusTone.Information);
                var result = await _editItemAsync(item, token);
                if (result is null)
                {
                    _listController.RefreshView();
                    return;
                }

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
            _operationController.IsBusy)
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
                SetStatus("Deleting...", HiveStatusTone.Information);
                await _deleteItemAsync(item, token);
                await LoadItemsCoreAsync(token);
            },
            CancellationToken.None);
    }


    private async Task ActivateAsync(TItem? item)
    {
        if (item is null ||
            _activateItemAsync is null ||
            _operationController.IsBusy)
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
                SetStatus("Activating...", HiveStatusTone.Information);
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

        SetStatus("Loading...", HiveStatusTone.Information);
        var items = await _loadItemsAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested ||
            IsDisposed ||
            Disposing)
            return;

        _listController.SetItems(items);
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

        _addButton.Enabled = !_operationController.IsBusy && _editItemAsync is not null;
        _editButton.Enabled = !_operationController.IsBusy && canEdit && _editItemAsync is not null;
        _activateButton.Enabled = !_operationController.IsBusy && canActivate && _activateItemAsync is not null;
        _deleteButton.Enabled = !_operationController.IsBusy && canDelete && _deleteItemAsync is not null;
        _refreshButton.Enabled = !_operationController.IsBusy && _loadItemsAsync is not null;
    }


    private static HiveButton CreateActionButton(
        string text,
        HiveButtonStyle style) =>
        new()
        {
            Text = text,
            Style = style,
            Width = HiveCrudPageLayoutController.ActionButtonWidth,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };


    private void ChangePage(int delta) => _listController.ChangePage(delta);

    private IHiveThemeManager? ThemeManager() => _layoutController!.ThemeManager();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationController.Dispose();
            _layoutController?.Dispose();
        }
        base.Dispose(disposing);
    }

}