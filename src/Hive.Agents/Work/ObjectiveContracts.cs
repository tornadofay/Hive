using System.Collections.Concurrent;
using Hive.Core;

namespace Hive.Agents;

public enum ObjectiveStatus
{
    Active,
    Completed,
    Cancelled
}

public sealed record ObjectiveUpdate
{
    public ObjectiveUpdate(
        string completionCriteria,
        int priority,
        DateTimeOffset? deadlineUtc,
        IReadOnlyCollection<ObjectiveId> dependencies)
    {
        CompletionCriteria = RequireText(
            completionCriteria,
            nameof(completionCriteria),
            2000);
        Priority = priority;
        DeadlineUtc = deadlineUtc?.ToUniversalTime();

        ArgumentNullException.ThrowIfNull(dependencies);

        var copy = dependencies.ToArray();
        if (copy.Any(static id => id == default))
        {
            throw new ArgumentException(
                "Objective dependencies cannot contain empty identities.",
                nameof(dependencies));
        }

        if (copy.Distinct().Count() != copy.Length)
        {
            throw new ArgumentException(
                "Objective dependencies cannot contain duplicates.",
                nameof(dependencies));
        }

        Dependencies = Array.AsReadOnly(copy);
    }

    public string CompletionCriteria { get; }

    public int Priority { get; }

    public DateTimeOffset? DeadlineUtc { get; }

    public IReadOnlyList<ObjectiveId> Dependencies { get; }

    private static string RequireText(
        string value,
        string name,
        int maxLength)
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

public sealed record WorkItemBinding
{
    private WorkItemBinding(
        WorkItemId workItemId,
        ResourceVersion workItemVersion,
        AgentId agentId,
        RuntimeId runtimeId,
        ResourceProvenance provenance)
    {
        WorkItemId = workItemId;
        WorkItemVersion = workItemVersion;
        AgentId = agentId;
        RuntimeId = runtimeId;
        Provenance = provenance;
    }

    public WorkItemId WorkItemId { get; }

    public ResourceVersion WorkItemVersion { get; }

    public AgentId AgentId { get; }

    public RuntimeId RuntimeId { get; }

    public ResourceProvenance Provenance { get; }

    public ResourceReference WorkItemReference =>
        new(ResourceKind.WorkItem, WorkItemId.Value);

    public static Result<WorkItemBinding> Create(
        WorkItem workItem,
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        DateTimeOffset boundAtUtc,
        CorrelationId correlationId,
        CausationId? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(accessContext);

        var runtimeAccess = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (runtimeAccess.IsFailure)
            return Result<WorkItemBinding>.Failure(runtimeAccess.Error!);

        if (accessContext.PrincipalId!.Value != workItem.Resource.Owner)
        {
            return Result<WorkItemBinding>.Failure(
                new Error(
                    "hive.agent.workitem.owner-mismatch",
                    ErrorCategory.Forbidden,
                    "The WorkItem owner does not match the protocol caller."));
        }

        if (!workItem.Resource.Scope.Matches(accessContext))
        {
            return Result<WorkItemBinding>.Failure(
                new Error(
                    "hive.agent.workitem.scope-mismatch",
                    ErrorCategory.Forbidden,
                    "The protocol caller does not satisfy the WorkItem scope."));
        }

        if (workItem.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active ||
            workItem.IsTerminal)
        {
            return Result<WorkItemBinding>.Failure(
                Error.Validation(
                    "hive.agent.workitem.not-bindable",
                    "Only active non-terminal WorkItems can be bound to runtime work."));
        }

        var provenance = new ResourceProvenance(
            accessContext.PrincipalId.Value,
            boundAtUtc,
            correlationId,
            causationId,
            new ResourceReference(
                ResourceKind.WorkItem,
                workItem.Id.Value));

        return Result<WorkItemBinding>.Success(
            new WorkItemBinding(
                workItem.Id,
                workItem.Resource.Version,
                agentId,
                runtimeId,
                provenance));
    }
}

public sealed class Objective
{
    private Objective(
        ResourceEnvelope<ObjectiveId> resource,
        AgentId agentId,
        RuntimeId runtimeId,
        string title,
        string completionCriteria,
        int priority,
        DateTimeOffset? deadlineUtc,
        IReadOnlyList<ObjectiveId> dependencies,
        WorkItemBinding? workItemBinding,
        ObjectiveStatus status)
    {
        Resource = resource;
        AgentId = agentId;
        RuntimeId = runtimeId;
        Title = title;
        CompletionCriteria = completionCriteria;
        Priority = priority;
        DeadlineUtc = deadlineUtc;
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        WorkItemBinding = workItemBinding;
        Status = status;
    }

