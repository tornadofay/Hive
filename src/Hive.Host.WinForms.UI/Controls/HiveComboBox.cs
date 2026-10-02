using Hive.Host.WinForms.UI.Theme;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveComboBox : UserControl, IHiveWinFormsFieldControl
{
    private const int DefaultFieldHeight = 34;
    private const int DefaultDropDownWidth = 280;
    private const int DefaultMaxDropDownItems = 8;
    private const int DefaultItemHeight = 34;
    private const int ArrowAreaWidth = 30;

    private readonly HostTextBox _fieldEditor;
    private readonly BindingSource _bindingSource;
    private readonly HiveComboBoxItemCollection _items;
    private readonly List<HiveComboBoxItem> _filteredItems = new();

    private object? _dataSource;
    private int _selectedIndex = -1;
    private string _displayMember = string.Empty;
    private string _valueMember = string.Empty;
    private ComboBoxStyle _dropDownStyle = ComboBoxStyle.DropDown;
    private bool _readOnly;
    private int _dropDownWidth = DefaultDropDownWidth;
    private int _maxDropDownItems = DefaultMaxDropDownItems;
    private int _itemHeight = DefaultItemHeight;
    private bool _synchronizingFieldText;
    private bool _synchronizingSelection;
    private bool _suppressBaseTextChanged;
    private object? _selectedItemIdentity;
    private bool _hasSelectedItemIdentity;
    private HiveThemeDefinition? _theme;
    private HiveComboBoxPopupForm? _popup;
    private SolidBrush? _backgroundBrush;
    private SolidBrush? _disabledBrush;
    private Pen? _borderPen;
    private Pen? _hoverBorderPen;
    private Pen? _focusPen;
    private bool _hovered;

    public HiveComboBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        AutoScaleMode = AutoScaleMode.Dpi;
        Margin = Padding.Empty;
        Padding = new Padding(1);
        TabStop = true;
        Cursor = Cursors.Default;
        AccessibleRole = AccessibleRole.ComboBox;
        AccessibleName = "Combo box";
        AccessibleDescription = "Select a value from the available options.";

        HiveIntegration = new HiveWinFormsControlMetadata();
        HiveField = new HiveWinFormsFieldMetadata();

        _items = new HiveComboBoxItemCollection(this);
        _bindingSource = new BindingSource();
        _bindingSource.ListChanged += BindingSourceOnListChanged;
        _bindingSource.CurrentChanged += BindingSourceOnCurrentChanged;

        _fieldEditor = new HostTextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.None,
            Margin = Padding.Empty,
            Padding = new Padding(8, 0, ArrowAreaWidth, 0),
            TabStop = false,
            AutoSize = false,
            TextAlign = HorizontalAlignment.Left
        };

        _fieldEditor.MouseDown += FieldEditorOnMouseDown;
        _fieldEditor.KeyDown += FieldEditorOnKeyDown;
        _fieldEditor.TextChanged += FieldEditorOnTextChanged;
        _fieldEditor.GotFocus += FieldEditorOnFocusChanged;
        _fieldEditor.LostFocus += FieldEditorOnFocusChanged;

        Controls.Add(_fieldEditor);

        // Size changes can raise OnResize during UserControl construction.
        // Establish the child editor before assigning the initial bounds.
        MinimumSize = new Size(0, DefaultFieldHeight);
        Size = new Size(240, DefaultFieldHeight);

        UpdateFieldEditorState();
        RebuildPaintResources();
        UpdateFieldLayout();
    }

    public HiveWinFormsControlMetadata HiveIntegration { get; }

    public HiveWinFormsFieldMetadata HiveField { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public HiveComboBoxItemCollection Items => _items;

    [DefaultValue(null)]
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (ReferenceEquals(_dataSource, value))
                return;

            var previousSelected = CaptureSelectedItem();
            var hadSelection = HasSelection;

            _synchronizingSelection = true;
            try
            {
                _dataSource = value;
                _bindingSource.DataSource = value;
            }
            finally
            {
                _synchronizingSelection = false;
            }

            RefreshSelectionAfterSourceChange(previousSelected, hadSelection);
            RefreshPopup();
        }
    }

    [DefaultValue("")]
    public string DisplayMember
    {
        get => _displayMember;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_displayMember, value, StringComparison.Ordinal))
                return;

            var selectedValueBefore = SelectedValue;
            _displayMember = value;

            UpdateDisplayedText();
            RefreshPopup();
            Invalidate();

            if (!Equals(selectedValueBefore, SelectedValue))
                SelectedValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue("")]
    public string ValueMember
    {
        get => _valueMember;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_valueMember, value, StringComparison.Ordinal))
                return;

            var previousValue = SelectedValue;
            _valueMember = value;
            RefreshPopup();

            if (!Equals(previousValue, SelectedValue))
                SelectedValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue(-1)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= SourceCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "SelectedIndex must be -1 or refer to an existing item.");
            }

            SetSelectedIndexCore(value, updateDataSourcePosition: true, userCommit: false);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => _selectedIndex >= 0
            ? GetSourceItem(_selectedIndex)
            : null;
        set => SetSelectedItem(value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedValue
    {
        get
        {
            var item = SelectedItem;
            if (item is null)
                return null;

            if (string.IsNullOrWhiteSpace(_valueMember))
                return item;

            return GetMemberValue(item, _valueMember);
        }
        set
        {
            if (value is null)
            {
                SelectedIndex = -1;
                return;
            }

            for (var index = 0; index < SourceCount; index++)
            {
                var item = GetSourceItem(index);
                var candidate = string.IsNullOrWhiteSpace(_valueMember)
                    ? item
                    : GetMemberValue(item, _valueMember);

                if (Equals(candidate, value))
                {
                    SelectedIndex = index;
                    return;
                }
            }

            SelectedIndex = -1;
        }
    }

    [AllowNull]
    public override string Text
    {
        get => _fieldEditor.Text;
        set
        {
            value ??= string.Empty;

            if (string.Equals(_fieldEditor.Text, value, StringComparison.Ordinal))
                return;

            _suppressBaseTextChanged = true;
            try
            {
                base.Text = value;
            }
            finally
            {
                _suppressBaseTextChanged = false;
            }

            _synchronizingFieldText = true;
            try
            {
                _fieldEditor.Text = value;
            }
            finally
            {
                _synchronizingFieldText = false;
            }

            OnTextChanged(EventArgs.Empty);
        }
    }

    [DefaultValue(ComboBoxStyle.DropDown)]
    public ComboBoxStyle DropDownStyle
    {
        get => _dropDownStyle;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new InvalidEnumArgumentException(
                    nameof(value),
                    (int)value,
                    typeof(ComboBoxStyle));
            }

            if (_dropDownStyle == value)
                return;

            _dropDownStyle = value;
            UpdateFieldEditorState();
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value)
                return;

            _readOnly = value;
            UpdateFieldEditorState();
            Invalidate();
        }
    }

    [DefaultValue(DefaultDropDownWidth)]
    public int DropDownWidth
    {
        get => _dropDownWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _dropDownWidth = value;
        }
    }

    [DefaultValue(DefaultMaxDropDownItems)]
    public int MaxDropDownItems
    {
        get => _maxDropDownItems;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxDropDownItems = value;
        }
    }

    [DefaultValue(DefaultItemHeight)]
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_itemHeight == value)
                return;

            _itemHeight = value;
            RefreshPopup();
        }
    }

    [Browsable(false)]
    public bool DroppedDown => _popup is not null && !_popup.IsDisposed;

    public event EventHandler? SelectedIndexChanged;

    public event EventHandler? SelectedItemChanged;

    public event EventHandler? SelectedValueChanged;

    public event EventHandler? SelectionChangeCommitted;

    public event EventHandler? DropDownOpened;

    public event EventHandler? DropDownClosed;

    public void ShowDropDown()
    {
        if (!Enabled || DroppedDown)
            return;

        if (SourceCount == 0)
            _filteredItems.Clear();
        else
            BuildFilteredItems(
                InitialPopupFilter());

        var popup = new HiveComboBoxPopupForm(
            this,
            _theme);

        _popup = popup;
        popup.FormClosed += PopupOnFormClosed;

        popup.SetItems(
            _filteredItems,
            _selectedIndex);

        popup.SetFilterText(
            InitialPopupFilter());

        popup.ShowPopup();
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    public void HideDropDown()
    {
        ClosePopup(restoreFocus: true);
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;
        RebuildPaintResources();

        _fieldEditor.BackColor =
            Enabled
                ? theme.Palette.InputBackground
                : theme.Palette.DisabledBackground;
        _fieldEditor.ForeColor =
            Enabled
                ? theme.Palette.Text
                : theme.Palette.DisabledText;

        _popup?.ApplyTheme(theme);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateFieldLayout();

        if (DroppedDown)
            _popup!.Reposition();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _fieldEditor.Font = Font;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);

        UpdateFieldEditorState();

        if (!Enabled)
            ClosePopup(restoreFocus: false);

        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);

        if (_fieldEditor.CanFocus)
            _fieldEditor.Focus();

        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);

        if (!Enabled)
            return;

        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_popup is not null || _fieldEditor.Focused)
            return;

        _hovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        FocusField();

        if (IsArrowArea(e.Location.X) ||
            _dropDownStyle == ComboBoxStyle.DropDownList)
        {
            ToggleDropDown();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        HandleFieldKey(e);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = Math.Max(
            LogicalToDevice(DefaultFieldHeight),
            _fieldEditor.GetPreferredSize(Size.Empty).Height + LogicalToDevice(2));

        return new Size(
            proposedSize.Width > 0
                ? proposedSize.Width
                : LogicalToDevice(240),
            height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var bounds = ClientRectangle;
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        var theme = _theme;

        if (theme is null)
        {
            e.Graphics.FillRectangle(
                SystemBrushes.Window,
                bounds);
            using var border = new Pen(SystemColors.WindowFrame);
            e.Graphics.DrawRectangle(
                border,
                bounds.Left,
                bounds.Top,
                bounds.Width - 1,
                bounds.Height - 1);
        }
        else
        {
            var brush = Enabled
                ? _backgroundBrush
                : _disabledBrush;

            if (brush is not null)
                e.Graphics.FillRectangle(brush, bounds);

            var border = ContainsFocus || DroppedDown
                ? _focusPen
                : _hovered
                    ? _hoverBorderPen
                    : _borderPen;

            if (border is not null)
            {
                e.Graphics.DrawRectangle(
                    border,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width - 1,
                    bounds.Height - 1);
            }
        }

        var arrowX = bounds.Right - LogicalToDevice(ArrowAreaWidth);
        var centerY = bounds.Top + bounds.Height / 2;
        var halfWidth = LogicalToDevice(5);
        var halfHeight = LogicalToDevice(3);

        using var arrowBrush =
            new SolidBrush(
                Enabled
                    ? _theme?.Palette.MutedText ?? SystemColors.ControlText
                    : _theme?.Palette.DisabledText ?? SystemColors.GrayText);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPolygon(
            arrowBrush,
            [
                new Point(arrowX + halfWidth, centerY - halfHeight),
                new Point(arrowX + LogicalToDevice(ArrowAreaWidth) - halfWidth, centerY - halfHeight),
                new Point(
                    arrowX + LogicalToDevice(ArrowAreaWidth) / 2,
                    centerY + halfHeight)
            ]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClosePopup(restoreFocus: false);

            _bindingSource.ListChanged -= BindingSourceOnListChanged;
            _bindingSource.CurrentChanged -= BindingSourceOnCurrentChanged;
            _bindingSource.Dispose();

            _fieldEditor.MouseDown -= FieldEditorOnMouseDown;
            _fieldEditor.KeyDown -= FieldEditorOnKeyDown;
            _fieldEditor.TextChanged -= FieldEditorOnTextChanged;
            _fieldEditor.GotFocus -= FieldEditorOnFocusChanged;
            _fieldEditor.LostFocus -= FieldEditorOnFocusChanged;

            DisposePaintResources();
        }

        base.Dispose(disposing);
    }

    private int SourceCount =>
        _dataSource is null
            ? _items.CountInternal
            : _bindingSource.Count;

    private object? GetSourceItem(int index) =>
        _dataSource is null
            ? _items[index]
            : _bindingSource[index];

    private object? CaptureSelectedItem() =>
        _selectedIndex >= 0 && _selectedIndex < SourceCount
            ? GetSourceItem(_selectedIndex)
            : null;

    private bool HasSelection => _selectedIndex >= 0 && _selectedIndex < SourceCount;

    private void SetSelectedItem(object? item)
    {
        if (item is null)
        {
            SelectedIndex = -1;
            return;
        }

        for (var index = 0; index < SourceCount; index++)
        {
            var candidate = GetSourceItem(index);
            if (ReferenceEquals(candidate, item) ||
                Equals(candidate, item))
            {
                SelectedIndex = index;
                return;
            }
        }

        SelectedIndex = -1;
    }

    private void BindingSourceOnListChanged(object? sender, ListChangedEventArgs e)
    {
        if (_synchronizingSelection)
            return;

        var previousSelected = _hasSelectedItemIdentity
            ? _selectedItemIdentity
            : CaptureSelectedItem();
        var hadSelection = _hasSelectedItemIdentity || HasSelection;
        RefreshSelectionAfterSourceChange(previousSelected, hadSelection);
    }

    private void BindingSourceOnCurrentChanged(object? sender, EventArgs e)
    {
        if (_synchronizingSelection || _dataSource is null)
            return;

        var position = _bindingSource.Position;
        if (position < -1 || position >= SourceCount)
            position = -1;

        SetSelectedIndexCore(
            position,
            updateDataSourcePosition: false,
            userCommit: false);
    }

    private void SetSelectedIndexCore(
        int value,
        bool updateDataSourcePosition,
        bool userCommit)
    {
        if (_selectedIndex == value)
            return;

        var oldItem = CaptureSelectedItem();
        var oldValue = GetValueForItem(oldItem);
        _selectedIndex = value;

        if (updateDataSourcePosition &&
            _dataSource is not null &&
            !_synchronizingSelection &&
            value >= 0 &&
            value < _bindingSource.Count)
        {
            _synchronizingSelection = true;
            try
            {
                _bindingSource.Position = value;
            }
            finally
            {
                _synchronizingSelection = false;
            }
        }

        UpdateDisplayedText();
        RefreshPopup();

        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

        if (!Equals(oldItem, SelectedItem))
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);

        if (!Equals(oldValue, SelectedValue))
            SelectedValueChanged?.Invoke(this, EventArgs.Empty);

        if (userCommit)
            SelectionChangeCommitted?.Invoke(this, EventArgs.Empty);

        _selectedItemIdentity = SelectedItem;
        _hasSelectedItemIdentity = _selectedIndex >= 0;
    }

    private object? GetValueForItem(object? item)
    {
        if (item is null)
            return null;

        return string.IsNullOrWhiteSpace(_valueMember)
            ? item
            : GetMemberValue(item, _valueMember);
    }

    private void UpdateDisplayedText()
    {
        Text = HasSelection
            ? GetDisplayText(SelectedItem)
            : string.Empty;

        AccessibleName =
            string.IsNullOrWhiteSpace(Name)
                ? "Combo box"
                : Name;
    }

    private void RefreshSelectionAfterSourceChange(object? previousSelected, bool hadSelection)
    {
        var previousIndex = _selectedIndex;
        var previousItem = hadSelection ? previousSelected : null;
        var previousValue = GetValueForItem(previousItem);

        _selectedIndex = hadSelection
            ? FindSourceIndex(previousSelected)
            : -1;

        if (_selectedIndex >= 0 && _selectedIndex >= SourceCount)
            _selectedIndex = -1;

        var newItem = SelectedItem;
        var newValue = SelectedValue;

        UpdateDisplayedText();

        if (_selectedIndex != previousIndex)
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

        if (!Equals(previousItem, newItem))
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);

        if (!Equals(previousValue, newValue))
            SelectedValueChanged?.Invoke(this, EventArgs.Empty);

        _selectedItemIdentity = newItem;
        _hasSelectedItemIdentity = _selectedIndex >= 0;

        if (_dataSource is not null &&
            _selectedIndex >= 0 &&
            _selectedIndex < _bindingSource.Count)
        {
            _synchronizingSelection = true;
            try
            {
                _bindingSource.Position = _selectedIndex;
            }
            finally
            {
                _synchronizingSelection = false;
            }
        }

        RefreshPopup();
    }

    private int FindSourceIndex(object? item)
    {
        for (var index = 0; index < SourceCount; index++)
        {
            var candidate = GetSourceItem(index);
            if (ReferenceEquals(candidate, item) ||
                Equals(candidate, item))
                return index;
        }

        return -1;
    }

    private void BuildFilteredItems(string filter)
    {
        _filteredItems.Clear();

        for (var index = 0; index < SourceCount; index++)
        {
            var item = GetSourceItem(index);
            var display = GetDisplayText(item);

            if (string.IsNullOrEmpty(filter) ||
                display.IndexOf(
                    filter,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _filteredItems.Add(
                    new HiveComboBoxItem(
                        index,
                        item,
                        display));
            }
        }
    }

    private string InitialPopupFilter()
    {
        if (_dropDownStyle == ComboBoxStyle.DropDownList)
            return string.Empty;

        var currentText = _fieldEditor.Text;
        var selectedText = HasSelection
            ? GetDisplayText(SelectedItem)
            : string.Empty;

        return string.Equals(
            currentText,
            selectedText,
            StringComparison.Ordinal)
            ? string.Empty
            : currentText;
    }

    private void RefreshPopup(string? filter = null)
    {
        if (_popup is null || _popup.IsDisposed)
            return;

        var query = filter ?? _popup.FilterText;
        BuildFilteredItems(query);
        _popup.SetItems(_filteredItems, _selectedIndex);

        if (!string.Equals(_popup.FilterText, query, StringComparison.Ordinal))
            _popup.SetFilterText(query);
    }

    private void SetPopupFilter(string filter)
    {
        RefreshPopup(filter);
    }

    private void SourceCollectionChanged(object? previousSelected, bool hadSelection)
    {
        RefreshSelectionAfterSourceChange(previousSelected, hadSelection);
    }

    private void ToggleDropDown()
    {
        if (DroppedDown)
            HideDropDown();
        else
            ShowDropDown();
    }

    private void ClosePopup(bool restoreFocus)
    {
        var popup = _popup;
        if (popup is null)
            return;

        _popup = null;
        popup.FormClosed -= PopupOnFormClosed;

        try
        {
            popup.Close();
        }
        finally
        {
            if (restoreFocus)
                RestoreFieldFocus();
        }

        DropDownClosed?.Invoke(this, EventArgs.Empty);
    }

    private void PopupOnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (!ReferenceEquals(_popup, sender))
            return;

        _popup = null;
        DropDownClosed?.Invoke(this, EventArgs.Empty);
        RestoreFieldFocus();
    }

    private void CommitPopupSelection(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= SourceCount)
            return;

        SetSelectedIndexCore(
            sourceIndex,
            updateDataSourcePosition: true,
            userCommit: true);

        ClosePopup(restoreFocus: true);
    }

    private void HandleFieldKey(KeyEventArgs e)
    {
        if (!Enabled)
            return;

        if (e.KeyCode == Keys.Escape && DroppedDown)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HideDropDown();
            return;
        }

        if (e.KeyCode == Keys.F4 ||
            (e.Alt && e.KeyCode == Keys.Down))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ToggleDropDown();
            return;
        }

        if (e.KeyCode is Keys.Down or Keys.Up)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (!DroppedDown)
            {
                ShowDropDown();
                return;
            }

            var delta = e.KeyCode == Keys.Down ? 1 : -1;
            _popup?.MoveHighlight(delta);
            return;
        }

        if (e.KeyCode == Keys.Enter && DroppedDown)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _popup?.CommitHighlightedSelection();
        }
    }

    private void FieldEditorOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        FocusField();

        if (_dropDownStyle == ComboBoxStyle.DropDownList)
            ToggleDropDown();
    }

    private void FieldEditorOnKeyDown(object? sender, KeyEventArgs e)
    {
        HandleFieldKey(e);
    }

    private void FieldEditorOnTextChanged(object? sender, EventArgs e)
    {
        if (_synchronizingFieldText)
            return;

        Text = _fieldEditor.Text;

        if (DroppedDown)
            RefreshPopup();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (_suppressBaseTextChanged)
            return;

        base.OnTextChanged(e);
    }

    private void FieldEditorOnFocusChanged(object? sender, EventArgs e)
    {
        Invalidate();
    }

    private void FocusField()
    {
        if (Enabled && _fieldEditor.CanFocus)
            _fieldEditor.Focus();
    }

    private void RestoreFieldFocus()
    {
        if (!IsDisposed &&
            IsHandleCreated)
        {
            try
            {
                BeginInvoke(new MethodInvoker(FocusField));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void UpdateFieldEditorState()
    {
        var editable =
            Enabled &&
            !_readOnly &&
            _dropDownStyle != ComboBoxStyle.DropDownList;

        _fieldEditor.Enabled = Enabled;
        _fieldEditor.ReadOnly = !editable;
        _fieldEditor.BackColor =
            _theme is null
                ? SystemColors.Window
                : Enabled
                    ? _theme.Palette.InputBackground
                    : _theme.Palette.DisabledBackground;
        _fieldEditor.ForeColor =
            _theme is null
                ? SystemColors.WindowText
                : Enabled
                    ? _theme.Palette.Text
                    : _theme.Palette.DisabledText;
    }

    private void UpdateFieldLayout()
    {
        if (_fieldEditor is null || _fieldEditor.IsDisposed)
            return;

        var arrowWidth = LogicalToDevice(ArrowAreaWidth);
        var fieldHeight = Math.Max(0, ClientSize.Height - 2);
        var fieldWidth = Math.Max(0, ClientSize.Width - arrowWidth - 2);

        _fieldEditor.SetBounds(
            1,
            1,
            fieldWidth,
            fieldHeight);
    }

    private bool IsArrowArea(int x) =>
        x >= Math.Max(
            0,
            ClientSize.Width - LogicalToDevice(ArrowAreaWidth));

    private string GetDisplayText(object? item)
    {
        if (item is null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(_displayMember))
            return item.ToString() ?? string.Empty;

        return Convert.ToString(
            GetMemberValue(item, _displayMember),
            System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static object? GetMemberValue(object? item, string memberName)
    {
        if (item is null)
            return null;

        if (item is IDictionary dictionary &&
            dictionary.Contains(memberName))
        {
            return dictionary[memberName];
        }

        var property = TypeDescriptor.GetProperties(item)[memberName];
        return property?.GetValue(item);
    }

    private void RebuildPaintResources()
    {
        DisposePaintResources();

        var theme = _theme;
        if (theme is null)
            return;

        _backgroundBrush = new SolidBrush(theme.Palette.InputBackground);
        _disabledBrush = new SolidBrush(theme.Palette.DisabledBackground);
        _borderPen = new Pen(theme.Palette.Border);
        _hoverBorderPen = new Pen(theme.Palette.AccentHover);
        _focusPen = new Pen(theme.VisualStates.FocusedBorder, 2f);
    }

    private void DisposePaintResources()
    {
        _backgroundBrush?.Dispose();
        _disabledBrush?.Dispose();
        _borderPen?.Dispose();
        _hoverBorderPen?.Dispose();
        _focusPen?.Dispose();

        _backgroundBrush = null;
        _disabledBrush = null;
        _borderPen = null;
        _hoverBorderPen = null;
        _focusPen = null;
    }

    private int LogicalToDevice(int value)
    {
        var dpi = DeviceDpi;
        return Math.Max(
            1,
            (int)Math.Round(
                value * dpi / 96f,
                MidpointRounding.AwayFromZero));
    }

    internal void CommitPopupSelectionForTesting(int sourceIndex) =>
        CommitPopupSelection(sourceIndex);

    internal void ProcessKeyForTesting(Keys keyData) =>
        HandleFieldKey(new KeyEventArgs(keyData));

    internal static Rectangle CalculatePopupBoundsForTesting(
        Rectangle ownerBounds,
        Rectangle workArea,
        int popupWidth,
        int popupHeight) =>
        HiveComboBoxPopupForm.CalculatePopupBounds(
            ownerBounds,
            workArea,
            popupWidth,
            popupHeight);

    internal int FilteredCountForTesting =>
        _filteredItems.Count;

    internal void ApplyFilterForTesting(string filter) =>
        BuildFilteredItems(filter ?? string.Empty);

    internal IReadOnlyList<string> FilteredDisplayValuesForTesting =>
        _filteredItems.Select(static item => item.Display).ToArray();

    internal HiveScrollState PopupVerticalScrollStateForTesting =>
        _popup?.VerticalScrollStateForTesting ?? HiveScrollState.Create(
            Orientation.Vertical,
            0,
            0,
            0,
            0,
            enabled: false);

    internal static int CalculatePopupHighlightScrollForTesting(
        int currentScroll,
        int rowTop,
        int rowHeight,
        int viewportSize) =>
        CalculatePopupHighlightScroll(
            currentScroll,
            rowTop,
            rowHeight,
            viewportSize);

    private static int CalculatePopupHighlightScroll(
        int currentScroll,
        int rowTop,
        int rowHeight,
        int viewportSize)
    {
        if (viewportSize <= 0)
            return currentScroll;

        var rowBottom = rowTop + Math.Max(1, rowHeight);

        if (rowTop < currentScroll)
            return rowTop;

        if (rowBottom > currentScroll + viewportSize)
            return rowBottom - viewportSize;

        return currentScroll;
    }

    protected override AccessibleObject CreateAccessibilityInstance() =>
        new HiveComboBoxAccessibleObject(this);

    private sealed class HiveComboBoxAccessibleObject : ControlAccessibleObject
    {
        private readonly HiveComboBox _owner;

        public HiveComboBoxAccessibleObject(HiveComboBox owner)
            : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role =>
            AccessibleRole.ComboBox;

        public override AccessibleStates State
        {
            get
            {
                var state = base.State;

                if (_owner.DroppedDown)
                    state |= AccessibleStates.Expanded;
                else
                    state |= AccessibleStates.Collapsed;

                return state;
            }
        }

        public override string? Value =>
            _owner.Text;
    }

    private readonly record struct HiveComboBoxItem(
        int SourceIndex,
        object? Item,
        string Display);

        public sealed class HiveComboBoxItemCollection : IEnumerable<object?>
    {
        private readonly HiveComboBox _owner;
        private readonly List<object?> _items = new();

        internal HiveComboBoxItemCollection(HiveComboBox owner)
        {
            _owner = owner;
        }

        internal int CountInternal =>
            _owner._dataSource is null
                ? _items.Count
                : _owner._bindingSource.Count;

        public int Count => CountInternal;

        public object? this[int index]
        {
            get
            {
                if (index < 0 || index >= CountInternal)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _owner._dataSource is null
                    ? _items[index]
                    : _owner._bindingSource[index];
            }
            set
            {
                EnsureUnbound();
                if (index < 0 || index >= _items.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var previousSelected = _owner.CaptureSelectedItem();
                var hadSelection = _owner.HasSelection;

                _items[index] = value;
                _owner.SourceCollectionChanged(previousSelected, hadSelection);
            }
        }

            public int Add(object? item)
        {
            EnsureUnbound();

            var previousSelected = _owner.CaptureSelectedItem();
            var hadSelection = _owner.HasSelection;

            _items.Add(item);
            _owner.SourceCollectionChanged(previousSelected, hadSelection);
            return _items.Count - 1;
        }

        public void AddRange(IEnumerable<object?> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            EnsureUnbound();

            var previousSelected = _owner.CaptureSelectedItem();
            var hadSelection = _owner.HasSelection;

            _items.AddRange(items);
            _owner.SourceCollectionChanged(previousSelected, hadSelection);
        }

        public void Insert(int index, object? item)
        {
            EnsureUnbound();
            var previousSelected = _owner.CaptureSelectedItem();
            var hadSelection = _owner.HasSelection;

            _items.Insert(index, item);
            _owner.SourceCollectionChanged(previousSelected, hadSelection);
        }

        public void RemoveAt(int index)
        {
            EnsureUnbound();
            var previousSelected = _owner.CaptureSelectedItem();
            var hadSelection = _owner.HasSelection;

            _items.RemoveAt(index);
            _owner.SourceCollectionChanged(previousSelected, hadSelection);
        }

        public bool Remove(object? item)
        {
            EnsureUnbound();

            var previousSelected = _owner.CaptureSelectedItem();
            var hadSelection = _owner.HasSelection;

            var removed = _items.Remove(item);
            if (removed)
                _owner.SourceCollectionChanged(previousSelected, hadSelection);

            return removed;
        }

        public bool Contains(object? item) =>
            _owner._dataSource is null
                ? _items.Contains(item)
                : EnumerableItems().Any(existing => Equals(existing, item));

        public int IndexOf(object? item)
        {
            if (_owner._dataSource is null)
                return _items.IndexOf(item);

            for (var index = 0; index < _owner._bindingSource.Count; index++)
            {
                if (Equals(_owner._bindingSource[index], item))
                    return index;
            }

            return -1;
        }

        public void Clear()
        {
            EnsureUnbound();
            if (_items.Count == 0)
                return;

            var previousSelected = _owner.CaptureSelectedItem();
            var hadSelection = _owner.HasSelection;

            _items.Clear();
            _owner.SourceCollectionChanged(previousSelected, hadSelection);
        }

        public void CopyTo(object?[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < CountInternal)
                throw new ArgumentException("The destination array is too small.", nameof(array));

            for (var index = 0; index < CountInternal; index++)
                array[arrayIndex + index] = this[index];
        }

        public IEnumerator<object?> GetEnumerator() =>
            EnumerableItems().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        private IEnumerable<object?> EnumerableItems()
        {
            for (var index = 0; index < CountInternal; index++)
                yield return this[index];
        }

        private void EnsureUnbound()
        {
            if (_owner._dataSource is not null)
            {
                throw new InvalidOperationException(
                    "Items cannot be modified while DataSource is assigned.");
            }
        }
    }

    private sealed class HiveComboBoxPopupForm : Form
    {
        private readonly HiveComboBox _owner;
        private readonly HostTextBox _filterEditor;
        private readonly HiveScrollHost _scrollHost;
        private readonly HiveComboPopupList _list;
        private HiveThemeDefinition? _theme;
        private string _filterText = string.Empty;

        public HiveComboBoxPopupForm(
            HiveComboBox owner,
            HiveThemeDefinition? theme)
        {
            _owner = owner;
            _theme = theme;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ShowIcon = false;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            Padding = new Padding(1);
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleRole = AccessibleRole.Dialog;
            AccessibleName = "Combo box options";
            Deactivate += OnDeactivate;
            KeyDown += OnKeyDown;

            _filterEditor = new HostTextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Top,
                Height = LogicalToDevice(34),
                Margin = Padding.Empty,
                Padding = new Padding(8, 0, 8, 0),
                AutoSize = false,
                PlaceholderText = "Filter options",
                AccessibleRole = AccessibleRole.Text,
                AccessibleName = "Filter options"
            };
            _filterEditor.TextChanged += FilterEditorOnTextChanged;

            _list = new HiveComboPopupList(
                owner,
                LogicalToDevice(owner._itemHeight));
            _list.ItemInvoked += ListOnItemInvoked;
            _list.HighlightedItemChanged += ListOnHighlightedItemChanged;

            _scrollHost = new HiveScrollHost
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AccessibleName = "Combo box option list",
                AccessibleDescription = "Navigate the filtered options."
            };
            _scrollHost.Attach(_list);

            Controls.Add(_scrollHost);
            Controls.Add(_filterEditor);

            ApplyTheme(_theme);
        }

        public string FilterText => _filterText;

        internal HiveScrollState VerticalScrollStateForTesting =>
            _scrollHost.VerticalScrollState;

        public void SetItems(
            IReadOnlyList<HiveComboBoxItem> items,
            int selectedSourceIndex)
        {
            _list.SetItems(items, selectedSourceIndex);
            UpdateListSize();
        }

        public void SetFilterText(string value)
        {
            _filterText = value ?? string.Empty;

            if (!string.Equals(
                    _filterEditor.Text,
                    _filterText,
                    StringComparison.Ordinal))
            {
                _filterEditor.Text = _filterText;
            }
        }

        public void CommitHighlightedSelection()
        {
            var sourceIndex = _list.GetHighlightedSourceIndex();
            if (sourceIndex >= 0)
                _owner.CommitPopupSelection(sourceIndex);
        }

        public void MoveHighlight(int delta)
        {
            _list.MoveHighlight(delta);
        }

        public void ApplyTheme(HiveThemeDefinition? theme)
        {
            _theme = theme;

            if (theme is not null)
            {
                BackColor = theme.Palette.Surface;
                ForeColor = theme.Palette.Text;
                _filterEditor.BackColor = theme.Palette.InputBackground;
                _filterEditor.ForeColor = theme.Palette.Text;
                _list.ApplyTheme(theme);
                _scrollHost.ApplyTheme(theme);
            }

            Invalidate();
        }

        public void Reposition()
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            SetBoundsForOwner();
        }

        public void ShowPopup()
        {
            SetBoundsForOwner();

            var form = _owner.FindForm();
            if (form is null)
                Show();
            else
                Show(form);

            Activate();
            SynchronizeInitialScrollPosition();
            _filterEditor.Focus();
        }

        private void SynchronizeInitialScrollPosition()
        {
            if (_list.Count == 0)
                return;

            _scrollHost.Synchronize();
            EnsureHighlightedItemVisible();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using var pen = new Pen(
                _theme?.Palette.Border ?? SystemColors.WindowFrame);

            e.Graphics.DrawRectangle(
                pen,
                0,
                0,
                ClientSize.Width - 1,
                ClientSize.Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Deactivate -= OnDeactivate;
                KeyDown -= OnKeyDown;
                _filterEditor.TextChanged -= FilterEditorOnTextChanged;
                _list.ItemInvoked -= ListOnItemInvoked;
                _list.HighlightedItemChanged -= ListOnHighlightedItemChanged;
            }

            base.Dispose(disposing);
        }

        private void OnDeactivate(object? sender, EventArgs e)
        {
            if (!IsDisposed)
                _owner.HideDropDown();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _owner.HideDropDown();
                    break;

                case Keys.Enter:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    CommitHighlightedSelection();
                    break;

                case Keys.Down:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _list.MoveHighlight(1);
                    break;

                case Keys.Up:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _list.MoveHighlight(-1);
                    break;

                case Keys.PageDown:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _list.MoveHighlight(Math.Max(1, _owner._maxDropDownItems - 1));
                    break;

                case Keys.PageUp:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _list.MoveHighlight(-Math.Max(1, _owner._maxDropDownItems - 1));
                    break;

                case Keys.Home:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _list.SelectFirst();
                    break;

                case Keys.End:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _list.SelectLast();
                    break;
            }
        }

        private void FilterEditorOnTextChanged(object? sender, EventArgs e)
        {
            _filterText = _filterEditor.Text;
            _owner.SetPopupFilter(_filterText);
        }

        private void ListOnItemInvoked(int sourceIndex)
        {
            _owner.CommitPopupSelection(sourceIndex);
        }

        private void ListOnHighlightedItemChanged(
            object? sender,
            EventArgs e)
        {
            EnsureHighlightedItemVisible();
        }

        private void EnsureHighlightedItemVisible()
        {
            var highlightedIndex = _list.GetHighlightedSourceIndex();
            if (highlightedIndex < 0)
                return;

            var current = _scrollHost.VerticalScrollPosition;
            var target = CalculatePopupHighlightScroll(
                current,
                _list.GetHighlightedY(),
                _list.RowHeight,
                _scrollHost.VerticalScrollState.ViewportSize);

            if (target != current)
            {
                _scrollHost.SetScrollPosition(
                    _scrollHost.HorizontalScrollPosition,
                    target);
            }
        }

        private void UpdateListSize()
        {
            var height = Math.Max(
                _list.RowHeight,
                _list.Count * _list.RowHeight);

            _list.Size = new Size(
                Math.Max(1, ClientSize.Width - Padding.Horizontal),
                height);
        }

        private void SetBoundsForOwner()
        {
            var workArea = Screen.FromControl(_owner).WorkingArea;
            var width = Math.Max(
                _owner.Width,
                _owner.LogicalToDevice(_owner._dropDownWidth));

            width = Math.Min(width, workArea.Width);

            var anchor = _owner.PointToScreen(
                new Point(0, _owner.Height));

            var rows = Math.Min(
                Math.Max(1, _owner.MaxDropDownItems),
                Math.Max(1, _owner.SourceCount));
            var desiredHeight =
                _owner.LogicalToDevice(34) +
                _owner.LogicalToDevice(2) +
                _owner.LogicalToDevice(_owner._itemHeight) * rows;

            var maxHeight = Math.Max(
                _owner.LogicalToDevice(80),
                workArea.Height - _owner.LogicalToDevice(8));
            desiredHeight = Math.Min(desiredHeight, maxHeight);

            var ownerBounds = new Rectangle(
                _owner.PointToScreen(Point.Empty),
                _owner.Size);

            Bounds = CalculatePopupBounds(
                ownerBounds,
                workArea,
                width,
                desiredHeight);
        }

        internal static Rectangle CalculatePopupBounds(
            Rectangle ownerBounds,
            Rectangle workArea,
            int popupWidth,
            int popupHeight)
        {
            var width = Math.Clamp(
                popupWidth,
                1,
                Math.Max(1, workArea.Width));

            var height = Math.Clamp(
                popupHeight,
                1,
                Math.Max(1, workArea.Height));

            var belowY = ownerBounds.Bottom;
            var aboveY = ownerBounds.Top - height;
            var y = belowY;

            if (belowY + height > workArea.Bottom &&
                aboveY >= workArea.Top)
            {
                y = aboveY;
            }

            y = Math.Clamp(
                y,
                workArea.Top,
                Math.Max(workArea.Top, workArea.Bottom - height));

            var x = Math.Clamp(
                ownerBounds.Left,
                workArea.Left,
                Math.Max(workArea.Left, workArea.Right - width));

            return new Rectangle(x, y, width, height);
        }

        private int LogicalToDevice(int value) =>
            Math.Max(
                1,
                (int)Math.Round(
                    value * DeviceDpi / 96f,
                    MidpointRounding.AwayFromZero));
    }

    private sealed class HiveComboPopupList : Control
    {
        private readonly HiveComboBox _owner;
        private HiveThemeDefinition? _theme;
        private IReadOnlyList<HiveComboBoxItem> _items =
            Array.Empty<HiveComboBoxItem>();
        private int _highlightedIndex = -1;
        private int _rowHeight;

        public HiveComboPopupList(
            HiveComboBox owner,
            int rowHeight)
        {
            _owner = owner;
            _rowHeight = Math.Max(1, rowHeight);

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            AccessibleRole = AccessibleRole.List;
            AccessibleName = "Combo box options";
            TabStop = false;
        }

        public event Action<int>? ItemInvoked;

        public event EventHandler? HighlightedItemChanged;

        public int Count => _items.Count;

        public int RowHeight => _rowHeight;

        public void SetItems(
            IReadOnlyList<HiveComboBoxItem> items,
            int selectedSourceIndex)
        {
            _items = items;
            _highlightedIndex = -1;

            for (var index = 0; index < _items.Count; index++)
            {
                if (_items[index].SourceIndex == selectedSourceIndex)
                {
                    _highlightedIndex = index;
                    break;
                }
            }

            if (_highlightedIndex < 0 && _items.Count > 0)
                _highlightedIndex = 0;

            Invalidate();
            HighlightedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyTheme(HiveThemeDefinition theme)
        {
            _theme = theme;
            BackColor = theme.Palette.Surface;
            ForeColor = theme.Palette.Text;
            Invalidate();
        }

        public int GetHighlightedSourceIndex() =>
            _highlightedIndex >= 0 &&
            _highlightedIndex < _items.Count
                ? _items[_highlightedIndex].SourceIndex
                : -1;

        public int GetHighlightedY() =>
            Math.Max(
                0,
                _highlightedIndex * _rowHeight);

        public void MoveHighlight(int delta)
        {
            if (_items.Count == 0 || delta == 0)
                return;

            var target = _highlightedIndex < 0
                ? delta > 0 ? 0 : _items.Count - 1
                : Math.Clamp(
                    _highlightedIndex + delta,
                    0,
                    _items.Count - 1);

            if (target == _highlightedIndex)
                return;

            _highlightedIndex = target;
            Invalidate();
            HighlightedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectFirst()
        {
            if (_items.Count == 0)
                return;

            _highlightedIndex = 0;
            Invalidate();
            HighlightedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectLast()
        {
            if (_items.Count == 0)
                return;

            _highlightedIndex = _items.Count - 1;
            Invalidate();
            HighlightedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var index = e.Y / _rowHeight;
            if (index < 0 || index >= _items.Count)
                index = -1;

            if (_highlightedIndex == index)
                return;

            _highlightedIndex = index;
            Invalidate();
            HighlightedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            var index = e.Y / _rowHeight;
            if (index < 0 || index >= _items.Count)
                return;

            ItemInvoked?.Invoke(
                _items[index].SourceIndex);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var theme = _theme;
            var normalBackground =
                theme?.Palette.Surface ?? SystemColors.Window;
            var normalText =
                theme?.Palette.Text ?? SystemColors.ControlText;
            var selectedBackground =
                theme?.Palette.Selection ?? SystemColors.Highlight;
            var hoverBackground =
                theme?.VisualStates.HoverBackground ?? SystemColors.HotTrack;
            var selectedText =
                theme?.Palette.Text ?? SystemColors.HighlightText;

            e.Graphics.Clear(normalBackground);

            for (var index = 0; index < _items.Count; index++)
            {
                var top = index * _rowHeight;
                if (top > ClientSize.Height ||
                    top + _rowHeight < 0)
                    continue;

                var row = new Rectangle(
                    0,
                    top,
                    ClientSize.Width,
                    _rowHeight);

                var item = _items[index];

                if (index == _highlightedIndex)
                {
                    using var brush = new SolidBrush(
                        item.SourceIndex == _owner._selectedIndex
                            ? selectedBackground
                            : hoverBackground);
                    e.Graphics.FillRectangle(brush, row);
                }
                else if (item.SourceIndex == _owner._selectedIndex)
                {
                    using var brush = new SolidBrush(selectedBackground);
                    e.Graphics.FillRectangle(brush, row);
                }

                var textBounds = row;
                textBounds.Inflate(
                    -LogicalToDevice(10),
                    0);

                TextRenderer.DrawText(
                    e.Graphics,
                    item.Display,
                    _owner.Font,
                    textBounds,
                    normalText,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);

                if (item.SourceIndex == _owner._selectedIndex)
                {
                    var marker = new Rectangle(
                        ClientSize.Width - LogicalToDevice(14),
                        top + LogicalToDevice(10),
                        LogicalToDevice(5),
                        LogicalToDevice(14));

                    using var markerBrush = new SolidBrush(
                        theme?.Palette.Accent ?? SystemColors.Highlight);
                    e.Graphics.FillRectangle(markerBrush, marker);
                }
            }

            if (_items.Count == 0)
            {
                var message = _owner.SourceCount == 0
                    ? "No options"
                    : "No matching options";

                TextRenderer.DrawText(
                    e.Graphics,
                    message,
                    _owner.Font,
                    ClientRectangle,
                    theme?.Palette.MutedText ?? SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix);
            }
        }

        private int LogicalToDevice(int value) =>
            Math.Max(
                1,
                (int)Math.Round(
                    value * DeviceDpi / 96f,
                    MidpointRounding.AwayFromZero));
    }
}
