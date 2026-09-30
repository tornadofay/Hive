using Hive.Agents;
using Hive.Core;

namespace Hive.Coordination;

public sealed class AgentExecutionRequest
{
    public AgentExecutionRequest(
        Agent agent,
        RuntimeInstance runtime,
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        string userMessage,
        SecretMaterial? apiKey = null,
        CorrelationId? correlationId = null)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        AccessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));

        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException(
                "User message is required.",
                nameof(userMessage));

        var normalizedMessage = userMessage.Trim();

        if (normalizedMessage.Length > 64 * 1024)
        {
            throw new ArgumentException(
                "User message cannot exceed 64 KiB.",
                nameof(userMessage));
        }

        if (correlationId is { } suppliedCorrelationId &&
            suppliedCorrelationId == default)
        {
            throw new ArgumentException(
                "CorrelationId must be non-empty when supplied.",
                nameof(correlationId));
        }

        UserMessage = normalizedMessage;
        ApiKey = apiKey;
        CorrelationId = correlationId;
    }

    public Agent Agent { get; }

    public RuntimeInstance Runtime { get; }

    public ExecutionTarget Target { get; }

    public ResourceAccessContext AccessContext { get; }

    public string UserMessage { get; }

    public SecretMaterial? ApiKey { get; }

    public CorrelationId? CorrelationId { get; }
}

public sealed record AgentExecutionResult(
    Execution Execution,
    string ResponseText,
    ExecutionTargetId TargetId,
    CorrelationId CorrelationId,
    EventId StartedEventId,
    EventId TerminalEventId,
    string? ProviderResponseId);
