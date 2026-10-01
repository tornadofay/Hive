using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public readonly record struct HiveScrollState(
    int Minimum,
    int Maximum,
    int Value,
    int ViewportSize,
    int SmallChange,
    int LargeChange,
    bool Enabled,
    Orientation Orientation)
{
    public int EffectiveMaximum =>
        Minimum +
        Math.Max(
            0,
            (Maximum - Minimum) - Math.Max(0, ViewportSize));

    public bool CanScroll =>
        Enabled && EffectiveMaximum > Minimum;

    public int ContentExtent => Math.Max(0, Maximum - Minimum);

    public static HiveScrollState Create(
        Orientation orientation,
        int minimum,
        int maximum,
        int value,
        int viewportSize,
        int smallChange = 16,
        int largeChange = 64,
        bool enabled = true)
    {
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                maximum,
                "Maximum cannot be less than Minimum.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(viewportSize);
        ArgumentOutOfRangeException.ThrowIfNegative(smallChange);
        ArgumentOutOfRangeException.ThrowIfNegative(largeChange);

        var effectiveMaximum =
            minimum +
            Math.Max(0, (maximum - minimum) - viewportSize);

        return new HiveScrollState(
            minimum,
            maximum,
            Math.Clamp(value, minimum, effectiveMaximum),
            viewportSize,
            smallChange,
            largeChange,
            enabled,
            orientation);
    }

    internal HiveScrollState Normalize()
    {
        if (Maximum < Minimum)
        {
            throw new InvalidOperationException(
                "Hive scroll state maximum cannot be less than minimum.");
        }

        if (ViewportSize < 0)
        {
            throw new InvalidOperationException(
                "Hive scroll state viewport size cannot be negative.");
        }

        if (SmallChange < 0 || LargeChange < 0)
        {
            throw new InvalidOperationException(
                "Hive scroll change values cannot be negative.");
        }

        var effectiveMaximum = EffectiveMaximum;

        return this with
        {
            Value = Math.Clamp(Value, Minimum, effectiveMaximum)
        };
    }
}

public sealed class HiveScrollBar : Control
{
    private const int DefaultThickness = 10;
    private const int DefaultMinimumThumbSize = 20;
    private const int DefaultCornerRadius = 5;

    private HiveThemeDefinition? _theme;
    private HiveScrollState _state = HiveScrollState.Create(
        Orientation.Vertical,
        0,
        0,
        0,
        0,
        smallChange: 16,
        largeChange: 64,
        enabled: false);

    private bool _hovered;
    private bool _pressed;
    private bool _dragging;
    private int _dragOffset;
    private Rectangle _lastThumbBounds;
    private SolidBrush? _trackBrush;
    private SolidBrush? _thumbBrush;
    private SolidBrush? _thumbHoverBrush;
    private SolidBrush? _thumbPressedBrush;
    private Pen? _focusPen;
    private int _thickness = DefaultThickness;
    private int _minimumThumbSize = DefaultMinimumThumbSize;

