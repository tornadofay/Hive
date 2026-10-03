using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;
using System.ComponentModel;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveScrollHost : UserControl
{
    private readonly Panel _viewport;
    private readonly HiveScrollBar _horizontalScrollBar;
    private readonly HiveScrollBar _verticalScrollBar;
    private readonly HashSet<Control> _hookedContentControls = new();
    private HiveNativeScrollAdapter? _nativeScrollAdapter;
    private NativeWheelInterceptor? _nativeWheelInterceptor;

    private Control? _content;
    private ContentPresentation? _contentPresentation;
    private bool _synchronizing;
    private bool _scrolling;
    private bool _synchronizationPending;
    private Point _lastReportedPosition;

    public HiveScrollHost()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _viewport = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoScroll = false
        };

        _horizontalScrollBar = new HiveScrollBar
        {
            Orientation = Orientation.Horizontal,
            Visible = false
        };

        _verticalScrollBar = new HiveScrollBar
        {
            Orientation = Orientation.Vertical,
            Visible = false
        };

        _horizontalScrollBar.ValueChanged += ScrollBarValueChanged;
        _verticalScrollBar.ValueChanged += ScrollBarValueChanged;

        Controls.Add(_viewport);
        Controls.Add(_horizontalScrollBar);
        Controls.Add(_verticalScrollBar);

        _lastReportedPosition = Point.Empty;

        UpdateScrollBarLayout();
    }

    public Control? Content => _content;

    public HiveScrollState HorizontalScrollState =>
        _horizontalScrollBar.State;

    public HiveScrollState VerticalScrollState =>
        _verticalScrollBar.State;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int HorizontalScrollPosition
    {
        get => _horizontalScrollBar.Value;
        set => SetScrollPosition(value, VerticalScrollPosition);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int VerticalScrollPosition
    {
        get => _verticalScrollBar.Value;
        set => SetScrollPosition(HorizontalScrollPosition, value);
    }

    public event EventHandler? ScrollPositionChanged;

    public void Attach(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (ReferenceEquals(_content, content))
        {
            Synchronize();
            return;
        }

        if (content.Parent is not null &&
            !ReferenceEquals(content.Parent, _viewport))
        {
            throw new InvalidOperationException(
                "The scroll content must not already have a parent. Detach it from its current parent before attaching it to HiveScrollHost.");
        }

        Detach();

        _content = content;
        _contentPresentation = ContentPresentation.Capture(content);
        _nativeScrollAdapter = HiveNativeScrollAdapter.Supports(content)
            ? new HiveNativeScrollAdapter()
            : null;

        if (_nativeScrollAdapter is null &&
            content is ScrollableControl scrollable)
        {
            scrollable.AutoScroll = false;
        }

        content.Dock = DockStyle.None;
        content.Anchor = AnchorStyles.Top | AnchorStyles.Left;

        _viewport.Controls.Add(content);

        HookContentControls(content);

        content.Resize += ContentChanged;
        content.Layout += ContentChanged;
        content.SizeChanged += ContentChanged;
        content.HandleCreated += NativeContentHandleCreated;
        content.HandleDestroyed += NativeContentHandleDestroyed;
        content.FontChanged += ContentChanged;
        content.KeyUp += NativeContentKeyUp;

        if (_nativeScrollAdapter is not null &&
            content.IsHandleCreated)
        {
            AttachNativeWheelInterceptor(content);
        }

        if (content is TextBoxBase textBox)
            textBox.TextChanged += ContentChanged;

        if (content is TreeView tree)
        {
            tree.AfterExpand += NativeTreeViewChanged;
            tree.AfterCollapse += NativeTreeViewChanged;
            tree.AfterSelect += NativeTreeViewChanged;
        }

        if (content is ListView listView)
            listView.SelectedIndexChanged += NativeListViewChanged;

        Synchronize();
    }

    public Control? Detach()
    {
        var content = _content;
        if (content is null)
            return null;

        content.Resize -= ContentChanged;
        content.Layout -= ContentChanged;
        content.SizeChanged -= ContentChanged;
        content.HandleCreated -= NativeContentHandleCreated;
        content.HandleDestroyed -= NativeContentHandleDestroyed;
        content.FontChanged -= ContentChanged;
        content.KeyUp -= NativeContentKeyUp;

        DetachNativeWheelInterceptor();

        if (content is TextBoxBase textBox)
            textBox.TextChanged -= ContentChanged;

        if (content is TreeView tree)
        {
            tree.AfterExpand -= NativeTreeViewChanged;
            tree.AfterCollapse -= NativeTreeViewChanged;
            tree.AfterSelect -= NativeTreeViewChanged;
        }

        if (content is ListView listView)
            listView.SelectedIndexChanged -= NativeListViewChanged;

        UnhookContentControls(content);
        _viewport.Controls.Remove(content);

        if (content is HiveListView hiveListView)
            hiveListView.RestoreNativeScrollBars();

        var presentation = _contentPresentation;
        if (presentation is not null)
            presentation.Restore(content);

        _content = null;
        _contentPresentation = null;
        _nativeScrollAdapter = null;

        _horizontalScrollBar.SetState(
            HiveScrollState.Create(
                Orientation.Horizontal,
                0,
                0,
                0,
                0,
                enabled: false));

        _verticalScrollBar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                0,
                0,
                0,
                0,
                enabled: false));

        _horizontalScrollBar.Visible = false;
        _verticalScrollBar.Visible = false;

        UpdateScrollBarLayout();

        var previous = _lastReportedPosition;
        _lastReportedPosition = Point.Empty;

        if (previous != Point.Empty)
            ScrollPositionChanged?.Invoke(
                this,
                EventArgs.Empty);

        return content;
    }

    public void Synchronize()
    {
        if (IsDisposed || Disposing)
            return;

        if (_synchronizing)
            return;

        _synchronizing = true;
        try
        {
            if (_content is null)
            {
                ApplyEmptyState();
                return;
            }

            var viewportSize = new Size(
                Math.Max(0, _viewport.ClientSize.Width),
                Math.Max(0, _viewport.ClientSize.Height));

            if (_nativeScrollAdapter is not null)
            {
                // Native controls need a real client viewport before their Win32
                // scroll range is authoritative. During handle/layout creation the
                // viewport can briefly be zero-sized; do not resize or suppress the
                // native control in that transient state.
                if (viewportSize.Width <= 0 ||
                    viewportSize.Height <= 0)
                {
                    return;
                }

                if (_content.Size != viewportSize)
                    _content.Size = viewportSize;

                if (_content.Location != Point.Empty)
                    _content.Location = Point.Empty;

                var nativeHorizontal = _nativeScrollAdapter.ReadState(
                    _content,
                    Orientation.Horizontal,
                    out var horizontalAuthoritative);
                var nativeVertical = _nativeScrollAdapter.ReadState(
                    _content,
                    Orientation.Vertical,
                    out var verticalAuthoritative);

                // Do not suppress the native non-client scrollbars until both
                // orientations have supplied their first authoritative state.
                // Otherwise a transient zero-page response can permanently hide the
                // native source of truth before the next layout synchronization.
                if (horizontalAuthoritative &&
                    verticalAuthoritative)
                {
                    // The native viewport size was just finalized above. Keep the
                    // native control's state authoritative while the Hive bars are
                    // updated, then make native scrollbar suppression the final
                    // synchronous step of this layout boundary.
                }

                _horizontalScrollBar.SetState(nativeHorizontal);
                _verticalScrollBar.SetState(nativeVertical);

                _horizontalScrollBar.Visible = nativeHorizontal.CanScroll;
                _verticalScrollBar.Visible = nativeVertical.CanScroll;
                _horizontalScrollBar.Enabled = nativeHorizontal.CanScroll;
                _verticalScrollBar.Enabled = nativeVertical.CanScroll;

                UpdateScrollBarLayout();

                if (horizontalAuthoritative &&
                    verticalAuthoritative)
                {
                    _nativeScrollAdapter.HideNativeScrollBars(_content);
                }

                NotifyScrollPositionChanged();
                return;
            }

            var contentSize = MeasureContentSize(viewportSize);

            var horizontal = HiveScrollState.Create(
                Orientation.Horizontal,
                0,
                contentSize.Width,
                _horizontalScrollBar.Value,
                viewportSize.Width,
                smallChange: ResolveSmallChange(viewportSize.Width),
                largeChange: ResolveLargeChange(viewportSize.Width),
                enabled: contentSize.Width > viewportSize.Width);

            var vertical = HiveScrollState.Create(
                Orientation.Vertical,
                0,
                contentSize.Height,
                _verticalScrollBar.Value,
                viewportSize.Height,
                smallChange: ResolveSmallChange(viewportSize.Height),
                largeChange: ResolveLargeChange(viewportSize.Height),
                enabled: contentSize.Height > viewportSize.Height);

            _content.Size = contentSize;

            _horizontalScrollBar.SetState(horizontal);
            _verticalScrollBar.SetState(vertical);

            _horizontalScrollBar.Visible = horizontal.CanScroll;
            _verticalScrollBar.Visible = vertical.CanScroll;

            _horizontalScrollBar.Enabled = horizontal.CanScroll;
            _verticalScrollBar.Enabled = vertical.CanScroll;

            ApplyContentLocation();
            UpdateScrollBarLayout();

            NotifyScrollPositionChanged();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    public void SetScrollPosition(
        int horizontal,
        int vertical)
    {
        if (_content is null)
            return;

        if (_scrolling)
            return;

        _scrolling = true;
        try
        {
            var horizontalValue = Math.Clamp(
                horizontal,
                _horizontalScrollBar.State.Minimum,
                _horizontalScrollBar.State.EffectiveMaximum);

            var verticalValue = Math.Clamp(
                vertical,
                _verticalScrollBar.State.Minimum,
                _verticalScrollBar.State.EffectiveMaximum);

            if (_nativeScrollAdapter is not null)
            {
                var horizontalChanged =
                    horizontalValue != _horizontalScrollBar.Value;
                var verticalChanged =
                    verticalValue != _verticalScrollBar.Value;

                if (horizontalChanged)
                {
                    _nativeScrollAdapter.SetPosition(
                        _content,
                        Orientation.Horizontal,
                        horizontalValue);
                }

                if (verticalChanged)
                {
                    _nativeScrollAdapter.SetPosition(
                        _content,
                        Orientation.Vertical,
                        verticalValue);
                }

                SynchronizeNativeScrollPosition(
                    horizontalChanged,
                    verticalChanged);
            }
            else
            {
                _horizontalScrollBar.SetValue(horizontalValue);
                _verticalScrollBar.SetValue(verticalValue);

                ApplyContentLocation();
                NotifyScrollPositionChanged();
            }
        }
        finally
        {
            _scrolling = false;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_content is not null &&
            _nativeScrollAdapter is not null &&
            IsHandleCreated)
        {
            RequestSynchronization();
            return;
        }

        UpdateScrollBarLayout();
        Synchronize();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RequestSynchronization();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        if (_synchronizing)
            return;

        UpdateScrollBarLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingSynchronization();

            _horizontalScrollBar.ValueChanged -= ScrollBarValueChanged;
            _verticalScrollBar.ValueChanged -= ScrollBarValueChanged;

            DetachNativeWheelInterceptor();
            _nativeWheelInterceptor?.Dispose();
            _nativeWheelInterceptor = null;

            var content = _content;
            if (content is not null)
            {
                content.Resize -= ContentChanged;
                content.Layout -= ContentChanged;
                content.SizeChanged -= ContentChanged;
                content.HandleCreated -= NativeContentHandleCreated;
                content.HandleDestroyed -= NativeContentHandleDestroyed;
                content.FontChanged -= ContentChanged;
                content.KeyUp -= NativeContentKeyUp;

                if (content is TextBoxBase textBox)
                    textBox.TextChanged -= ContentChanged;

                if (content is TreeView tree)
                {
                    tree.AfterExpand -= NativeTreeViewChanged;
                    tree.AfterCollapse -= NativeTreeViewChanged;
                    tree.AfterSelect -= NativeTreeViewChanged;
                }

                if (content is ListView listView)
                    listView.SelectedIndexChanged -= NativeListViewChanged;

                UnhookContentControls(content);
            }
        }

        base.Dispose(disposing);
    }

    internal HiveScrollBar HorizontalScrollBarForTesting =>
        _horizontalScrollBar;

    internal HiveScrollBar VerticalScrollBarForTesting =>
        _verticalScrollBar;

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _viewport.BackColor = theme.Palette.Surface;
        BackColor = theme.Palette.Surface;
        Invalidate();
    }

    private void ScrollBarValueChanged(
        object? sender,
        EventArgs e)
    {
        if (_synchronizing || _scrolling || _content is null)
            return;

        _scrolling = true;
        try
        {
            if (_nativeScrollAdapter is not null &&
                _content is not null &&
                sender is HiveScrollBar scrollBar)
            {
                _nativeScrollAdapter.SetPosition(
                    _content,
                    scrollBar.Orientation,
                    scrollBar.Value);
                SynchronizeNativeScrollPosition(
                    scrollBar.Orientation == Orientation.Horizontal,
                    scrollBar.Orientation == Orientation.Vertical);
            }
            else
            {
                ApplyContentLocation();
                NotifyScrollPositionChanged();
            }
        }
        finally
        {
            _scrolling = false;
        }
    }

    private void SynchronizeNativeScrollPosition(
        bool horizontalChanged,
        bool verticalChanged)
    {
        if (_nativeScrollAdapter is null ||
            _content is null ||
            _synchronizing ||
            IsDisposed ||
            Disposing ||
            (!horizontalChanged && !verticalChanged))
        {
            return;
        }

        _synchronizing = true;
        try
        {
            var horizontalVisible = _horizontalScrollBar.Visible;
            var verticalVisible = _verticalScrollBar.Visible;

            if (horizontalChanged)
            {
                var horizontal = _nativeScrollAdapter.ReadState(
                    _content,
                    Orientation.Horizontal,
                    out _);
                _horizontalScrollBar.SetState(horizontal);
                _horizontalScrollBar.Visible = horizontal.CanScroll;
                _horizontalScrollBar.Enabled = horizontal.CanScroll;
            }

            if (verticalChanged)
            {
                var vertical = _nativeScrollAdapter.ReadState(
                    _content,
                    Orientation.Vertical,
                    out _);
                _verticalScrollBar.SetState(vertical);
                _verticalScrollBar.Visible = vertical.CanScroll;
                _verticalScrollBar.Enabled = vertical.CanScroll;
            }

            if (horizontalVisible != _horizontalScrollBar.Visible ||
                verticalVisible != _verticalScrollBar.Visible)
            {
                UpdateScrollBarLayout();
            }

            NotifyScrollPositionChanged();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void ContentChanged(
        object? sender,
        EventArgs e) =>
        RequestSynchronization();

    private void ContentMouseWheel(
        object? sender,
        MouseEventArgs e)
    {
        if (_content is null ||
            _scrolling)
        {
            return;
        }

        if (_nativeScrollAdapter is not null &&
            ReferenceEquals(sender, _content))
        {
            HandleNativeWheelDelta(
                e.Delta,
                Control.ModifierKeys.HasFlag(Keys.Shift));
            return;
        }

        if (sender is ListControl ||
            sender is DataGridView ||
            sender is TreeView)
        {
            return;
        }

        ScrollByWheel(
            e.Delta,
            Control.ModifierKeys.HasFlag(Keys.Shift));
    }

    private void HookContentControls(Control control)
    {
        if (!_hookedContentControls.Add(control))
            return;

        control.MouseWheel += ContentMouseWheel;
        control.ControlAdded += DescendantControlAdded;
        control.ControlRemoved += DescendantControlRemoved;

        foreach (Control child in control.Controls)
            HookContentControls(child);
    }

    private void UnhookContentControls(Control control)
    {
        if (!_hookedContentControls.Remove(control))
            return;

        control.MouseWheel -= ContentMouseWheel;
        control.ControlAdded -= DescendantControlAdded;
        control.ControlRemoved -= DescendantControlRemoved;

        foreach (Control child in control.Controls)
            UnhookContentControls(child);
    }

    private void NativeContentHandleCreated(object? sender, EventArgs e)
    {
        if (_nativeScrollAdapter is not null &&
            sender is Control content &&
            ReferenceEquals(content, _content))
        {
            AttachNativeWheelInterceptor(content);
        }

        RequestSynchronization();
    }

    private void NativeContentHandleDestroyed(object? sender, EventArgs e) =>
        DetachNativeWheelInterceptor();

    private void NativeContentKeyUp(object? sender, KeyEventArgs e) =>
        RequestSynchronization();

    private void NativeTreeViewChanged(object? sender, TreeViewEventArgs e) =>
        RequestSynchronization();

    private void NativeListViewChanged(object? sender, EventArgs e) =>
        RequestSynchronization();

    private void DescendantControlAdded(
        object? sender,
        ControlEventArgs e)
    {
        if (e.Control is Control control)
            HookContentControls(control);

        RequestSynchronization();
    }

    private void DescendantControlRemoved(
        object? sender,
        ControlEventArgs e)
    {
        if (e.Control is Control control)
            UnhookContentControls(control);

        RequestSynchronization();
    }

    private void HandleNativeWheelDelta(
        int delta,
        bool horizontal)
    {
        if (_nativeScrollAdapter is null ||
            _content is null ||
            delta == 0)
        {
            return;
        }

        ScrollByWheel(delta, horizontal);
    }

    private void ScrollByWheel(
        int delta,
        bool horizontal)
    {
        if (delta == 0 ||
            _content is null)
        {
            return;
        }

        var steps = delta / SystemInformation.MouseWheelScrollDelta;
        if (steps == 0)
            steps = Math.Sign(delta);

        if (horizontal)
        {
            if (!_horizontalScrollBar.State.CanScroll)
                return;

            SetScrollPosition(
                _horizontalScrollBar.Value +
                steps * Math.Max(
                    1,
                    _horizontalScrollBar.State.SmallChange),
                _verticalScrollBar.Value);
            return;
        }

        if (!_verticalScrollBar.State.CanScroll)
            return;

        SetScrollPosition(
            _horizontalScrollBar.Value,
            _verticalScrollBar.Value -
            steps * Math.Max(
                1,
                _verticalScrollBar.State.SmallChange));
    }

    private void AttachNativeWheelInterceptor(Control content)
    {
        if (_nativeScrollAdapter is null ||
            !content.IsHandleCreated)
        {
            return;
        }

        _nativeWheelInterceptor ??= new NativeWheelInterceptor(
            () => IsDisposed || Disposing,
            (delta, horizontal) =>
                HandleNativeWheelDelta(delta, horizontal));

        _nativeWheelInterceptor.AssignHandle(content.Handle);
    }

    private void DetachNativeWheelInterceptor()
    {
        _nativeWheelInterceptor?.ReleaseHandle();
    }

    private void RequestSynchronization()
    {
        if (IsDisposed || Disposing || _synchronizing)
            return;

        if (!IsHandleCreated)
        {
            Synchronize();
            return;
        }

        if (_synchronizationPending)
            return;

        _synchronizationPending = true;
        try
        {
            BeginInvoke(new MethodInvoker(() =>
            {
                _synchronizationPending = false;

                if (IsDisposed || Disposing)
                    return;

                Synchronize();
            }));
        }
        catch (ObjectDisposedException)
        {
            _synchronizationPending = false;
        }
        catch (InvalidOperationException)
        {
            _synchronizationPending = false;
        }
    }

    private void CancelPendingSynchronization() =>
        _synchronizationPending = false;

    private void ApplyContentLocation()
    {
        if (_content is null ||
            _nativeScrollAdapter is not null)
            return;

        _content.Location = new Point(
            -_horizontalScrollBar.Value,
            -_verticalScrollBar.Value);
    }

    private void ApplyEmptyState()
    {
        _horizontalScrollBar.SetState(
            HiveScrollState.Create(
                Orientation.Horizontal,
                0,
                0,
                0,
                Math.Max(0, _viewport.ClientSize.Width),
                enabled: false));

        _verticalScrollBar.SetState(
            HiveScrollState.Create(
                Orientation.Vertical,
                0,
                0,
                0,
                Math.Max(0, _viewport.ClientSize.Height),
                enabled: false));

        _horizontalScrollBar.Visible = false;
        _verticalScrollBar.Visible = false;

        UpdateScrollBarLayout();
    }

    private Size MeasureContentSize(Size viewportSize)
    {
        if (_content is null)
            return Size.Empty;

        if (_nativeScrollAdapter is not null)
            return new Size(
                Math.Max(0, viewportSize.Width),
                Math.Max(0, viewportSize.Height));

        var viewportWidth = Math.Max(1, viewportSize.Width);
        var currentSize = _content.Size;

        Size unboundedPreferred;
        Size viewportPreferred;

        try
        {
            unboundedPreferred = _content.GetPreferredSize(Size.Empty);
            viewportPreferred = _content.GetPreferredSize(
                new Size(viewportWidth, 0));
        }
        catch (InvalidOperationException)
        {
            unboundedPreferred = currentSize;
            viewportPreferred = currentSize;
        }

        var width = Math.Max(
            viewportSize.Width,
            Math.Max(
                _content.MinimumSize.Width,
                Math.Max(
                    currentSize.Width,
                    Math.Max(
                        unboundedPreferred.Width,
                        viewportPreferred.Width))));

        var height = Math.Max(
            viewportSize.Height,
            Math.Max(
                _content.MinimumSize.Height,
                Math.Max(
                    currentSize.Height,
                    Math.Max(
                        unboundedPreferred.Height,
                        viewportPreferred.Height))));

        if (_content.MaximumSize.Width > 0)
            width = Math.Min(width, _content.MaximumSize.Width);

        if (_content.MaximumSize.Height > 0)
            height = Math.Min(height, _content.MaximumSize.Height);

        return new Size(
            Math.Max(0, width),
            Math.Max(0, height));
    }

    private static int ResolveSmallChange(int extent) =>
        Math.Max(1, extent / 10);

    private static int ResolveLargeChange(int extent) =>
        Math.Max(1, extent - Math.Max(1, extent / 10));

    private void UpdateScrollBarLayout()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        var verticalWidth = _verticalScrollBar.Visible
            ? _verticalScrollBar.GetPreferredSize(Size.Empty).Width
            : 0;

        var horizontalHeight = _horizontalScrollBar.Visible
            ? _horizontalScrollBar.GetPreferredSize(Size.Empty).Height
            : 0;

        var horizontalBounds = new Rectangle(
            0,
            Math.Max(0, ClientSize.Height - horizontalHeight),
            Math.Max(0, ClientSize.Width - verticalWidth),
            horizontalHeight);

        var verticalBounds = new Rectangle(
            Math.Max(0, ClientSize.Width - verticalWidth),
            0,
            verticalWidth,
            Math.Max(0, ClientSize.Height - horizontalHeight));

        _horizontalScrollBar.SetBounds(
            horizontalBounds.X,
            horizontalBounds.Y,
            horizontalBounds.Width,
            horizontalBounds.Height);
        _verticalScrollBar.SetBounds(
            verticalBounds.X,
            verticalBounds.Y,
            verticalBounds.Width,
            verticalBounds.Height);

        if (_horizontalScrollBar.Visible)
            _horizontalScrollBar.BringToFront();

        if (_verticalScrollBar.Visible)
            _verticalScrollBar.BringToFront();
    }

    private void NotifyScrollPositionChanged()
    {
        var current = new Point(
            _horizontalScrollBar.Value,
            _verticalScrollBar.Value);

        if (current == _lastReportedPosition)
            return;

        _lastReportedPosition = current;
        ScrollPositionChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private sealed record ContentPresentation(
        DockStyle Dock,
        AnchorStyles Anchor,
        Point Location,
        Size Size,
        bool AutoSize,
        bool? AutoScroll)
    {
        public static ContentPresentation Capture(
            Control content)
        {
            return new ContentPresentation(
                content.Dock,
                content.Anchor,
                content.Location,
                content.Size,
                content.AutoSize,
                content is ScrollableControl scrollable
                    ? scrollable.AutoScroll
                    : null);
        }

        public void Restore(Control content)
        {
            if (AutoScroll is bool autoScroll &&
                content is ScrollableControl scrollable)
            {
                scrollable.AutoScroll = autoScroll;
            }

            content.AutoSize = AutoSize;
            content.Size = Size;
            content.Location = Location;
            content.Anchor = Anchor;
            content.Dock = Dock;
        }
    }
    private sealed class NativeWheelInterceptor : NativeWindow, IDisposable
    {
        private const int WmMouseWheel = 0x020A;
        private const int WmMouseHWheel = 0x020E;
        private const int MkShift = 0x0004;

        private readonly Func<bool> _isDisposed;
        private readonly Action<int, bool> _wheel;
        private int _verticalRemainder;
        private int _horizontalRemainder;

        public NativeWheelInterceptor(
            Func<bool> isDisposed,
            Action<int, bool> wheel)
        {
            _isDisposed = isDisposed;
            _wheel = wheel;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel ||
                m.Msg == WmMouseHWheel)
            {
                if (_isDisposed())
                    return;

                var delta = unchecked(
                    (short)((m.WParam.ToInt64() >> 16) & 0xFFFF));

                var horizontal =
                    m.Msg == WmMouseHWheel ||
                    (m.Msg == WmMouseWheel &&
                     (((int)m.WParam.ToInt64() & MkShift) != 0));

                var steps = horizontal
                    ? ConsumeWheelDelta(
                        ref _horizontalRemainder,
                        delta)
                    : ConsumeWheelDelta(
                        ref _verticalRemainder,
                        delta);

                if (steps != 0)
                    _wheel(
                        steps * SystemInformation.MouseWheelScrollDelta,
                        horizontal);

                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose() =>
            ReleaseHandle();
    }

    private static int ConsumeWheelDelta(
        ref int remainder,
        int delta)
    {
        remainder += delta;

        var steps = remainder / SystemInformation.MouseWheelScrollDelta;
        remainder %= SystemInformation.MouseWheelScrollDelta;

        return steps;
    }
}
