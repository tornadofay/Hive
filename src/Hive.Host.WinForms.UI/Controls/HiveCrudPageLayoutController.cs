using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveCrudPageLayoutController : IDisposable
{
    internal const int HeaderHeight = 64;
    internal const int ActionBarHeight = 46;
    internal const int FooterHeight = 42;
    internal const int ActionButtonWidth = 92;
    internal const int CompactActionButtonWidth = 84;
    internal const int ActionButtonSpacing = 8;
    internal const int InitialActionBarActionsWidth = (ActionButtonWidth + ActionButtonSpacing) * 4;
    internal const int PaginationWidth = 276;
    internal const int MinimumSearchWidth = 180;
    internal const int MaximumSearchWidth = 420;

    private readonly Control _owner;
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
    private readonly Label _emptyStateLabel;
    private readonly HiveButton _addButton;
    private readonly HiveButton _editButton;
    private readonly HiveButton _activateButton;
    private readonly HiveButton _deleteButton;
    private readonly HiveButton _refreshButton;
    private readonly HivePaginationBar _pagination;
    private readonly Func<HiveStatusTone> _statusToneProvider;
    private Font _titleFont;
    private Font _descriptionFont;
    private Font _searchLabelFont;
    private Font _emptyStateFont;
    private IHiveThemeManager? _subscribedThemeManager;
    private bool _typographyReady = true;
    private bool _compactToolbar;

    internal HiveCrudPageLayoutController(
        Control owner,
        HiveListPageLayout pageLayout,
        Label titleLabel,
        Label descriptionLabel,
        Label searchLabel,
        TextBox searchBox,
        Label statusFilterLabel,
        ComboBox statusFilterBox,
        TableLayoutPanel actionLayout,
        FlowLayoutPanel searchPanel,
        FlowLayoutPanel actionButtons,
        TableLayoutPanel footerLayout,
        Label statusLabel,
        Label emptyStateLabel,
        HiveButton addButton,
        HiveButton editButton,
        HiveButton activateButton,
        HiveButton deleteButton,
        HiveButton refreshButton,
        HivePaginationBar pagination,
        Font titleFont,
        Font descriptionFont,
        Font searchLabelFont,
        Font emptyStateFont,
        Func<HiveStatusTone> statusToneProvider)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _pageLayout = pageLayout ?? throw new ArgumentNullException(nameof(pageLayout));
        _titleLabel = titleLabel ?? throw new ArgumentNullException(nameof(titleLabel));
        _descriptionLabel = descriptionLabel ?? throw new ArgumentNullException(nameof(descriptionLabel));
        _searchLabel = searchLabel ?? throw new ArgumentNullException(nameof(searchLabel));
        _searchBox = searchBox ?? throw new ArgumentNullException(nameof(searchBox));
        _statusFilterLabel = statusFilterLabel ?? throw new ArgumentNullException(nameof(statusFilterLabel));
        _statusFilterBox = statusFilterBox ?? throw new ArgumentNullException(nameof(statusFilterBox));
        _actionLayout = actionLayout ?? throw new ArgumentNullException(nameof(actionLayout));
        _searchPanel = searchPanel ?? throw new ArgumentNullException(nameof(searchPanel));
        _actionButtons = actionButtons ?? throw new ArgumentNullException(nameof(actionButtons));
        _footerLayout = footerLayout ?? throw new ArgumentNullException(nameof(footerLayout));
        _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
        _emptyStateLabel = emptyStateLabel ?? throw new ArgumentNullException(nameof(emptyStateLabel));
        _addButton = addButton ?? throw new ArgumentNullException(nameof(addButton));
        _editButton = editButton ?? throw new ArgumentNullException(nameof(editButton));
        _activateButton = activateButton ?? throw new ArgumentNullException(nameof(activateButton));
        _deleteButton = deleteButton ?? throw new ArgumentNullException(nameof(deleteButton));
        _refreshButton = refreshButton ?? throw new ArgumentNullException(nameof(refreshButton));
        _pagination = pagination ?? throw new ArgumentNullException(nameof(pagination));
        _statusToneProvider = statusToneProvider ?? throw new ArgumentNullException(nameof(statusToneProvider));
        _titleFont = titleFont ?? throw new ArgumentNullException(nameof(titleFont));
        _descriptionFont = descriptionFont ?? throw new ArgumentNullException(nameof(descriptionFont));
        _searchLabelFont = searchLabelFont ?? throw new ArgumentNullException(nameof(searchLabelFont));
        _emptyStateFont = emptyStateFont ?? throw new ArgumentNullException(nameof(emptyStateFont));
        _owner.FontChanged += OwnerFontChanged;
    }

    internal void UpdateThemeSubscription()
    {
        var nextManager = _owner.FindForm() is HiveForm hiveForm
            ? hiveForm.ThemeManager
            : null;

        if (ReferenceEquals(_subscribedThemeManager, nextManager))
            return;

        if (_subscribedThemeManager is not null)
            _subscribedThemeManager.ThemeChanged -= ThemeManagerOnChanged;

        _subscribedThemeManager = nextManager;

        if (_subscribedThemeManager is not null)
            _subscribedThemeManager.ThemeChanged += ThemeManagerOnChanged;
    }

    internal void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyThemeTypography();
        ApplyStatusColor();
    }

    internal void ApplyStatusColor()
    {
        if (_owner.FindForm() is not HiveForm hiveForm)
            return;

        _statusLabel.ForeColor = _statusToneProvider() switch
        {
            HiveStatusTone.Information => hiveForm.Theme.VisualStates.Information,
            HiveStatusTone.Success => hiveForm.Theme.VisualStates.Success,
            HiveStatusTone.Warning => hiveForm.Theme.VisualStates.Warning,
            HiveStatusTone.Error => hiveForm.Theme.VisualStates.Error,
            _ => hiveForm.Theme.Palette.MutedText
        };
    }

    internal void ApplyThemeTypography()
    {
        if (!_typographyReady ||
            _owner.FindForm() is not HiveForm hiveForm)
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

    internal static Font? ReplaceFontIfNeeded(
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

    internal void UpdateFooterLayout()
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

    internal void UpdateToolbarLayout()
    {
        var compact = _owner.ClientSize.Width > 0 &&
                      _owner.ClientSize.Width < GetWideToolbarMinimumWidth();

        // Status filtering remains a secondary filter. In compact mode the
        // search label disappears, so keep the status filter aligned with the
        // search field instead of retaining the wide-layout leading offset.
        _statusFilterLabel.Margin = new Padding(
            compact ? 0 : 16,
            0,
            6,
            0);

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

    internal int GetCompactActionRowCount()
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
            _owner.ClientSize.Width -
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

    internal void UpdateSearchBoxWidth()
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

    internal int GetWideToolbarMinimumWidth()
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

    internal int GetVisibleActionBarWidth(bool compact)
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

    internal void OwnerFontChanged(object? sender, EventArgs e) => ApplyThemeTypography();

    internal IHiveThemeManager? ThemeManager()
    {
        return _owner.FindForm() switch
        {
            HiveForm hiveForm => hiveForm.ThemeManager,
            _ => null
        };
    }

    public void Dispose()
    {
        _owner.FontChanged -= OwnerFontChanged;
        if (_subscribedThemeManager is not null)
            _subscribedThemeManager.ThemeChanged -= ThemeManagerOnChanged;
        _titleFont.Dispose();
        _descriptionFont.Dispose();
        _searchLabelFont.Dispose();
        _emptyStateFont.Dispose();
    }
}