    public ResourceEnvelope<ObjectiveId> Resource { get; }

    public ObjectiveId Id => Resource.Identity;

    public AgentId AgentId { get; }

    public RuntimeId RuntimeId { get; }

    public string Title { get; }

    public string CompletionCriteria { get; }

    public int Priority { get; }

    public DateTimeOffset? DeadlineUtc { get; }

    public IReadOnlyList<ObjectiveId> Dependencies { get; }

    public WorkItemBinding? WorkItemBinding { get; }

    public ObjectiveStatus Status { get; }

    public static Result<Objective> Create(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string title,
        ObjectiveUpdate state,
        DateTimeOffset createdAtUtc,
        WorkItemBinding? workItemBinding = null,
        ObjectiveId? id = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var ownerAccess = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownerAccess.IsFailure)
            return Result<Objective>.Failure(ownerAccess.Error!);

        if (workItemBinding is not null &&
            (workItemBinding.AgentId != agentId ||
             workItemBinding.RuntimeId != runtimeId))
        {
            return Result<Objective>.Failure(
                Error.Validation(
                    "hive.agent.objective.binding-mismatch",
                    "The WorkItem binding belongs to a different Agent or RuntimeInstance."));
        }

        var objectiveId = id ?? ObjectiveId.New();
        var source = workItemBinding?.Provenance.Source;

        var provenance = new ResourceProvenance(
            accessContext.PrincipalId!.Value,
            createdAtUtc,
            CorrelationId.New(),
            null,
            source);

        var resource = new ResourceEnvelope<ObjectiveId>(
            ResourceKind.Objective,
            objectiveId,
            accessContext.PrincipalId.Value,
            ResourceScope.Runtime(runtimeId),
            ResourceVersion.Initial,
            provenance,
            ResourceLifecycle.Active(createdAtUtc));

        return Result<Objective>.Success(
            new Objective(
                resource,
                agentId,
                runtimeId,
                RequireTitle(title),
                state.CompletionCriteria,
                state.Priority,
                state.DeadlineUtc,
                state.Dependencies,
                workItemBinding,
                ObjectiveStatus.Active));
    }

    public Result<Objective> Update(
        ResourceAccessContext accessContext,
        ObjectiveUpdate update,
        DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(update);

        var ownership = RuntimeProtocolGuard.ValidateOwner(
            accessContext,
            AgentId,
            RuntimeId,
            Resource.Owner);

        if (ownership.IsFailure)
            return Result<Objective>.Failure(ownership.Error!);

        if (Status != ObjectiveStatus.Active)
        {
            return Result<Objective>.Failure(
                Error.Validation(
                    "hive.agent.objective.invalid-transition",
                    $"Cannot update an objective in status '{Status}'."));
        }

        return Result<Objective>.Success(
            WithState(
                update.CompletionCriteria,
                update.Priority,
                update.DeadlineUtc,
                update.Dependencies,
                WorkItemBinding,
                Status,
                changedAtUtc));
    }

    public Result<Objective> BindWorkItem(
        ResourceAccessContext accessContext,
        WorkItemBinding binding,
        DateTimeOffset boundAtUtc)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var ownership = RuntimeProtocolGuard.ValidateOwner(
            accessContext,
            AgentId,
            RuntimeId,
            Resource.Owner);

