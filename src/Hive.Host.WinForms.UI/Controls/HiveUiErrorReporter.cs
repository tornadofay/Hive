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
        RunObserverSafely(
            () => output?.Write("EXCEPTION", technicalDetails),
            "Output-panel diagnostic write",
            technicalDetails);

        RunObserverSafely(
            () => HiveMessageBox.Show(
                owner,
                new HiveMessageOptions(
                    title,
                    safeMessage,
                    HiveMessageType.Error,
                    MessageBoxButtons.OK,
                    technicalDetails,
                    DetailsExpanded: true),
                themeManager),
            "HiveMessageBox presentation",
            technicalDetails);
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

        RunObserverSafely(
            () => output?.Write("ERROR", message),
            "Output-panel error write",
            message);

        RunObserverSafely(
            () => HiveMessageBox.ShowError(
                owner,
                message,
                title,
                themeManager),
            "HiveMessageBox error presentation",
            message);
    }

    internal static void RunObserverSafely(
        Action observer,
        string boundary,
        string safeDiagnostic)
    {
        ArgumentNullException.ThrowIfNull(observer);

        try
        {
            observer();
        }
        catch (Exception observerException)
        {
            string failureDetails;
            try
            {
                failureDetails = HiveUiExceptionDiagnostics.Format(observerException);
            }
            catch
            {
                failureDetails =
                    observerException.GetType().FullName ??
                    observerException.GetType().Name;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"HiveUiErrorReporter: {boundary} failed.{Environment.NewLine}" +
                    $"Original safe diagnostic: {safeDiagnostic}{Environment.NewLine}" +
                    $"Reporter exception: {failureDetails}");
            }
            catch
            {
                // A broken diagnostic listener must not escape this observer boundary.
            }
        }
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
            @"(?<prefix>\b(?:password|passwd|pwd|secret|api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret)\s*[:=]\s*\{)(?<value>[^\r\n\}]*)(?<suffix>\})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:password|passwd|pwd|secret|api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret)\s*[:=]\s*)(?!(?:[""'\{]|$))(?<value>[^\r\n,;\]\}]+?)(?=$|[\r\n,;\]\}])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:double|single)[\s_-]?quote\s*=\s*(?<quote>[""']))(?<value>(?:\\[\s\S]|(?!\k<quote>)[^\\])*)(?<suffix>\k<quote>)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:authorization)\s*:\s*(?:bearer|basic)\s+)(?<value>[^\s,;]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(
            @"(?<prefix>\b(?:authorization)\s*=\s*(?:bearer|basic)\s+)(?<value>[^\s,;]+)",
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
