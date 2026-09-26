using Hive.Core;

using Hive.Persistence;



namespace Hive.Management;



internal sealed class HiveWorkItemManagementService : HiveManagementServiceBase

{

    private readonly IWorkItemResourceStore _workItems



    internal HiveWorkItemManagementService(IWorkItemResourceStore workItems)

    {

        _workItems = workItems ?? throw new ArgumentNullException(nameof(workItems));

    }



    internal Task<Result<WorkItem>> CreateImageWorkItemAsync(
        WorkItemImageSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (submission is null)
            return Failure<WorkItem>(
                Error.Validation(
                    "hive.management.work-item.submission-required",
                    "An image WorkItem submission is required."));

        var contextError = ValidateAccessContext(accessContext);

        return contextError is null
            ? _workItems.CreateImageWorkItemAsync(
                submission,
                accessContext,
                cancellationToken)
            : Failure<WorkItem>(contextError);
    }


    internal Task<Result<WorkItem>> GetWorkItemAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            workItemId == default,
            accessContext,
            "work item",
            () => _workItems.GetWorkItemAsync(
                workItemId,
                accessContext,
                cancellationToken));


    internal Task<Result<IReadOnlyList<WorkItem>>> ListWorkItemsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            accessContext,
            "work item",
            () => _workItems.ListWorkItemsAsync(
                accessContext,
                includeRetired,
                cancellationToken));


    internal Task<Result<WorkItemAttachmentContent>> GetWorkItemAttachmentAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            workItemId == default,
            accessContext,
            "work item",
            () => _workItems.GetWorkItemAttachmentAsync(
                workItemId,
                accessContext,
                cancellationToken));


    internal async Task<Result<IReadOnlyList<WorkItemActivity>>> GetWorkItemActivityAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(contextError);

        if (workItemId == default)
        {
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(
                Error.Validation(
                    "hive.management.work-item.identity-required",
                    "The work item identity is required."));
        }

        var events = await _workItems.GetWorkItemActivityAsync(
            workItemId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (events.IsFailure)
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(events.Error!);

        try
        {
            var activities = new List<WorkItemActivity>(events.Value!.Count);

            foreach (var envelope in events.Value)
            {
                var version = ReadVersion(envelope);
                var status = ReadStatus(envelope);
                var reason = ReadString(envelope, "reason");

                activities.Add(
                    new WorkItemActivity(
                        envelope.EventId,
                        envelope.OccurredAtUtc,
                        version,
                        envelope.EventType.Value,
                        status,
                        ActivityMessage(envelope.EventType.Value, reason),
                        envelope.CorrelationId,
                        envelope.CausationId));
            }

            return Result<IReadOnlyList<WorkItemActivity>>.Success(activities);
        }
        catch (EventSerializationException exception)
        {
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(exception.Error);
        }
        catch (Exception)
        {
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(
                new Error(
                    "hive.management.work-item.activity-invalid",
                    ErrorCategory.Serialization,
                    "WorkItem activity could not be reconstructed."));
        }
    }


    internal Task<Result<WorkItem>> RequestWorkItemApprovalAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        TransitionWorkItem(
            workItemId,
            expectedVersion,
            accessContext,
            "work item",
            () => _workItems.RequestApprovalAsync(
                workItemId,
                expectedVersion,
                accessContext,
                cancellationToken));


    internal Task<Result<WorkItem>> ApproveWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        TransitionWorkItem(
            workItemId,
            expectedVersion,
            accessContext,
            "work item",
            () => _workItems.ApproveAsync(
                workItemId,
                expectedVersion,
                accessContext,
                cancellationToken));


    internal Task<Result<WorkItem>> RejectWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateTransitionArguments(
            workItemId,
            expectedVersion,
            accessContext,
            "work item");

        if (validation is not null)
            return Failure<WorkItem>(validation);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure<WorkItem>(
                Error.Validation(
                    "hive.management.work-item.rejection-reason-required",
                    "A rejection reason is required."));
        }

        var normalized = reason.Trim();

        if (normalized.Length > 2000)
        {
            return Failure<WorkItem>(
                Error.Validation(
                    "hive.management.work-item.rejection-reason-too-long",
                    "A WorkItem rejection reason cannot exceed 2000 characters."));
        }

        return _workItems.RejectAsync(
            workItemId,
            expectedVersion,
            accessContext,
            normalized,
            cancellationToken);
    }


    internal static Task<Result<T>> TransitionWorkItem<T>(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<T>>> operation)
    {
        var validation = ValidateTransitionArguments(
            workItemId,
            expectedVersion,
            accessContext,
            resourceName);

        return validation is null
            ? operation()
            : Failure<T>(validation);
    }


    internal static Error? ValidateTransitionArguments(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string resourceName)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return contextError;

        if (workItemId == default)
        {
            return Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                $"The {resourceName} identity is required.");
        }

        if (expectedVersion.Value <= 0)
        {
            return Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.version-invalid",
                $"A positive {resourceName} version is required.");
        }

        return null;
    }


    internal static ResourceVersion ReadVersion(EventEnvelope envelope)
    {
        if (envelope.Payload.TryGetProperty("version", out var property) &&
            property.ValueKind == System.Text.Json.JsonValueKind.Number &&
            property.TryGetInt64(out var version) &&
            version > 0)
        {
            return new ResourceVersion(version);
        }

        throw new EventSerializationException(
            Error.Validation(
                "hive.management.work-item.activity-invalid",
                "A WorkItem activity event does not contain a valid resource version."));
    }


    internal static WorkItemStatus? ReadStatus(EventEnvelope envelope)
    {
        if (!envelope.Payload.TryGetProperty("status", out var property))
            return null;

        if (property.ValueKind == System.Text.Json.JsonValueKind.String &&
            Enum.TryParse<WorkItemStatus>(
                property.GetString(),
                true,
                out var textStatus) &&
            Enum.IsDefined(textStatus))
        {
            return textStatus;
        }

        if (property.ValueKind == System.Text.Json.JsonValueKind.Number &&
            property.TryGetInt32(out var numericStatus) &&
            Enum.IsDefined((WorkItemStatus)numericStatus))
        {
            return (WorkItemStatus)numericStatus;
        }

        throw new EventSerializationException(
            Error.Validation(
                "hive.management.work-item.activity-invalid",
                "A WorkItem activity event contains an invalid status value."));
    }


    internal static string? ReadString(
        EventEnvelope envelope,
        string propertyName)
    {
        if (!envelope.Payload.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == System.Text.Json.JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == System.Text.Json.JsonValueKind.String)
            return property.GetString();

        throw new EventSerializationException(
            Error.Validation(
                "hive.management.work-item.activity-invalid",
                $"A WorkItem activity event contains an invalid {propertyName} value."));
    }


    internal static string ActivityMessage(
        string eventType,
        string? reason) =>
        eventType switch
        {
            "work-item.created" => "WorkItem created.",
            "work-item.approval-requested" => "Approval requested.",
            "work-item.approved" => "WorkItem approved.",
            "work-item.rejected" => string.IsNullOrWhiteSpace(reason)
                ? "WorkItem rejected."
                : $"WorkItem rejected: {reason}",
            _ => eventType
        };


}