        if (ownership.IsFailure)
            return Result<Objective>.Failure(ownership.Error!);

        if (Status != ObjectiveStatus.Active)
        {
            return Result<Objective>.Failure(
                Error.Validation(
                    "hive.agent.objective.invalid-transition",
                    $"Cannot bind a WorkItem to an objective in status '{Status}'."));
        }

        if (binding.AgentId != AgentId ||
            binding.RuntimeId != RuntimeId)
        {
            return Result<Objective>.Failure(
                Error.Validation(
                    "hive.agent.objective.binding-mismatch",
                    "The WorkItem binding belongs to a different Agent or RuntimeInstance."));
        }

        if (WorkItemBinding is not null &&
            WorkItemBinding.WorkItemId != binding.WorkItemId)
        {
            return Result<Objective>.Failure(
                Error.Conflict(
                    "hive.agent.objective.binding-already-set",
                    "The objective is already bound to a different WorkItem."));
        }

        if (WorkItemBinding is not null)
            return Result<Objective>.Success(this);

        return Result<Objective>.Success(
            WithState(
                CompletionCriteria,
                Priority,
                DeadlineUtc,
                Dependencies,
                binding,
                Status,
                boundAtUtc));
    }

    public Result<Objective> Complete(
        ResourceAccessContext accessContext,
        DateTimeOffset completedAtUtc)
    {
        return TransitionTerminal(
            accessContext,
            ObjectiveStatus.Completed,
            completedAtUtc);
    }

    public Result<Objective> Cancel(
        ResourceAccessContext accessContext,
        DateTimeOffset cancelledAtUtc)
    {
        return TransitionTerminal(
            accessContext,
            ObjectiveStatus.Cancelled,
            cancelledAtUtc);
    }

    private Result<Objective> TransitionTerminal(
        ResourceAccessContext accessContext,
        ObjectiveStatus next,
        DateTimeOffset changedAtUtc)
    {
        var ownership = RuntimeProtocolGuard.ValidateOwner(
            accessContext,
            AgentId,
            RuntimeId,
            Resource.Owner);

        if (ownership.IsFailure)
            return Result<Objective>.Failure(ownership.Error!);

        if (Status != ObjectiveStatus.Active)
        {
            return Result<Objective>.Failure(
                Error.Validation(
                    "hive.agent.objective.invalid-transition",
                    $"Cannot transition an objective from status '{Status}'."));
        }

        return Result<Objective>.Success(
            WithState(
                CompletionCriteria,
                Priority,
                DeadlineUtc,
                Dependencies,
                WorkItemBinding,
                next,
                changedAtUtc));
    }

    private Objective WithState(
        string completionCriteria,
        int priority,
        DateTimeOffset? deadlineUtc,
        IReadOnlyList<ObjectiveId> dependencies,
        WorkItemBinding? workItemBinding,
        ObjectiveStatus status,
        DateTimeOffset changedAtUtc) =>
        new(
            Resource.TransitionLifecycle(
                ResourceLifecycleStatus.Active,
                changedAtUtc),
            AgentId,
            RuntimeId,
            Title,
            completionCriteria,
            priority,
            deadlineUtc,
            dependencies.ToArray(),
            workItemBinding,
            status);

    private static string RequireTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Objective title is required.", nameof(value));

        var normalized = value.Trim();

        return normalized.Length <= 200
            ? normalized
            : throw new ArgumentException(
                "Objective title cannot exceed 200 characters.",
                nameof(value));
    }
}

public sealed class ObjectiveStore
{
    private readonly ConcurrentDictionary<ObjectiveId, Objective> _objectives = new();

