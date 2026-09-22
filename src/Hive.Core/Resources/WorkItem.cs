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
        WorkItemStatus status,
        WorkItemAttachmentMetadata? attachment)
    {
        Resource = resource;
        Status = status;
        Attachment = attachment;
    }

    public ResourceEnvelope<WorkItemId> Resource { get; }

    public WorkItemId Id => Resource.Identity;

    public WorkItemStatus Status { get; }

    public WorkItemAttachmentMetadata? Attachment { get; }

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
        IReadOnlyDictionary<string, string>? metadata = null,
        WorkItemAttachmentMetadata? attachment = null)
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

        return new WorkItem(resource, WorkItemStatus.Created, attachment);
    }

    public static WorkItem Restore(
        ResourceEnvelope<WorkItemId> resource,
        WorkItemStatus status,
        WorkItemAttachmentMetadata? attachment = null)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Kind != ResourceKind.WorkItem)
        {
            throw new ArgumentException(
                "WorkItem resources must use ResourceKind.WorkItem.",
                nameof(resource));
        }

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status);

        return new WorkItem(resource, status, attachment);
    }

    public WorkItem TransitionTo(
        WorkItemStatus next,
        DateTimeOffset changedAtUtc)
    {
        if (next == Status)
            return this;

        if (!Enum.IsDefined(next))
            throw new ArgumentOutOfRangeException(nameof(next), next);

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

        return new WorkItem(resource, next, Attachment);
    }

    public WorkItem Suspend(DateTimeOffset changedAtUtc) =>
        Status is WorkItemStatus.Created or WorkItemStatus.Queued or WorkItemStatus.Running
            ? new WorkItem(
                Resource.TransitionLifecycle(ResourceLifecycleStatus.Suspended, changedAtUtc),
                Status,
                Attachment)
            : throw new InvalidOperationException(
                $"WorkItem '{Id}' cannot be suspended while in status '{Status}'.");

    public WorkItem Resume(DateTimeOffset changedAtUtc) =>
        Resource.Lifecycle.Status == ResourceLifecycleStatus.Suspended
            ? new WorkItem(
                Resource.TransitionLifecycle(ResourceLifecycleStatus.Active, changedAtUtc),
                Status,
                Attachment)
            : throw new InvalidOperationException(
                $"WorkItem '{Id}' is not suspended.");

    public WorkItem Retire(DateTimeOffset changedAtUtc) =>
        new WorkItem(
            Resource.TransitionLifecycle(ResourceLifecycleStatus.Retired, changedAtUtc),
            Status,
            Attachment);
}
