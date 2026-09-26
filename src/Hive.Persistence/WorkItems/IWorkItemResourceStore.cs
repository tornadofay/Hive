using Hive.Core;

namespace Hive.Persistence;

public interface IWorkItemResourceStore
{
    Task<Result<WorkItem>> CreateImageWorkItemAsync(
        WorkItemImageSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> GetWorkItemAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkItem>>> ListWorkItemsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItemAttachmentContent>> GetWorkItemAttachmentAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<EventEnvelope>>> GetWorkItemActivityAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> RequestApprovalAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> ApproveAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> RejectAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string reason,
        CancellationToken cancellationToken = default);
}
