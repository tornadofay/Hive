using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal static class HiveLifecyclePresentation
{
    public static string Format(ResourceLifecycleStatus status) =>
        status switch
        {
            ResourceLifecycleStatus.Active => "● Active",
            ResourceLifecycleStatus.Suspended => "● Suspended",
            ResourceLifecycleStatus.Retired => "● Retired",
            _ => "● Unknown"
        };

    public static Color Color(
        ResourceLifecycleStatus status,
        IHiveThemeManager themeManager) =>
        status switch
        {
            ResourceLifecycleStatus.Active => themeManager.Theme.VisualStates.Success,
            ResourceLifecycleStatus.Suspended => themeManager.Theme.VisualStates.Warning,
            ResourceLifecycleStatus.Retired => themeManager.Theme.VisualStates.Error,
            _ => themeManager.Theme.Palette.Text
        };

    public static bool IsActive<TIdentity>(
        ResourceEnvelope<TIdentity> resource)
        where TIdentity : struct =>
        resource.Lifecycle.Status == ResourceLifecycleStatus.Active;

    public static bool IsRetired<TIdentity>(
        ResourceEnvelope<TIdentity> resource)
        where TIdentity : struct =>
        resource.Lifecycle.Status == ResourceLifecycleStatus.Retired;
}
