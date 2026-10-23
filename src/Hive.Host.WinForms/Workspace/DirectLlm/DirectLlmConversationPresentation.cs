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

        return $"{summary.Title}{Environment.NewLine}" +
               $"Status: {status} · Messages: {summary.MessageCount} · " +
               $"Last target: {lastTarget} · Active request: {correlation}";
    }
}
