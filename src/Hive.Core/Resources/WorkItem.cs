namespace Hive.Core;

public enum WorkItemStatus
{
    Created,
    Queued,
    Running,
    PendingApproval,
    Completed,
    Rejected,
    Failed,
    Cancelled
}

public sealed class WorkItem
{
    private WorkItem(
        ResourceEnvelope<WorkItemId> resource,
        WorkItemStatus status)
    {
        Resource = resource;
        Status = status;
    }

    public ResourceEnvelope<WorkItemId> Resource { get; }

    public WorkItemId Id => Resource.Identity;

    public WorkItemStatus Status { get; }

    public bool IsTerminal =>
        Status is WorkItemStatus.Completed
            or WorkItemStatus.Rejected
            or WorkItemStatus.Failed
            or WorkItemStatus.Cancelled;

    public static WorkItem Create(
        WorkItemId id,
        PrincipalId owner,
        ResourceScope scope,
        ResourceProvenance provenance,
        DateTimeOffset createdAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var lifecycle = ResourceLifecycle.Active(createdAtUtc);

        var resource = new ResourceEnvelope<WorkItemId>(
            ResourceKind.WorkItem,
            id,
            owner,
            scope,
            ResourceVersion.Initial,
            provenance,
            lifecycle,
            metadata);

        return new WorkItem(resource, WorkItemStatus.Created);
    }

    public WorkItem TransitionTo(
        WorkItemStatus next,
        DateTimeOffset changedAtUtc)
    {
        if (next == Status)
            return this;

        if (Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
        {
            throw new InvalidOperationException(
                $"Retired WorkItem '{Id}' cannot change status.");
        }

        if (Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            throw new InvalidOperationException(
                $"WorkItem '{Id}' cannot change status while resource lifecycle is '{Resource.Lifecycle.Status}'.");
        }

        if (IsTerminal)
        {
            throw new InvalidOperationException(
                $"Terminal WorkItem '{Id}' cannot transition from '{Status}'.");
        }

        var resource = Resource.TransitionLifecycle(
            ResourceLifecycleStatus.Active,
            changedAtUtc);

        return new WorkItem(resource, next);
    }

    public WorkItem Suspend(DateTimeOffset changedAtUtc) =>
        Status == WorkItemStatus.Created || Status == WorkItemStatus.Queued || Status == WorkItemStatus.Running
            ? new WorkItem(
                Resource.TransitionLifecycle(ResourceLifecycleStatus.Suspended, changedAtUtc),
                Status)
            : throw new InvalidOperationException(
                $"WorkItem '{Id}' cannot be suspended while in status '{Status}'.");

    public WorkItem Resume(DateTimeOffset changedAtUtc) =>
        Resource.Lifecycle.Status == ResourceLifecycleStatus.Suspended
            ? new WorkItem(
                Resource.TransitionLifecycle(ResourceLifecycleStatus.Active, changedAtUtc),
                Status)
            : throw new InvalidOperationException(
                $"WorkItem '{Id}' is not suspended.");

    public WorkItem Retire(DateTimeOffset changedAtUtc) =>
        new WorkItem(
            Resource.TransitionLifecycle(ResourceLifecycleStatus.Retired, changedAtUtc),
            Status);
}