    public HiveScrollBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        TabStop = true;
        AccessibleRole = AccessibleRole.ScrollBar;
        AccessibleName = "Vertical scroll bar";
        AccessibleDescription =
            "Scroll the associated content vertically.";
        Cursor = Cursors.Default;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        BackColor = Color.Transparent;
        Size = new Size(
            LogicalToDevice(DefaultThickness),
            LogicalToDevice(64));
        UpdateCursor();
    }

    [DefaultValue(typeof(Orientation), "Vertical")]
    public Orientation Orientation
    {
        get => _state.Orientation;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new InvalidEnumArgumentException(
                    nameof(value),
                    (int)value,
                    typeof(Orientation));
            }

            if (_state.Orientation == value)
                return;

            var wasFocused = Focused;
            _state = _state with { Orientation = value };
            AccessibleName = value == Orientation.Vertical
                ? "Vertical scroll bar"
                : "Horizontal scroll bar";
            AccessibleDescription = value == Orientation.Vertical
                ? "Scroll the associated content vertically."
                : "Scroll the associated content horizontally.";

            _pressed = false;
            _dragging = false;
            _hovered = false;
            UpdateCursor();
            Invalidate();

            if (wasFocused && CanFocus)
                Focus();
        }
    }

    [DefaultValue(DefaultThickness)]
    public int Thickness
    {
        get => _thickness;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_thickness == value)
                return;

            _thickness = value;
            Invalidate();
        }
    }

    [DefaultValue(DefaultMinimumThumbSize)]
    public int MinimumThumbSize
    {
        get => _minimumThumbSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_minimumThumbSize == value)
                return;

            _minimumThumbSize = value;
            InvalidateThumbTransition();
        }
    }

    public HiveScrollState State => _state;

    public int Value => _state.Value;

    public bool CanScroll => Enabled && _state.CanScroll;

    public event EventHandler? ValueChanged;

    public void SetState(HiveScrollState state)
    {
        var normalized = state.Normalize();

        if (normalized.Orientation != Orientation)
            normalized = normalized with { Orientation = Orientation };

        var changed = normalized != _state;
        var oldThumb = GetThumbBounds(_state);

        _state = normalized;

        if (!normalized.CanScroll)
        {
            _hovered = false;
            _pressed = false;
            _dragging = false;
            Capture = false;
        }

        if (Enabled != normalized.Enabled)
            Enabled = normalized.Enabled;

        UpdateCursor();

        if (!changed)
            return;

        var newThumb = GetThumbBounds(_state);
        InvalidateUnion(oldThumb, newThumb);
        InvalidateFocusState();

        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetValue(int value)
    {
        var normalized = Math.Clamp(
            value,
            _state.Minimum,
            _state.EffectiveMaximum);

        if (normalized == _state.Value)
            return;

        SetState(_state with { Value = normalized });
    }

    public void ScrollBy(int delta)
    {
        if (!_state.CanScroll || delta == 0)
            return;

        var target = _state.Value;
        if (delta > 0)
        {
            target = Math.Min(
                _state.EffectiveMaximum,
                target + delta);
        }
        else
        {
            target = Math.Max(
                _state.Minimum,
                target + delta);
        }

        SetValue(target);
    }

    internal Rectangle GetThumbBoundsForTesting() => GetThumbBounds(_state);

    internal Rectangle GetTrackBoundsForTesting() => GetTrackBounds();

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);

        if (!Enabled)
            return;

        _hovered = true;
        InvalidateThumbTransition();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_dragging)
            return;

        if (!_hovered)
            return;

        _hovered = false;
        InvalidateThumbTransition();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left ||
            !Enabled ||
            !_state.CanScroll)
            return;

        if (!Focused && CanFocus)
            Focus();

        var thumb = GetThumbBounds(_state);

        if (thumb.Contains(e.Location))
        {
            _pressed = true;
            _dragging = true;
            _dragOffset = GetAxisCoordinate(e.Location) -
                           GetAxisStart(thumb);
            Capture = true;
            UpdateCursor();
            InvalidateThumbTransition();
            return;
        }

        var track = GetTrackBounds();

        if (!track.Contains(e.Location))
            return;

        var coordinate = GetAxisCoordinate(e.Location);
        var thumbStart = GetAxisStart(thumb);
        var thumbEnd = GetAxisEnd(thumb);

        if (coordinate < thumbStart)
        {
            SetValue(_state.Value - EffectiveLargeChange());
        }
        else if (coordinate > thumbEnd)
        {
            SetValue(_state.Value + EffectiveLargeChange());
        }

        _pressed = true;
        UpdateCursor();
        InvalidateThumbTransition();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging &&
            _pressed &&
            Capture)
        {
            UpdateDragValue(e.Location);
            return;
        }

        if (!ClientRectangle.Contains(e.Location))
            return;

        if (_state.CanScroll &&
            GetThumbBounds(_state).Contains(e.Location))
        {
            if (!_hovered)
            {
                _hovered = true;
                InvalidateThumbTransition();
            }

            return;
        }

        if (!_hovered)
            return;

        _hovered = false;
        InvalidateThumbTransition();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        var wasPressed = _pressed;
        _pressed = false;
        _dragging = false;
        Capture = false;
        UpdateCursor();

        if (wasPressed)
            InvalidateThumbTransition();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!Enabled || !_state.CanScroll)
            return;

        var steps = e.Delta / SystemInformation.MouseWheelScrollDelta;
        if (steps == 0)
            steps = Math.Sign(e.Delta);

        ScrollBy(-steps * EffectiveSmallChange());
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!Enabled || !_state.CanScroll)
            return;

        var handled = true;

        switch (e.KeyCode)
        {
            case Keys.Up when Orientation == Orientation.Vertical:
            case Keys.Left when Orientation == Orientation.Horizontal:
                ScrollBy(-EffectiveSmallChange());
                break;

            case Keys.Down when Orientation == Orientation.Vertical:
            case Keys.Right when Orientation == Orientation.Horizontal:
                ScrollBy(EffectiveSmallChange());
                break;

            case Keys.PageUp:
                ScrollBy(-EffectiveLargeChange());
                break;

            case Keys.PageDown:
                ScrollBy(EffectiveLargeChange());
                break;

            case Keys.Home:
                SetValue(_state.Minimum);
                break;

            case Keys.End:
                SetValue(_state.EffectiveMaximum);
                break;

            default:
                handled = false;
                break;
        }

        if (handled)
            e.Handled = true;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);

        if (!Enabled)
        {
            _hovered = false;
            _pressed = false;
            _dragging = false;
            Capture = false;
        }

        UpdateCursor();
        InvalidateThumbTransition();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);

        if (Capture)
            return;

        if (_dragging || _pressed)
        {
            _dragging = false;
            _pressed = false;
            UpdateCursor();
            InvalidateThumbTransition();
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Invalidate();
    }

    protected override Size GetPreferredSizeCore(Size proposedSize)
    {
        var thickness = LogicalToDevice(_thickness);
        var length = LogicalToDevice(64);

        if (Orientation == Orientation.Vertical)
        {
            return new Size(
                thickness,
                proposedSize.Height > 0
                    ? proposedSize.Height
                    : length);
        }

        return new Size(
            proposedSize.Width > 0
                ? proposedSize.Width
                : length,
            thickness);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var track = GetTrackBounds();
        if (track.Width <= 0 || track.Height <= 0)
            return;

        var theme = _theme;
        if (theme is null)
            return;

        var radius = Math.Min(
            LogicalToDevice(DefaultCornerRadius),
            Math.Min(track.Width, track.Height) / 2);

        using var trackPath = CreateRoundedPath(track, radius);
        var trackBrush = _trackBrush;

        if (trackBrush is not null)
            e.Graphics.FillPath(trackBrush, trackPath);

        if (!CanScroll)
            return;

        var thumb = GetThumbBounds(_state);
        if (thumb.Width <= 0 || thumb.Height <= 0)
            return;

        var thumbRadius = Math.Min(
            radius,
            Math.Min(thumb.Width, thumb.Height) / 2);

        using var thumbPath = CreateRoundedPath(
            thumb,
            Math.Max(1, thumbRadius));

        var thumbBrush =
            _pressed
                ? _thumbPressedBrush
                : _hovered
                    ? _thumbHoverBrush
                    : _thumbBrush;

        if (thumbBrush is not null)
            e.Graphics.FillPath(thumbBrush, thumbPath);

        if (Focused &&
            Enabled &&
            _focusPen is not null)
        {
            var focus = Rectangle.Inflate(
                thumb,
                -1,
                -1);

            if (focus.Width > 0 && focus.Height > 0)
                e.Graphics.DrawRectangle(
                    _focusPen,
                    focus.Left,
                    focus.Top,
                    focus.Width - 1,
                    focus.Height - 1);
        }

        _lastThumbBounds = thumb;
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _theme = theme;
        RebuildPaintResources(theme);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Capture = false;
            DisposePaintResources();
        }

        base.Dispose(disposing);
    }

    private void UpdateDragValue(Point point)
    {
        var track = GetTrackBounds();
        var thumb = GetThumbBounds(_state);
        var travel = GetAxisLength(track) -
                     GetAxisLength(thumb);

        if (travel <= 0)
            return;

        var desiredStart =
            GetAxisCoordinate(point) -
            _dragOffset;

        var trackStart = GetAxisStart(track);
        var maxStart = GetAxisEnd(track) -
                       GetAxisLength(thumb);

        var clampedStart = Math.Clamp(
            desiredStart,
            trackStart,
            maxStart);

        var target = HiveScrollMetrics.CalculateValueFromThumbPosition(
            clampedStart,
            trackStart,
            GetAxisLength(track),
            GetAxisLength(thumb),
            _state.Minimum,
            _state.EffectiveMaximum);

        SetValue(target);
    }

    private int EffectiveSmallChange() =>
        Math.Max(
            1,
            _state.SmallChange > 0
                ? _state.SmallChange
                : Math.Max(1, _state.ViewportSize / 10));

    private int EffectiveLargeChange() =>
        Math.Max(
            1,
            _state.LargeChange > 0
                ? _state.LargeChange
                : Math.Max(1, _state.ViewportSize));

    private Rectangle GetTrackBounds()
    {
        var inset = Math.Max(
            1,
            LogicalToDevice(1));

        return new Rectangle(
            inset,
            inset,
            Math.Max(0, ClientSize.Width - inset * 2),
            Math.Max(0, ClientSize.Height - inset * 2));
    }

    private Rectangle GetThumbBounds(HiveScrollState state)
    {
        if (!CanScroll || !state.CanScroll)
            return Rectangle.Empty;

        var track = GetTrackBounds();
        var trackLength = GetAxisLength(track);
        if (trackLength <= 0)
            return Rectangle.Empty;

        var contentExtent = state.ContentExtent;
        var viewport = Math.Max(0, state.ViewportSize);
        var minimumThumb = LogicalToDevice(_minimumThumbSize);

        var thumbLength = HiveScrollMetrics.CalculateThumbLength(
            trackLength,
            contentExtent,
            viewport,
            minimumThumb);

        var start = HiveScrollMetrics.CalculateThumbPosition(
            GetAxisStart(track),
            trackLength,
            thumbLength,
            state.Minimum,
            state.EffectiveMaximum,
            state.Value);

        return Orientation == Orientation.Vertical
            ? new Rectangle(
                track.Left,
                start,
                track.Width,
                thumbLength)
            : new Rectangle(
                start,
                track.Top,
                thumbLength,
                track.Height);
    }

    private int GetAxisCoordinate(Point point) =>
        Orientation == Orientation.Vertical
            ? point.Y
            : point.X;

    private int GetAxisStart(Rectangle rectangle) =>
        Orientation == Orientation.Vertical
            ? rectangle.Top
            : rectangle.Left;

    private int GetAxisEnd(Rectangle rectangle) =>
        Orientation == Orientation.Vertical
            ? rectangle.Bottom
            : rectangle.Right;

    private int GetAxisLength(Rectangle rectangle) =>
        Orientation == Orientation.Vertical
            ? rectangle.Height
            : rectangle.Width;

    private void InvalidateThumbTransition()
    {
        var current = GetThumbBounds(_state);
        InvalidateUnion(_lastThumbBounds, current);
    }

    private void InvalidateUnion(
        Rectangle first,
        Rectangle second)
    {
        if (first.IsEmpty && second.IsEmpty)
        {
            Invalidate();
            return;
        }

        if (first.IsEmpty)
        {
            Invalidate(second);
            return;
        }

        if (second.IsEmpty)
        {
            Invalidate(first);
            return;
        }

        Invalidate(Rectangle.Union(first, second));
    }

    private void InvalidateFocusState() =>
        Invalidate(_lastThumbBounds);

    private void UpdateCursor()
    {
        Cursor =
            _dragging
                ? Orientation == Orientation.Vertical
                    ? Cursors.SizeNS
                    : Cursors.SizeWE
                : _state.CanScroll
                    ? Orientation == Orientation.Vertical
                        ? Cursors.SizeNS
                        : Cursors.SizeWE
                    : Cursors.Default;
    }

    private int LogicalToDevice(int value)
    {
        var dpi = DeviceDpi;
        return Math.Max(
            1,
            (int)Math.Round(
                value * (dpi / 96f),
                MidpointRounding.AwayFromZero));
    }

    private static GraphicsPath CreateRoundedPath(
        Rectangle bounds,
        int radius)
    {
        radius = Math.Max(
            1,
            Math.Min(
                radius,
                Math.Min(bounds.Width, bounds.Height) / 2));

        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(
            bounds.X,
            bounds.Y,
            diameter,
            diameter,
            180f,
            90f);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Y,
            diameter,
            diameter,
            270f,
            90f);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0f,
            90f);
        path.AddArc(
            bounds.X,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            90f,
            90f);
        path.CloseFigure();

        return path;
    }

    private void RebuildPaintResources(HiveThemeDefinition theme)
    {
        DisposePaintResources();

        _trackBrush = new SolidBrush(
            theme.Palette.ElevatedSurface);

        _thumbBrush = new SolidBrush(
            theme.Palette.Border);

        _thumbHoverBrush = new SolidBrush(
            theme.VisualStates.NavigationHover);

        _thumbPressedBrush = new SolidBrush(
            theme.Palette.Accent);

        _focusPen = new Pen(
            theme.VisualStates.FocusedBorder);
    }

    private void DisposePaintResources()
    {
        _trackBrush?.Dispose();
        _thumbBrush?.Dispose();
        _thumbHoverBrush?.Dispose();
        _thumbPressedBrush?.Dispose();
        _focusPen?.Dispose();

        _trackBrush = null;
        _thumbBrush = null;
        _thumbHoverBrush = null;
        _thumbPressedBrush = null;
        _focusPen = null;
    }
}