    public Result<Objective> Create(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string title,
        ObjectiveUpdate state,
        DateTimeOffset createdAtUtc,
        WorkItemBinding? workItemBinding = null)
    {
        var result = Objective.Create(
            accessContext,
            agentId,
            runtimeId,
            title,
            state,
            createdAtUtc,
            workItemBinding);

        if (result.IsFailure)
            return result;

        return _objectives.TryAdd(result.Value!.Id, result.Value)
            ? result
            : Result<Objective>.Failure(
                Error.Conflict(
                    "hive.agent.objective.identity-conflict",
                    "The objective identity is already in use."));
    }

    public Result<Objective> Get(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<Objective>.Failure(ownership.Error!);

        if (!_objectives.TryGetValue(objectiveId, out var objective))
        {
            return Result<Objective>.Failure(
                new Error(
                    "hive.agent.objective.not-found",
                    ErrorCategory.NotFound,
                    "The requested objective does not exist in this RuntimeInstance."));
        }

        if (objective.AgentId != agentId || objective.RuntimeId != runtimeId)
        {
            return Result<Objective>.Failure(
                new Error(
                    "hive.agent.objective.runtime-mismatch",
                    ErrorCategory.Forbidden,
                    "The requested objective belongs to another runtime boundary."));
        }

        return Result<Objective>.Success(objective);
    }

    public Result<Objective> Update(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        ObjectiveUpdate update,
        DateTimeOffset changedAtUtc)
    {
        var current = Get(
            accessContext,
            agentId,
            runtimeId,
            objectiveId);

        if (current.IsFailure)
            return current;

        var updated = current.Value!.Update(
            accessContext,
            update,
            changedAtUtc);

        if (updated.IsFailure)
            return updated;

        return _objectives.TryUpdate(
            objectiveId,
            updated.Value!,
            current.Value)
            ? updated
            : Result<Objective>.Failure(
                new Error(
                    "hive.agent.objective.concurrency-conflict",
                    ErrorCategory.Concurrency,
                    "The objective changed before the requested update was committed."));
    }

    public Result<Objective> BindWorkItem(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        WorkItemBinding binding,
        DateTimeOffset changedAtUtc)
    {
        var current = Get(
            accessContext,
            agentId,
            runtimeId,
            objectiveId);

        if (current.IsFailure)
            return current;

        var updated = current.Value!.BindWorkItem(
            accessContext,
            binding,
            changedAtUtc);

        if (updated.IsFailure || ReferenceEquals(updated.Value, current.Value))
            return updated;

        return _objectives.TryUpdate(
            objectiveId,
            updated.Value!,
            current.Value)
            ? updated
            : Result<Objective>.Failure(
                new Error(
                    "hive.agent.objective.concurrency-conflict",
                    ErrorCategory.Concurrency,
                    "The objective changed before the WorkItem binding was committed."));
    }

    public Result<Objective> Complete(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        DateTimeOffset completedAtUtc)
    {
        return Transition(
            accessContext,
            agentId,
            runtimeId,
            objectiveId,
            static (objective, context, timestamp) =>
                objective.Complete(context, timestamp),
            completedAtUtc);
    }

    public Result<Objective> Cancel(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        DateTimeOffset cancelledAtUtc)
    {
        return Transition(
            accessContext,
            agentId,
            runtimeId,
            objectiveId,
            static (objective, context, timestamp) =>
                objective.Cancel(context, timestamp),
            cancelledAtUtc);
    }

    private Result<Objective> Transition(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        Func<Objective, ResourceAccessContext, DateTimeOffset, Result<Objective>> transition,
        DateTimeOffset changedAtUtc)
    {
        var current = Get(
            accessContext,
            agentId,
            runtimeId,
            objectiveId);

        if (current.IsFailure)
            return current;

        var updated = transition(
            current.Value!,
            accessContext,
            changedAtUtc);

        if (updated.IsFailure)
            return updated;

        return _objectives.TryUpdate(
            objectiveId,
            updated.Value!,
            current.Value)
            ? updated
            : Result<Objective>.Failure(
                new Error(
                    "hive.agent.objective.concurrency-conflict",
                    ErrorCategory.Concurrency,
                    "The objective changed before the requested transition was committed."));
    }
}
