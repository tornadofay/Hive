using Hive.Core;

namespace Hive.Agents;

public enum AgentGeneration
{
    Base,
    Cognitive
}

public sealed record AgentDefinition
{
    public AgentDefinition(
        string key,
        string displayName,
        AgentGeneration generation = AgentGeneration.Base)
    {
        Key = RequireText(key, nameof(key), 100);
        DisplayName = RequireText(displayName, nameof(displayName), 200);

        if (!Enum.IsDefined(generation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "Agent generation is invalid.");
        }

        Generation = generation;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public AgentGeneration Generation { get; }

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{name} cannot exceed {maxLength} characters.",
                name);
        }

        return normalized;
    }
}

public sealed record AgentCreationContext
{
    public AgentCreationContext(ResourceAccessContext accessContext)
    {
        AccessContext = accessContext
            ?? throw new ArgumentNullException(nameof(accessContext));
    }

    public ResourceAccessContext AccessContext { get; }
}

public interface IAgentCreationAuthorizer
{
    Result Authorize(
        AgentDefinition definition,
        AgentCreationContext context);
}

public enum RuntimeInstanceStatus
{
    Active,
    Stopped
}

public class Agent
{
    protected internal Agent(
        AgentId id,
        AgentDefinition definition,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public AgentId Id { get; }

    public AgentDefinition Definition { get; }

    public AgentGeneration Generation => Definition.Generation;

    public DateTimeOffset CreatedAtUtc { get; }

    public RuntimeInstance CreateRuntimeInstance(
        DateTimeOffset? createdAtUtc = null,
        IClock? clock = null)
    {
        return RuntimeInstance.Create(
            Id,
            Generation,
            createdAtUtc ?? DateTimeOffset.UtcNow,
            clock);
    }
}

public sealed class RuntimeInstance
{
    private RuntimeInstance(
        RuntimeId id,
        AgentId agentId,
        AgentGeneration generation,
        RuntimeInstanceStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? stoppedAtUtc,
        RuntimeWorkProtocols workProtocols)
    {
        Id = id;
        AgentId = agentId;
        Generation = generation;
        Status = status;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        StoppedAtUtc = stoppedAtUtc?.ToUniversalTime();
        Work = workProtocols ?? throw new ArgumentNullException(nameof(workProtocols));
    }

    public RuntimeId Id { get; }

    public AgentId AgentId { get; }

    public AgentGeneration Generation { get; }

    public RuntimeInstanceStatus Status { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? StoppedAtUtc { get; }

    public RuntimeWorkProtocols Work { get; }

    public Result<Execution> StartExecution(
        DateTimeOffset? startedAtUtc = null)
    {
        if (Status != RuntimeInstanceStatus.Active)
        {
            return Result<Execution>.Failure(
                Error.Validation(
                    "hive.agent.runtime.not-active",
                    "An execution cannot start on a stopped runtime instance."));
        }

        return Result<Execution>.Success(
            Execution.Create(
                Id,
                AgentId,
                Generation,
                startedAtUtc ?? DateTimeOffset.UtcNow));
    }

    public Result<RuntimeInstance> Stop(
        DateTimeOffset? stoppedAtUtc = null)
    {
        if (Status == RuntimeInstanceStatus.Stopped)
        {
            return Result<RuntimeInstance>.Failure(
                Error.Validation(
                    "hive.agent.runtime.already-stopped",
                    "The runtime instance is already stopped."));
        }

        var stopped = stoppedAtUtc ?? DateTimeOffset.UtcNow;

        return Result<RuntimeInstance>.Success(
            new RuntimeInstance(
                Id,
                AgentId,
                Generation,
                RuntimeInstanceStatus.Stopped,
                CreatedAtUtc,
                stopped,
                Work));
    }

    internal static RuntimeInstance Create(
        AgentId agentId,
        AgentGeneration generation,
        DateTimeOffset createdAtUtc,
        IClock? clock)
    {
        var runtimeId = RuntimeId.New();

        return new RuntimeInstance(
            runtimeId,
            agentId,
            generation,
            RuntimeInstanceStatus.Active,
            createdAtUtc,
            null,
            new RuntimeWorkProtocols(agentId, runtimeId, clock));
    }
}

public enum ExecutionStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed class Execution
{
    private Execution(
        ExecutionId id,
        RuntimeId runtimeId,
        AgentId agentId,
        AgentGeneration generation,
        ExecutionStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? completedAtUtc)
    {
        Id = id;
        RuntimeId = runtimeId;
        AgentId = agentId;
        Generation = generation;
        Status = status;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        CompletedAtUtc = completedAtUtc?.ToUniversalTime();
    }

    public ExecutionId Id { get; }

    public RuntimeId RuntimeId { get; }

    public AgentId AgentId { get; }

    public AgentGeneration Generation { get; }

    public ExecutionStatus Status { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    public Result<Execution> Complete(
        DateTimeOffset? completedAtUtc = null)
    {
        if (Status != ExecutionStatus.Running)
            return InvalidTransition("complete", Status);

        return Result<Execution>.Success(
            WithStatus(
                ExecutionStatus.Succeeded,
                completedAtUtc ?? DateTimeOffset.UtcNow));
    }

    public Result<Execution> Fail(
        DateTimeOffset? completedAtUtc = null)
    {
        if (Status != ExecutionStatus.Running)
            return InvalidTransition("fail", Status);

        return Result<Execution>.Success(
            WithStatus(
                ExecutionStatus.Failed,
                completedAtUtc ?? DateTimeOffset.UtcNow));
    }

    public Result<Execution> Cancel(
        DateTimeOffset? completedAtUtc = null)
    {
        if (Status != ExecutionStatus.Running)
            return InvalidTransition("cancel", Status);

        return Result<Execution>.Success(
            WithStatus(
                ExecutionStatus.Cancelled,
                completedAtUtc ?? DateTimeOffset.UtcNow));
    }

    internal static Execution Create(
        RuntimeId runtimeId,
        AgentId agentId,
        AgentGeneration generation,
        DateTimeOffset startedAtUtc) =>
        new(
            ExecutionId.New(),
            runtimeId,
            agentId,
            generation,
            ExecutionStatus.Running,
            startedAtUtc,
            null);

    private Execution WithStatus(
        ExecutionStatus status,
        DateTimeOffset completedAtUtc) =>
        new(
            Id,
            RuntimeId,
            AgentId,
            Generation,
            status,
            StartedAtUtc,
            completedAtUtc);

    private static Result<Execution> InvalidTransition(
        string operation,
        ExecutionStatus currentStatus) =>
        Result<Execution>.Failure(
            Error.Validation(
                "hive.agent.execution.invalid-transition",
                $"Cannot {operation} an execution in status '{currentStatus}'."));
}