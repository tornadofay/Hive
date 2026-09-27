using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public static class HiveUiErrorReporter
{
    public static void Report(
        IWin32Window? owner,
        Exception exception,
        string title,
        string message,
        IHiveExampleOutput? output = null,
        IHiveThemeManager? themeManager = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var safeMessage = string.IsNullOrWhiteSpace(message)
            ? "The requested operation failed."
            : HiveUiExceptionDiagnostics.SanitizeMessage(message);

        var technicalDetails = HiveUiExceptionDiagnostics.Format(exception);
        output?.Write("EXCEPTION", technicalDetails);

        HiveMessageBox.Show(
            owner,
            new HiveMessageOptions(
                title,
                safeMessage,
                HiveMessageType.Error,
                MessageBoxButtons.OK,
                technicalDetails,
                DetailsExpanded: true),
            themeManager);
    }

    public static void Report(
        IWin32Window? owner,
        string message,
        string title,
        IHiveExampleOutput? output = null,
        IHiveThemeManager? themeManager = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "The requested operation failed.";
        else
            message = HiveUiExceptionDiagnostics.SanitizeMessage(message);

        output?.Write("ERROR", message);

        HiveMessageBox.ShowError(
            owner,
            message,
            title,
            themeManager);
    }
}


internal static class HiveUiExceptionDiagnostics
{
    private static readonly Regex[] SensitivePatterns
    =
    [
        new(
            @"(?<prefix>\b(?:password|passwd|pwd|secret|api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret)\s*[""']?\s*[:=]\s*(?<quote>[""']))(?<value>(?:\\[\s\S]|(?!\k<quote>)[^\\])*)(?<suffix>\k<quote>)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:password|passwd|pwd|secret|api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret)\s*[:=]\s*(?<quote>[""']))(?!\[REDACTED\])(?<value>(?:\\[\s\S]|(?!\k<quote>)[^\\\r\n;\]\}])*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:password|passwd|pwd|secret|api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret)\s*[:=]\s*)(?!(?:[""']|$))(?<value>[^\r\n,;\]\}]+?)(?=$|[\r\n,;\]\}])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:authorization)\s*:\s*(?:bearer|basic)\s+)(?<value>[^\s,;]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>://[^/\s:@]+:)(?<value>[^@\s/]+)(?<suffix>@)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    ];

    public static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();
        var current = exception;
        var depth = 0;

        while (current is not null)
        {
            if (depth > 0)
                builder.AppendLine();

            builder.Append("Exception ");
            builder.Append(depth + 1);
            builder.Append(": ");
            builder.Append(current.GetType().FullName ?? current.GetType().Name);

            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                builder.Append(": ");
                builder.Append(SanitizeMessage(current.Message));
            }

            current = current.InnerException;
            depth++;
        }

        return builder.ToString();
    }

    internal static string SanitizeMessage(string text)
    {
        var result = text;

        foreach (var pattern in SensitivePatterns)
        {
            result = pattern.Replace(
                result,
                match =>
                {
                    var replacement = match.Groups["prefix"].Value +
                        "[REDACTED]";

                    if (match.Groups["suffix"].Success)
                        replacement += match.Groups["suffix"].Value;

                    return replacement;
                });
        }

        return result;
    }
}
