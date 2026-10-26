using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed record DirectLlmConversationOption(DirectLlmConversationSummary Conversation)
{
    public override string ToString() =>
        $"{Conversation.Title}  ·  {Conversation.Status}  ·  " +
        $"{Conversation.UpdatedAtUtc.ToLocalTime():g}";
}

internal static class DirectLlmConversationPresentation
{
    internal static string FormatDetails(DirectLlmConversationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var lastTarget = summary.LastExecutionTargetId?.ToString() ?? "not selected";
        var correlation = summary.ActiveCorrelationId?.ToString() ?? "none";
        var status = summary.LastErrorCode is { Length: > 0 } code
            ? $"{summary.Status} · {code}"
            : summary.Status.ToString();

        var lines = new List<string>
        {
            summary.Title,
            $"Status: {status} · Messages: {summary.MessageCount}",
            $"Execution target: {lastTarget} · Active request: {correlation}"
        };

        if (summary.LastErrorMessage is { Length: > 0 } message)
        {
            lines.Add($"Failure detail: {message}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
