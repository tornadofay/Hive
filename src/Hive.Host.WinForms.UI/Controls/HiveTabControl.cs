using System.Collections;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveTabControl : UserControl
{
    private const int DefaultHeaderHeight = 42;
    private const int HorizontalPadding = 16;
    private const int MinimumTabWidth = 88;
    private const int MaximumTabWidth = 320;

    private readonly HiveScrollHost _headerScrollHost;
    private readonly HiveTabHeaderSurface _headerSurface;
    private readonly Panel _pageHost;
    private readonly HiveTabPageCollection _tabPages;

    private int _selectedIndex = -1;
    private HiveThemeDefinition? _theme;
    private bool _layouting;

    public HiveTabControl()
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
        Padding = Padding.Empty;
        TabStop = false;
        AccessibleRole = AccessibleRole.PageTabList;
        AccessibleName = "Tab control";

        _tabPages = new HiveTabPageCollection(this);

        _pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = SystemColors.Window
        };

        _headerScrollHost = new HiveScrollHost
        {
            Dock = DockStyle.Top,
            Height = LogicalToDevice(DefaultHeaderHeight),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AccessibleRole = AccessibleRole.PageTabList,
            AccessibleName = "Tab headers"
        };

        _headerSurface = new HiveTabHeaderSurface(this)
        {
            Height = _headerScrollHost.Height,
            Margin = Padding.Empty
        };
        _headerSurface.TabInvoked += HeaderOnTabInvoked;
        _headerSurface.HeaderFocusChanged += HeaderOnFocusChanged;

        _headerScrollHost.Attach(_headerSurface);

        Controls.Add(_pageHost);
        Controls.Add(_headerScrollHost);

        UpdateHeaderLayout();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public HiveTabPageCollection TabPages => _tabPages;

    [DefaultValue(-1)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= _tabPages.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "SelectedIndex must be -1 or refer to an existing tab page.");
            }

            SetSelectedIndexCore(value, userInitiated: false);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TabPage? SelectedTab
    {
        get => _selectedIndex >= 0 ? _tabPages[_selectedIndex] : null;
        set
        {
            if (value is null)
            {
                SelectedIndex = -1;
                return;
            }

            var index = _tabPages.IndexOf(value);
            if (index < 0)
            {
                throw new ArgumentException(
                    "The supplied TabPage does not belong to this HiveTabControl.",
                    nameof(value));
            }

            SelectedIndex = index;
        }
    }

    [DefaultValue(DefaultHeaderHeight)]
    public int HeaderHeight
    {
        get => DeviceToLogical(_headerScrollHost.Height);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            var height = LogicalToDevice(value);
            if (_headerScrollHost.Height == height)
                return;

            _headerScrollHost.Height = height;
            _headerSurface.Height = height;
            UpdateHeaderLayout();
            Invalidate();
        }
    }

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? SelectedTabChanged;

    public void SelectNextTab() => SelectRelative(1);
    public void SelectPreviousTab() => SelectRelative(-1);

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;
        BackColor = theme.Palette.Surface;
        ForeColor = theme.Palette.Text;
        _pageHost.BackColor = theme.Palette.Surface;
        _headerScrollHost.ApplyTheme(theme);
        _headerSurface.ApplyTheme(theme);

        Invalidate(true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateHeaderLayout();
        EnsureSelectedTabVisible();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateHeaderLayout();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _headerSurface.Invalidate();
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Enabled)
            return base.ProcessCmdKey(ref msg, keyData);

        var keyCode = keyData & Keys.KeyCode;
        var modifiers = keyData & Keys.Modifiers;

        if (keyCode == Keys.Tab && modifiers == (Keys.Control | Keys.Shift))
        {
            SelectRelative(-1);
            return true;
        }

        if (keyCode == Keys.Tab && modifiers == Keys.Control)
        {
            SelectRelative(1);
            return true;
        }

        if (_headerSurface.HasKeyboardFocus && modifiers == Keys.None)
        {
            switch (keyCode)
            {
                case Keys.Left:
                    SelectRelative(-1);
                    return true;
                case Keys.Right:
                    SelectRelative(1);
                    return true;
                case Keys.Home:
                    SelectFirstEnabledTab();
                    return true;
                case Keys.End:
                    SelectLastEnabledTab();
                    return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headerSurface.TabInvoked -= HeaderOnTabInvoked;
            _headerSurface.HeaderFocusChanged -= HeaderOnFocusChanged;
        }

        base.Dispose(disposing);
    }

    internal Rectangle GetTabHeaderBoundsForTesting(int index) =>
        _headerSurface.GetTabBoundsForTesting(index);

    internal HiveScrollState HeaderHorizontalScrollStateForTesting =>
        _headerScrollHost.HorizontalScrollState;

    internal int HeaderHorizontalScrollPositionForTesting =>
        _headerScrollHost.HorizontalScrollPosition;

    internal void FocusHeaderForTesting()
    {
        _headerSurface.Focus();
    }

    internal void ProcessKeyForTesting(Keys keyData)
    {
        var message = Message.Create(
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            IntPtr.Zero);

        ProcessCmdKey(
            ref message,
            keyData);
    }

    internal int CalculateHeaderScrollForTesting(int index) =>
        CalculateHeaderScrollForSelectedTab(index);

    internal static int CalculateHeaderScrollForTesting(
        int currentScroll,
        int tabLeft,
        int tabWidth,
        int viewportSize) =>
        CalculateHeaderScroll(currentScroll, tabLeft, tabWidth, viewportSize);

    private void AddTabPage(TabPage page, int index)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Parent is not null && !ReferenceEquals(page.Parent, _pageHost))
        {
            throw new InvalidOperationException(
                "The TabPage must not already have a parent.");
        }

        if (_tabPages.Contains(page))
            throw new ArgumentException(
                "The TabPage is already part of this control.",
                nameof(page));

        if (index < 0 || index > _tabPages.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _tabPages.InsertCore(index, page);
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pageHost.Controls.Add(page);

        if (_selectedIndex < 0)
        {
            SetSelectedIndexCore(0, userInitiated: false);
        }
        else if (index <= _selectedIndex)
        {
            _selectedIndex++;
            UpdateSelectedPageVisibility();
        }

        RefreshHeaders();
    }

    private void RemoveTabPage(TabPage page)
    {
        var index = _tabPages.IndexOf(page);
        if (index < 0)
            return;

        var wasSelected = index == _selectedIndex;
        _tabPages.RemoveCoreAt(index);
        _pageHost.Controls.Remove(page);
        page.Visible = false;

        if (_tabPages.Count == 0)
        {
            SetSelectedIndexCore(-1, userInitiated: false);
        }
        else if (wasSelected)
        {
            SetSelectedIndexCore(
                Math.Min(index, _tabPages.Count - 1),
                userInitiated: false);
        }
        else if (index < _selectedIndex)
        {
            _selectedIndex--;
            UpdateSelectedPageVisibility();
        }

        RefreshHeaders();
    }

    private void ClearTabPages()
    {
        var pages = _tabPages.ToArray();

        foreach (var page in pages)
        {
            _pageHost.Controls.Remove(page);
            page.Visible = false;
        }

        _tabPages.ClearCore();
        SetSelectedIndexCore(-1, userInitiated: false);
        RefreshHeaders();
    }

    private void HeaderOnTabInvoked(int index)
    {
        if (index < 0 || index >= _tabPages.Count)
            return;

        if (!_tabPages[index].Enabled)
            return;

        Focus();
        SetSelectedIndexCore(index, userInitiated: true);
    }

    private void HeaderOnFocusChanged(bool focused)
    {
        _headerSurface.Invalidate();
    }

    private void SetSelectedIndexCore(int value, bool userInitiated)
    {
        if (_selectedIndex == value)
        {
            if (value >= 0 && userInitiated && !_tabPages[value].Enabled)
                return;

            EnsureSelectedTabVisible();
            return;
        }

        if (userInitiated &&
            (value < 0 ||
             value >= _tabPages.Count ||
             !_tabPages[value].Enabled))
            return;

        var previous = _selectedIndex;
        _selectedIndex = value;

        UpdateSelectedPageVisibility();
        RefreshHeaders();
        EnsureSelectedTabVisible();

        AccessibleName = SelectedTab is { } selected
            ? $"Tab control, selected {selected.Text}"
            : "Tab control";

        if (previous != _selectedIndex)
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

        if (!AreSameSelectedPage(previous, _selectedIndex))
            SelectedTabChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool AreSameSelectedPage(int previous, int current)
    {
        if (previous < 0 || current < 0)
            return previous == current;

        return ReferenceEquals(_tabPages[previous], _tabPages[current]);
    }

    private void UpdateSelectedPageVisibility()
    {
        for (var index = 0; index < _tabPages.Count; index++)
            _tabPages[index].Visible = index == _selectedIndex;

        if (SelectedTab is { } selected && _theme is not null)
            selected.BackColor = _theme.Palette.Surface;
    }

    private void SelectRelative(int delta)
    {
        if (_tabPages.Count == 0)
            return;

        var start = _selectedIndex < 0
            ? delta > 0 ? 0 : _tabPages.Count - 1
            : _selectedIndex;

        for (var offset = 1; offset <= _tabPages.Count; offset++)
        {
            var index = Mod(start + delta * offset, _tabPages.Count);
            if (_tabPages[index].Enabled)
            {
                Focus();
                SetSelectedIndexCore(index, userInitiated: true);
                return;
            }
        }
    }

    private void SelectFirstEnabledTab()
    {
        for (var index = 0; index < _tabPages.Count; index++)
        {
            if (_tabPages[index].Enabled)
            {
                SetSelectedIndexCore(index, userInitiated: true);
                return;
            }
        }
    }

    private void SelectLastEnabledTab()
    {
        for (var index = _tabPages.Count - 1; index >= 0; index--)
        {
            if (_tabPages[index].Enabled)
            {
                SetSelectedIndexCore(index, userInitiated: true);
                return;
            }
        }
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private void RefreshHeaders()
    {
        if (_layouting)
            return;

        UpdateHeaderLayout();
    }

    private void UpdateHeaderLayout()
    {
        if (_layouting || IsDisposed || DeviceDpi <= 0)
            return;

        _layouting = true;
        try
        {
            var height = LogicalToDevice(HeaderHeight);
            _headerScrollHost.Height = height;
            _headerSurface.Height = height;
            _headerSurface.SetTabs(
                _tabPages,
                Font,
                height);

            _headerScrollHost.Synchronize();
        }
        finally
        {
            _layouting = false;
        }
    }

    private void EnsureSelectedTabVisible()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _tabPages.Count)
            return;

        var bounds = _headerSurface.GetTabBoundsForTesting(_selectedIndex);
        var current = _headerScrollHost.HorizontalScrollPosition;
        var viewport = _headerScrollHost.HorizontalScrollState.ViewportSize;

        var target = CalculateHeaderScroll(
            current,
            bounds.Left,
            bounds.Width,
            viewport);

        if (target != current)
        {
            _headerScrollHost.SetScrollPosition(
                target,
                _headerScrollHost.VerticalScrollPosition);
        }
    }

    private int CalculateHeaderScrollForSelectedTab(int index)
    {
        if (index < 0 || index >= _tabPages.Count)
            return _headerScrollHost.HorizontalScrollPosition;

        var bounds = _headerSurface.GetTabBoundsForTesting(index);
        return CalculateHeaderScroll(
            _headerScrollHost.HorizontalScrollPosition,
            bounds.Left,
            bounds.Width,
            _headerScrollHost.HorizontalScrollState.ViewportSize);
    }

    internal static int CalculateHeaderScroll(
        int currentScroll,
        int tabLeft,
        int tabWidth,
        int viewportSize)
    {
        if (viewportSize <= 0)
            return currentScroll;

        var tabRight = tabLeft + Math.Max(1, tabWidth);

        if (tabLeft < currentScroll)
            return tabLeft;

        if (tabRight > currentScroll + viewportSize)
            return tabRight - viewportSize;

        return currentScroll;
    }

    private int LogicalToDevice(int value) =>
        Math.Max(
            1,
            (int)Math.Round(
                value * DeviceDpi / 96f,
                MidpointRounding.AwayFromZero));

    private int DeviceToLogical(int value) =>
        Math.Max(
            1,
            (int)Math.Round(
                value * 96f / DeviceDpi,
                MidpointRounding.AwayFromZero));

    public sealed class HiveTabPageCollection : IList<TabPage>, IReadOnlyList<TabPage>
    {
        private readonly HiveTabControl _owner;
        private readonly List<TabPage> _pages = new();

        internal HiveTabPageCollection(HiveTabControl owner)
        {
            _owner = owner;
        }

        public TabPage this[int index]
        {
            get => _pages[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                var previous = _pages[index];
                if (ReferenceEquals(previous, value))
                    return;

                if (_pages.Contains(value))
                {
                    throw new ArgumentException(
                        "The TabPage is already part of this control.",
                        nameof(value));
                }

                _owner.RemoveTabPage(previous);
                _owner.AddTabPage(
                    value,
                    Math.Min(index, _owner._tabPages.Count));
            }
        }

        public int Count => _pages.Count;

        bool ICollection<TabPage>.IsReadOnly => false;

        public int Add(TabPage page)
        {
            _owner.AddTabPage(page, _pages.Count);
            return _pages.Count - 1;
        }

        public TabPage Add(string text)
        {
            var page = new TabPage(text);
            Add(page);
            return page;
        }

        public void AddRange(params TabPage[] pages)
        {
            ArgumentNullException.ThrowIfNull(pages);

            foreach (var page in pages)
                Add(page);
        }

        public void Insert(int index, TabPage item) =>
            _owner.AddTabPage(item, index);

        public bool Remove(TabPage item)
        {
            if (!_pages.Contains(item))
                return false;

            _owner.RemoveTabPage(item);
            return true;
        }

        public void RemoveAt(int index) =>
            _owner.RemoveTabPage(_pages[index]);

        public bool Contains(TabPage item) =>
            _pages.Contains(item);

        public int IndexOf(TabPage item) =>
            _pages.IndexOf(item);

        public void Clear() =>
            _owner.ClearTabPages();

        public void CopyTo(TabPage[] array, int arrayIndex) =>
            _pages.CopyTo(array, arrayIndex);

        public IEnumerator<TabPage> GetEnumerator() =>
            _pages.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        internal void InsertCore(int index, TabPage page) =>
            _pages.Insert(index, page);

        internal void RemoveCoreAt(int index) =>
            _pages.RemoveAt(index);

        internal void ClearCore() =>
            _pages.Clear();

        internal TabPage[] ToArray() =>
            _pages.ToArray();
    }

    private sealed class HiveTabHeaderSurface : Control
    {
        private readonly HiveTabControl _owner;
        private IReadOnlyList<TabPage> _pages = Array.Empty<TabPage>();
        private Rectangle[] _tabBounds = Array.Empty<Rectangle>();
        private HiveThemeDefinition? _theme;
        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;
        private bool _keyboardFocus;

        public HiveTabHeaderSurface(HiveTabControl owner)
        {
            _owner = owner;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            AccessibleRole = AccessibleRole.PageTabList;
            AccessibleName = "Tab headers";
        }

        public event Action<int>? TabInvoked;
        public event Action<bool>? HeaderFocusChanged;

        public bool HasKeyboardFocus => _keyboardFocus;

        public void SetTabs(
            IReadOnlyList<TabPage> pages,
            Font font,
            int headerHeight)
        {
            _pages = pages;
            _tabBounds = new Rectangle[pages.Count];

            var x = 0;
            for (var index = 0; index < pages.Count; index++)
            {
                var text = pages[index].Text ?? string.Empty;
                var measured = TextRenderer.MeasureText(
                    text,
                    font,
                    new Size(MaximumTabWidth, headerHeight),
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding);

                var width = Math.Clamp(
                    measured.Width + LogicalToDevice(HorizontalPadding * 2),
                    LogicalToDevice(MinimumTabWidth),
                    LogicalToDevice(MaximumTabWidth));

                _tabBounds[index] = new Rectangle(
                    x,
                    0,
                    width,
                    headerHeight);

                x += width;
            }

            Size = new Size(
                Math.Max(1, x),
                Math.Max(1, headerHeight));

            _hoveredIndex = Math.Clamp(
                _hoveredIndex,
                -1,
                _pages.Count - 1);

            _pressedIndex = Math.Clamp(
                _pressedIndex,
                -1,
                _pages.Count - 1);

            Invalidate();
        }

        public void ApplyTheme(HiveThemeDefinition theme)
        {
            _theme = theme;
            BackColor = theme.Palette.Surface;
            ForeColor = theme.Palette.Text;
            Invalidate();
        }

        internal Rectangle GetTabBoundsForTesting(int index) =>
            index >= 0 && index < _tabBounds.Length
                ? _tabBounds[index]
                : Rectangle.Empty;

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _keyboardFocus = true;
            HeaderFocusChanged?.Invoke(true);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _keyboardFocus = false;
            HeaderFocusChanged?.Invoke(false);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var index = HitTest(e.Location);
            if (_hoveredIndex == index)
                return;

            _hoveredIndex = index;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            var index = HitTest(e.Location);
            if (index < 0 || !_pages[index].Enabled)
                return;

            _owner._headerSurface.Focus();
            _pressedIndex = index;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left)
                return;

            var pressed = _pressedIndex;
            _pressedIndex = -1;
            Invalidate();

            if (pressed >= 0 && pressed == HitTest(e.Location))
                TabInvoked?.Invoke(pressed);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var theme = _theme;
            var surface = theme?.Palette.Surface ?? SystemColors.Window;
            var text = theme?.Palette.Text ?? SystemColors.ControlText;
            var hover = theme?.VisualStates.NavigationHover ?? SystemColors.ControlLight;
            var pressed = theme?.VisualStates.NavigationPressed ?? SystemColors.ControlDark;
            var selected = theme?.VisualStates.NavigationSelected ?? SystemColors.Highlight;
            var selectedText = theme?.VisualStates.NavigationSelectedText ?? SystemColors.HighlightText;
            var border = theme?.Palette.Border ?? SystemColors.ControlDark;
            var accent = theme?.Palette.Accent ?? SystemColors.Highlight;
            var disabledText = theme?.Palette.DisabledText ?? SystemColors.GrayText;

            e.Graphics.Clear(surface);

            for (var index = 0; index < _tabBounds.Length; index++)
            {
                var bounds = _tabBounds[index];
                if (bounds.Right <= 0 || bounds.Left >= ClientSize.Width)
                    continue;

                var page = _pages[index];
                var isSelected = index == _owner._selectedIndex;
                var isHovered = index == _hoveredIndex;
                var isPressed = index == _pressedIndex;
                var enabled = _owner.Enabled && page.Enabled;

                var background = isSelected
                    ? selected
                    : isPressed
                        ? pressed
                        : isHovered && enabled
                            ? hover
                            : surface;

                var foreground = !enabled
                    ? disabledText
                    : isSelected
                        ? selectedText
                        : text;

                using (var brush = new SolidBrush(background))
                    e.Graphics.FillRectangle(brush, bounds);

                TextRenderer.DrawText(
                    e.Graphics,
                    page.Text ?? string.Empty,
                    _owner.Font,
                    bounds,
                    foreground,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);

                if (isSelected)
                {
                    using var indicator = new SolidBrush(accent);
                    e.Graphics.FillRectangle(
                        indicator,
                        bounds.Left,
                        Math.Max(
                            bounds.Bottom - LogicalToDevice(3),
                            bounds.Top),
                        bounds.Width,
                        LogicalToDevice(3));
                }

                if (index < _tabBounds.Length - 1)
                {
                    using var separator = new Pen(border);
                    e.Graphics.DrawLine(
                        separator,
                        bounds.Right - 1,
                        bounds.Top + LogicalToDevice(8),
                        bounds.Right - 1,
                        bounds.Bottom - LogicalToDevice(8));
                }
            }

            if (_keyboardFocus &&
                _owner._selectedIndex >= 0 &&
                _owner._selectedIndex < _tabBounds.Length)
            {
                var focusBounds = Rectangle.Inflate(
                    _tabBounds[_owner._selectedIndex],
                    -LogicalToDevice(3),
                    -LogicalToDevice(3));

                using var focusPen = new Pen(accent, LogicalToDevice(1));
                focusPen.DashStyle = DashStyle.Dot;

                e.Graphics.DrawRectangle(
                    focusPen,
                    focusBounds.Left,
                    focusBounds.Top,
                    focusBounds.Width - 1,
                    focusBounds.Height - 1);
            }

            using var bottomBorder = new Pen(border);
            e.Graphics.DrawLine(
                bottomBorder,
                0,
                ClientSize.Height - 1,
                ClientSize.Width,
                ClientSize.Height - 1);
        }

        private int HitTest(Point location)
        {
            for (var index = 0; index < _tabBounds.Length; index++)
            {
                if (_tabBounds[index].Contains(location))
                    return index;
            }

            return -1;
        }

        private int LogicalToDevice(int value) =>
            Math.Max(
                1,
                (int)Math.Round(
                    value * DeviceDpi / 96f,
                    MidpointRounding.AwayFromZero));
    }

    private sealed class HiveTabAccessibleObject : ControlAccessibleObject
    {
        private readonly HiveTabControl _owner;

        public HiveTabAccessibleObject(HiveTabControl owner)
            : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role =>
            AccessibleRole.PageTabList;

        public override AccessibleStates State =>
            _owner.Enabled
                ? AccessibleStates.Focusable
                : AccessibleStates.Unavailable;

        public override string? Value =>
            _owner.SelectedTab?.Text;
    }

    protected override AccessibleObject CreateAccessibilityInstance() =>
        new HiveTabAccessibleObject(this);
}
