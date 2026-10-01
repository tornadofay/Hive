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

        if (content is ScrollableControl scrollable)
            scrollable.AutoScroll = false;

        content.Dock = DockStyle.None;
        content.Anchor = AnchorStyles.Top | AnchorStyles.Left;

        _viewport.Controls.Add(content);

        HookContentControls(content);

        content.Resize += ContentChanged;
        content.Layout += ContentChanged;
        content.SizeChanged += ContentChanged;

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

        UnhookContentControls(content);
        _viewport.Controls.Remove(content);

        var presentation = _contentPresentation;
        if (presentation is not null)
            presentation.Restore(content);

        _content = null;
        _contentPresentation = null;

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

            _horizontalScrollBar.SetValue(horizontalValue);
            _verticalScrollBar.SetValue(verticalValue);

            ApplyContentLocation();
            NotifyScrollPositionChanged();
        }
        finally
        {
            _scrolling = false;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        UpdateScrollBarLayout();
        Synchronize();
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

            var content = _content;
            if (content is not null)
            {
                content.Resize -= ContentChanged;
                content.Layout -= ContentChanged;
                content.SizeChanged -= ContentChanged;
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
            ApplyContentLocation();
            NotifyScrollPositionChanged();
        }
        finally
        {
            _scrolling = false;
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
            _scrolling ||
            !_verticalScrollBar.State.CanScroll)
            return;

        if (sender is TextBoxBase ||
            sender is ListControl ||
            sender is DataGridView ||
            sender is TreeView)
            return;

        var steps = e.Delta / SystemInformation.MouseWheelScrollDelta;
        if (steps == 0)
            steps = Math.Sign(e.Delta);

        SetScrollPosition(
            _horizontalScrollBar.Value,
            _verticalScrollBar.Value -
            steps * Math.Max(
                1,
                _verticalScrollBar.State.SmallChange));
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
        if (_content is null)
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
}
