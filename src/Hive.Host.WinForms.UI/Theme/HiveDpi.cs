using System.Drawing;
using System.Windows.Forms;

namespace Hive.Host.WinForms.UI;

internal static class HiveDpi
{
    public const int DesignDpi = 96;

    public static int Scale(Control control, int designPixels)
    {
        ArgumentNullException.ThrowIfNull(control);

        var dpi = control.DeviceDpi;
        if (dpi <= 0 || dpi == DesignDpi)
            return designPixels;

        return (int)Math.Round(
            designPixels * (dpi / (float)DesignDpi),
            MidpointRounding.AwayFromZero);
    }

    public static float Scale(Control control, float designPixels)
    {
        ArgumentNullException.ThrowIfNull(control);

        var dpi = control.DeviceDpi;
        if (dpi <= 0 || dpi == DesignDpi)
            return designPixels;

        return designPixels * (dpi / (float)DesignDpi);
    }

    public static Size Scale(Control control, Size designSize) =>
        new(
            Scale(control, designSize.Width),
            Scale(control, designSize.Height));

    public static Padding Scale(Control control, Padding designPadding) =>
        new(
            Scale(control, designPadding.Left),
            Scale(control, designPadding.Top),
            Scale(control, designPadding.Right),
            Scale(control, designPadding.Bottom));
}
