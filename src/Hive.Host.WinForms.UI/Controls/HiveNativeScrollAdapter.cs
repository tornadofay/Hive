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
        Orientation orientation)
    {
        ArgumentNullException.ThrowIfNull(control);

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

        var info = new ScrollInfo
        {
            cbSize = Marshal.SizeOf<ScrollInfo>(),
            fMask = SiAll
        };

        if (!GetScrollInfo(control.Handle, bar, ref info))
            return GetCachedState(orientation, null);

        var minimum = info.nMin;
        var page = Math.Max(0, ClampToInt(info.nPage));
        var maximum = info.nMax;

        if (maximum < minimum)
            return GetCachedState(orientation, null);

        // Once a native scrollbar is hidden, some Win32 controls can temporarily
        // report a zero page size even though their underlying scroll range is still
        // valid. Keep the last authoritative range/viewport rather than collapsing
        // the Hive scrollbar during that transient native state.
        if (page <= 0)
            return GetCachedState(orientation, info.nPos);

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
            info.nPos,
            viewportSize,
            smallChange: Math.Max(1, viewportSize / 10),
            largeChange: Math.Max(1, viewportSize),
            enabled: enabled);

        _lastKnownStates[orientation] = state;
        return state;
    }

    private HiveScrollState GetCachedState(
        Orientation orientation,
        int? nativePosition) =>
        _lastKnownStates.TryGetValue(orientation, out var state)
            ? nativePosition is int position
                ? state with
                {
                    Value = Math.Clamp(
                        position,
                        state.Minimum,
                        state.EffectiveMaximum)
                }
                : state
            : HiveScrollState.Create(
                orientation,
                0,
                0,
                0,
                0,
                enabled: false);

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

        var info = new ScrollInfo
        {
            cbSize = Marshal.SizeOf<ScrollInfo>(),
            fMask = SiAll
        };

        var hasNativeInfo = GetScrollInfo(
            control.Handle,
            bar,
            ref info);

        var cached = GetCachedState(orientation, null);
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

        var target = Math.Clamp(
            value,
            info.nMin,
            maximumPosition);

        if (control is ListView listView)
        {
            SetListViewPosition(
                listView,
                orientation,
                info.nPos,
                target);
        }
        else
        {
            info.fMask = SiPos;
            info.nPos = target;

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
                (ushort)Math.Clamp(target, 0, ushort.MaxValue));

            SendMessage(
                control.Handle,
                message,
                wParam,
                IntPtr.Zero);
        }

        _lastKnownStates[orientation] = cached with
        {
            Value = Math.Clamp(
                target,
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

    private static void SetListViewPosition(
        ListView listView,
        Orientation orientation,
        int current,
        int target)
    {
        if (current == target)
            return;

        if (orientation == Orientation.Vertical)
        {
            var lineHeight = ResolveListViewLineHeight(listView);
            var quantizedTarget = Math.Max(
                0,
                (int)Math.Round(
                    target / (double)lineHeight,
                    MidpointRounding.AwayFromZero) * lineHeight);

            target = quantizedTarget;
        }

        var delta = target - current;
        if (delta == 0)
            return;

        var horizontalDelta = orientation == Orientation.Horizontal
            ? delta
            : 0;
        var verticalDelta = orientation == Orientation.Vertical
            ? delta
            : 0;

        SendMessage(
            listView.Handle,
            LvmScroll,
            new IntPtr(horizontalDelta),
            new IntPtr(verticalDelta));
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
