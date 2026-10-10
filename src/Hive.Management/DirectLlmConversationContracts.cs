using Hive.Core;

namespace Hive.Management;

public enum DirectLlmConversationStatus
{
    Ready,
    Running,
    Completed,
    Failed,
    Cancelled,
    Interrupted
}

public enum DirectLlmConversationMessageRole
{
    User,
    Assistant
}

public sealed record DirectLlmConversationMessage(
    Guid MessageId,
    DirectLlmConversationMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc,
    ExecutionTargetId? ExecutionTargetId,
    string? ProviderReportedModelId);

public sealed record DirectLlmConversationSummary(
    ConversationId Id,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DirectLlmConversationStatus Status,
    int MessageCount,
    ExecutionTargetId? LastExecutionTargetId,
    CorrelationId? ActiveCorrelationId,
    string? LastErrorCode,
    string? LastErrorMessage,
    ResourceVersion Version);

public sealed record DirectLlmConversation(
    DirectLlmConversationSummary Summary,
    IReadOnlyList<DirectLlmConversationMessage> Messages);
