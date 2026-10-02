using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveNativeScrollAdapter
{
    private const int SbHorizontal = 0;
    private const int SbVertical = 1;

    private const int WmHorizontalScroll = 0x0114;
    private const int WmVerticalScroll = 0x0115;

    private const int SbThumbPosition = 4;

    private const uint SiRange = 0x0001;
    private const uint SiPage = 0x0002;
    private const uint SiPos = 0x0004;
    private const uint SiAll = SiRange | SiPage | SiPos;

    public static bool Supports(Control control) =>
        control is TextBoxBase or TreeView;

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
        {
            return HiveScrollState.Create(
                orientation,
                0,
                0,
                0,
                0,
                enabled: false);
        }

        var minimum = info.nMin;
        var page = Math.Max(0, ClampToInt(info.nPage));
        var maximum = info.nMax;

        if (maximum < minimum || page <= 0)
        {
            return HiveScrollState.Create(
                orientation,
                minimum,
                Math.Max(minimum, minimum),
                minimum,
                page,
                smallChange: 1,
                largeChange: Math.Max(1, page),
                enabled: false);
        }

        var effectiveMaximum = Math.Max(
            minimum,
            maximum - page + 1);

        var viewportSize = page;
        var extent = Math.Max(
            viewportSize,
            effectiveMaximum - minimum + viewportSize);

        var enabled = effectiveMaximum > minimum;

        HideNativeScrollBar(control.Handle, bar);

        return HiveScrollState.Create(
            orientation,
            minimum,
            minimum + extent,
            info.nPos,
            viewportSize,
            smallChange: Math.Max(1, viewportSize / 10),
            largeChange: Math.Max(1, viewportSize),
            enabled: enabled);
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

        var info = new ScrollInfo
        {
            cbSize = Marshal.SizeOf<ScrollInfo>(),
            fMask = SiAll
        };

        if (!GetScrollInfo(control.Handle, bar, ref info))
        {
            HideNativeScrollBar(control.Handle, bar);
            return;
        }

        var page = Math.Max(0, ClampToInt(info.nPage));
        var maximumPosition = page > 0
            ? Math.Max(info.nMin, info.nMax - page + 1)
            : info.nMax;

        var target = Math.Clamp(
            value,
            info.nMin,
            maximumPosition);

        info.fMask = SiPos;
        info.nPos = target;

        SetScrollInfo(
            control.Handle,
            bar,
            ref info,
            true);

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

        HideNativeScrollBar(control.Handle, SbHorizontal);
        HideNativeScrollBar(control.Handle, SbVertical);
    }

    private static int ClampToInt(uint value) =>
        value > int.MaxValue
            ? int.MaxValue
            : (int)value;

    private static IntPtr MakeWParam(
        ushort low,
        ushort high) =>
        new((high << 16) | low);

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
