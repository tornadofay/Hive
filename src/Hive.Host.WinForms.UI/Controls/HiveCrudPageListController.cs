using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveCrudPageListController<TItem>
    where TItem : class
{
    private const string AllStatusFilter = "All";
    private const int DefaultPageSize = 25;

    private readonly ListView _list;
    private readonly HivePaginationBar _pagination;
    private readonly Label _statusLabel;
    private readonly Label _emptyStateLabel;
    private readonly ComboBox _statusFilterBox;
    private readonly Action _applyStatusColor;
    private readonly Action _updateActionState;
    private readonly Func<bool> _isBusy;
    private readonly List<HiveCrudColumn<TItem>> _columns = new();

    private IReadOnlyList<TItem> _items = Array.Empty<TItem>();
    private Func<TItem, string?>? _statusSelector;
    private bool _updatingStatusFilter;
    private string _searchText = string.Empty;
    private int _pageSize = DefaultPageSize;
    private HiveStatusTone _statusTone = HiveStatusTone.Neutral;

    internal HiveCrudPageListController(
        ListView list,
        HivePaginationBar pagination,
        Label statusLabel,
        Label emptyStateLabel,
        ComboBox statusFilterBox,
        Action applyStatusColor,
        Action updateActionState,
        Func<bool> isBusy)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _pagination = pagination ?? throw new ArgumentNullException(nameof(pagination));
        _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
        _emptyStateLabel = emptyStateLabel ?? throw new ArgumentNullException(nameof(emptyStateLabel));
        _statusFilterBox = statusFilterBox ?? throw new ArgumentNullException(nameof(statusFilterBox));
        _applyStatusColor = applyStatusColor ?? throw new ArgumentNullException(nameof(applyStatusColor));
        _updateActionState = updateActionState ?? throw new ArgumentNullException(nameof(updateActionState));
        _isBusy = isBusy ?? throw new ArgumentNullException(nameof(isBusy));
    }

    internal IReadOnlyList<TItem> Items => _items;
    internal TItem? SelectedItem => _list.SelectedItems.Count == 0 ? default : (TItem?)_list.SelectedItems[0].Tag;
    internal IReadOnlyList<HiveCrudColumn<TItem>> Columns => _columns;

    internal string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
                return;
            _searchText = normalized;
            _pagination.PageNumber = 1;
            RebuildItems();
        }
    }

    internal int PageSize
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

    internal int PageNumber
    {
        get => _pagination.PageNumber;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _pagination.PageNumber = value;
            RebuildItems();
        }
    }

    internal HiveStatusTone StatusTone => _statusTone;

    internal Func<TItem, string?>? StatusSelector
    {
        get => _statusSelector;
        set => SetStatusSelector(value);
    }

    internal void SetStatusSelector(Func<TItem, string?>? statusSelector)
    {
        _statusSelector = statusSelector;
        if (statusSelector is not null)
            RebuildStatusFilterOptions();
        else
        {
            _statusFilterBox.Items.Clear();
            _statusFilterBox.SelectedItem = null;
        }
        RebuildItems();
    }

    internal void SetItems(IReadOnlyList<TItem> items)
    {
        _items = items ?? throw new InvalidOperationException("LoadItemsAsync returned null.");
        RebuildStatusFilterOptions();
        RebuildItems();
    }

    internal void SetColumns(IReadOnlyList<HiveCrudColumn<TItem>> columns)
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

    internal void SetStatus(string text, HiveStatusTone tone)
    {
        _statusTone = tone;
        _statusLabel.Text = text ?? string.Empty;
        _applyStatusColor();
    }

    internal void StatusFilterBoxOnSelectedIndexChanged()
    {
        if (_updatingStatusFilter)
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

            _statusTone = HiveStatusTone.Neutral;
            _statusLabel.Text = BuildStatusText(
                matchingCount,
                visibleCount,
                _pagination.PageNumber,
                _pageSize);
            _applyStatusColor();
            UpdateEmptyState(matchingCount);
        }
        finally
        {
            _list.EndUpdate();
        }

        _updateActionState();
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

    internal void RefreshView() => RebuildItems();

    internal void ChangePage(int delta)
    {
        if (_isBusy() || delta == 0)
            return;
        var nextPage = _pagination.PageNumber + delta;
        if (nextPage < 1)
            return;
        _pagination.PageNumber = nextPage;
        RebuildItems();
    }
}