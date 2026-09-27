namespace Hive.Host.WinForms;

internal static class HiveWinFormsText
{
    public static string CleanRequired(
        string? overrideValue,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue.Trim();

        return fallback.Trim();
    }

    public static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
