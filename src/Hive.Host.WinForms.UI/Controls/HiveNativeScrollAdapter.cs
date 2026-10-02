using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveNativeScrollAdapter
{
    private const int SbHorizontal = 0;
    private const int SbVertical = 1;

    private const int WmHorizontalScroll = 0x0114;
    private const int WmVerticalScroll = 0x0115;

    private const int LvmScroll = 0x1014;

    private const int SbThumbPosition = 4;

    private const uint SiRange = 0x0001;
    private const uint SiPage = 0x0002;
    private const uint SiPos = 0x0004;
    private const uint SiAll = SiRange | SiPage | SiPos;

    private readonly Dictionary<Orientation, HiveScrollState> _lastKnownStates = new();

    public static bool Supports(Control control) =>
        control is TextBoxBase or TreeView or ListView;

    public HiveScrollState ReadState(
        Control control,
        Orientation orientation) =>
        ReadState(control, orientation, out _);

    public HiveScrollState ReadState(
        Control control,
        Orientation orientation,
        out bool authoritative)
    {
        ArgumentNullException.ThrowIfNull(control);

        authoritative = false;

        if (!Supports(control) ||
            !control.IsHandleCreated)
        {
            return HiveScrollState.Create(
                orientation,
                0,
                0,
                0,
                0,
                enabled: false);
        }

        var bar = orientation == Orientation.Vertical
            ? SbVertical
            : SbHorizontal;

        if (control is ListView listView &&
            orientation == Orientation.Vertical &&
            listView.View == View.Details)
        {
            return ReadListViewVerticalState(
                listView,
                orientation,
                out authoritative);
        }


        var info = new ScrollInfo
        {
            cbSize = Marshal.SizeOf<ScrollInfo>(),
            fMask = SiAll
        };

        var hasScrollInfo = GetScrollInfo(
            control.Handle,
            bar,
            ref info);

        var minimum = hasScrollInfo
            ? info.nMin
            : 0;
        var page = hasScrollInfo
            ? Math.Max(0, ClampToInt(info.nPage))
            : 0;
        var maximum = hasScrollInfo
            ? info.nMax
            : 0;
        var position = hasScrollInfo
            ? info.nPos
            : 0;

        // Once a native scrollbar has been suppressed, some controls can report a
        // zero page size. A previously authoritative Hive state must remain the
        // source of truth in that case rather than being replaced by a synthetic
        // fallback range.
        if (page <= 0)
        {
            var cached = GetCachedState(
                orientation,
                hasScrollInfo ? info.nPos : null,
                out var cachedAuthoritative);

            if (cachedAuthoritative)
            {
                authoritative = true;
                return cached;
            }

            // On first acquisition, some native controls expose the range/position
            // through the standard compatibility APIs even when SCROLLINFO.nPage is
            // unavailable. Derive the viewport from the actual hosted control size.
            var hasRange = GetScrollRange(
                control.Handle,
                bar,
                out minimum,
                out maximum);

            if (hasRange &&
                maximum >= minimum)
            {
                position = GetScrollPosition(
                    control.Handle,
                    bar);

                page = orientation == Orientation.Vertical
                    ? Math.Max(1, control.ClientSize.Height)
                    : Math.Max(1, control.ClientSize.Width);

                hasScrollInfo = true;
            }
        }

        if (hasScrollInfo &&
            maximum >= minimum &&
            page > 0)
        {
            var effectiveMaximum = Math.Max(
                minimum,
                maximum - page + 1);

            var viewportSize = page;
            var extent = Math.Max(
                viewportSize,
                effectiveMaximum - minimum + viewportSize);

            var enabled = effectiveMaximum > minimum;

            var state = HiveScrollState.Create(
                orientation,
                minimum,
                minimum + extent,
                position,
                viewportSize,
                smallChange: Math.Max(1, viewportSize / 10),
                largeChange: Math.Max(1, viewportSize),
                enabled: enabled);

            _lastKnownStates[orientation] = state;
            authoritative = true;
            return state;
        }

        return GetCachedState(
            orientation,
            position,
            out authoritative);
    }

    private HiveScrollState GetCachedState(
        Orientation orientation,
        int? nativePosition) =>
        GetCachedState(
            orientation,
            nativePosition,
            out _);

    private HiveScrollState GetCachedState(
        Orientation orientation,
        int? nativePosition,
        out bool authoritative)
    {
        if (!_lastKnownStates.TryGetValue(orientation, out var state))
        {
            authoritative = false;
            return HiveScrollState.Create(
                orientation,
                0,
                0,
                0,
                0,
                enabled: false);
        }

        authoritative = true;

        return nativePosition is int position
            ? state with
            {
                Value = Math.Clamp(
                    position,
                    state.Minimum,
                    state.EffectiveMaximum)
            }
            : state;
    }

    public void SetPosition(
        Control control,
        Orientation orientation,
        int value)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!Supports(control) ||
            !control.IsHandleCreated)
        {
            return;
        }

        var bar = orientation == Orientation.Vertical
            ? SbVertical
            : SbHorizontal;

        var cached = GetCachedState(orientation, null);

        if (control is ListView listView &&
            orientation == Orientation.Vertical &&
            listView.View == View.Details)
        {
            if (!cached.CanScroll)
            {
                cached = ReadListViewVerticalState(
                    listView,
                    orientation,
                    out var authoritative);

                if (!authoritative)
                {
                    HideNativeScrollBars(control);
                    return;
                }
            }

            var target = Math.Clamp(
                value,
                cached.Minimum,
                cached.EffectiveMaximum);

            SetListViewPosition(
                listView,
                orientation,
                cached.Value,
                target);

            _lastKnownStates[orientation] = cached with
            {
                Value = target
            };

            HideNativeScrollBar(control.Handle, bar);
            return;
        }

        var info = new ScrollInfo
        {
            cbSize = Marshal.SizeOf<ScrollInfo>(),
            fMask = SiAll
        };

        var hasNativeInfo = GetScrollInfo(
            control.Handle,
            bar,
            ref info);

        var page = hasNativeInfo
            ? Math.Max(0, ClampToInt(info.nPage))
            : 0;

        if (!hasNativeInfo || page <= 0)
        {
            if (!cached.CanScroll)
            {
                HideNativeScrollBars(control);
                return;
            }

            info.nMin = cached.Minimum;
            info.nMax = cached.Maximum - 1;
            info.nPage = (uint)Math.Max(0, cached.ViewportSize);
            info.nPos = cached.Value;
        }

        var maximumPosition = Math.Max(
            info.nMin,
            info.nMax - Math.Max(1, ClampToInt(info.nPage)) + 1);

        var nativeTarget = Math.Clamp(
            value,
            info.nMin,
            maximumPosition);

        if (control is ListView nativeListView)
        {
            SetListViewPosition(
                nativeListView,
                orientation,
                info.nPos,
                nativeTarget);
        }
        else
        {
            info.fMask = SiPos;
            info.nPos = nativeTarget;

            SetScrollInfo(
                control.Handle,
                bar,
                ref info,
                false);

            var message = orientation == Orientation.Vertical
                ? WmVerticalScroll
                : WmHorizontalScroll;

            var wParam = MakeWParam(
                (ushort)SbThumbPosition,
                (ushort)Math.Clamp(nativeTarget, 0, ushort.MaxValue));

            SendMessage(
                control.Handle,
                message,
                wParam,
                IntPtr.Zero);
        }

        _lastKnownStates[orientation] = cached with
        {
            Value = Math.Clamp(
                nativeTarget,
                cached.Minimum,
                cached.EffectiveMaximum)
        };
        HideNativeScrollBar(control.Handle, bar);
    }

    public void HideNativeScrollBars(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!Supports(control) ||
            !control.IsHandleCreated)
        {
            return;
        }

        if (control is HiveListView hiveListView)
        {
            hiveListView.SuppressNativeScrollBars();
            return;
        }

        HideNativeScrollBar(control.Handle, SbHorizontal);
        HideNativeScrollBar(control.Handle, SbVertical);
    }

    private HiveScrollState ReadListViewVerticalState(
        ListView listView,
        Orientation orientation,
        out bool authoritative)
    {
        authoritative = false;

        var lineHeight = ResolveListViewLineHeight(listView);
        var itemCount = listView.Items.Count;
        var viewportSize = Math.Max(0, listView.ClientSize.Height);

        if (lineHeight <= 0 ||
            viewportSize <= 0)
        {
            return GetCachedState(
                orientation,
                null,
                out authoritative);
        }

        var contentExtent = Math.Max(
            viewportSize,
            (int)Math.Min(
                int.MaxValue,
                (long)itemCount * lineHeight));

        var effectiveMaximum = Math.Max(
            0,
            contentExtent - viewportSize);

        var topIndex = Math.Max(
            0,
            listView.TopItem?.Index ?? 0);
        var value = (int)Math.Clamp(
            (long)topIndex * lineHeight,
            0,
            effectiveMaximum);

        var state = HiveScrollState.Create(
            orientation,
            0,
            contentExtent,
            value,
            viewportSize,
            smallChange: lineHeight,
            largeChange: Math.Max(lineHeight, viewportSize),
            enabled: effectiveMaximum > 0);

        _lastKnownStates[orientation] = state;
        authoritative = true;
        return state;
    }

    private static void SetListViewPosition(
        ListView listView,
        Orientation orientation,
        int current,
        int target)
    {
        var lineHeight = ResolveListViewLineHeight(listView);

        if (orientation == Orientation.Vertical)
        {
            var currentTopIndex = Math.Max(
                0,
                listView.TopItem?.Index ?? 0);
            var targetTopIndex = Math.Max(
                0,
                (int)Math.Round(
                    target / (double)lineHeight,
                    MidpointRounding.AwayFromZero));

            var deltaRows = targetTopIndex - currentTopIndex;
            if (deltaRows == 0)
                return;

            var deltaPixels = Math.Clamp(
                (long)deltaRows * lineHeight,
                int.MinValue,
                int.MaxValue);

            SendMessage(
                listView.Handle,
                LvmScroll,
                IntPtr.Zero,
                new IntPtr(deltaPixels));
            return;
        }

        var delta = target - current;
        if (delta == 0)
            return;

        SendMessage(
            listView.Handle,
            LvmScroll,
            new IntPtr(delta),
            IntPtr.Zero);
    }

    private static int ResolveListViewLineHeight(ListView listView)
    {
        if (listView.Items.Count > 0)
        {
            var bounds = listView.GetItemRect(0);
            if (bounds.Height > 0)
                return bounds.Height;
        }

        return Math.Max(1, listView.Font.Height);
    }

    private static int ClampToInt(uint value) =>
        value > int.MaxValue
            ? int.MaxValue
            : (int)value;

    private static IntPtr MakeWParam(
        ushort low,
        ushort high) =>
        new(
            (long)(((uint)high << 16) | low));

    private static void HideNativeScrollBar(
        IntPtr handle,
        int bar)
    {
        ShowScrollBar(
            handle,
            bar,
            false);
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool GetScrollInfo(
        IntPtr hWnd,
        int nBar,
        ref ScrollInfo lpScrollInfo);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool GetScrollRange(
        IntPtr hWnd,
        int nBar,
        out int lpMinPos,
        out int lpMaxPos);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern int GetScrollPos(
        IntPtr hWnd,
        int nBar);

    private static int GetScrollPosition(
        IntPtr handle,
        int bar) =>
        Math.Max(
            0,
            GetScrollPos(handle, bar));

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern int SetScrollInfo(
        IntPtr hWnd,
        int nBar,
        [In] ref ScrollInfo lpsi,
        bool redraw);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool ShowScrollBar(
        IntPtr hWnd,
        int wBar,
        bool bShow);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        public int cbSize;
        public uint fMask;
        public int nMin;
        public int nMax;
        public uint nPage;
        public int nPos;
        public int nTrackPos;
    }
}
