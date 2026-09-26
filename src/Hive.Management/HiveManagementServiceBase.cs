using Hive.Core;

namespace Hive.Management;

internal abstract class HiveManagementServiceBase
{
    protected static Task<Result<T>> Execute<TIdentity, T>(
        ResourceEnvelope<TIdentity>? resource,
        ResourceKind expectedKind,
        ResourceAccessContext accessContext,
        string resourceName,
        bool isCreate,
        Func<Task<Result<T>>> operation)
        where TIdentity : struct =>
        ValidateResource(
            resource,
            expectedKind,
            accessContext,
            resourceName,
            isCreate) is { } error
            ? Failure<T>(error)
            : operation();


    protected static Task<Result<T>> Get<T>(
        bool invalidIdentity,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<T>>> operation) =>
        invalidIdentity
            ? Failure<T>(
                Error.Validation(
                    $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                    $"The {resourceName} identity is required."))
            : ValidateContextAndRun(accessContext, operation);


    protected static Task<Result<IReadOnlyList<T>>> List<T>(
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<IReadOnlyList<T>>>> operation) =>
        ValidateContextAndRun(accessContext, operation);


    protected static Task<Result<IReadOnlyList<T>>> List<T>(
        bool invalidIdentity,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<IReadOnlyList<T>>>> operation) =>
        invalidIdentity
            ? Task.FromResult(
                Result<IReadOnlyList<T>>.Failure(
                    Error.Validation(
                        $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                        $"The {resourceName} identity is required.")))
            : ValidateContextAndRun(accessContext, operation);


    protected static Task<Result<T>> Delete<T>(
        bool invalidIdentity,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<T>>> operation) =>
        invalidIdentity
            ? Failure<T>(
                Error.Validation(
                    $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                    $"The {resourceName} identity is required."))
            : ValidateContextAndRun(accessContext, operation);


    protected static Task<Result<T>> ValidateContextAndRun<T>(
        ResourceAccessContext accessContext,
        Func<Task<Result<T>>> operation)
    {
        var error = ValidateAccessContext(accessContext);

        return error is null
            ? operation()
            : Failure<T>(error);
    }


    protected static Error? ValidateResource<TIdentity>(
        ResourceEnvelope<TIdentity>? resource,
        ResourceKind expectedKind,
        ResourceAccessContext accessContext,
        string resourceName,
        bool isCreate)
        where TIdentity : struct
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return contextError;

        if (resource is null)
        {
            return Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.resource-required",
                $"A {resourceName} resource is required.");
        }

        if (resource.Kind != expectedKind)
        {
            return Error.Validation(
                "hive.resource.kind-invalid",
                $"The {resourceName} resource kind is invalid.");
        }

        if (isCreate && resource.Version != ResourceVersion.Initial)
        {
            return Error.Validation(
                "hive.resource.version-invalid",
                $"A new {resourceName} must start at resource version 1.");
        }

        if (!isCreate && resource.Version.Value <= 0)
        {
            return Error.Validation(
                "hive.resource.version-invalid",
                $"The {resourceName} resource version is invalid.");
        }

        if (isCreate &&
            resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Error.Validation(
                "hive.resource.lifecycle-invalid",
                $"A new {resourceName} must start in Active lifecycle state.");
        }

        if (resource.Owner != accessContext.PrincipalId)
        {
            return new Error(
                "hive.resource.owner-forbidden",
                ErrorCategory.Forbidden,
                $"The current principal does not own the {resourceName}.");
        }

        if (!resource.Scope.Matches(accessContext))
        {
            return new Error(
                "hive.resource.scope-forbidden",
                ErrorCategory.Forbidden,
                $"The current access context is outside the {resourceName} scope.");
        }

        return null;
    }


    protected static Error? ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        if (accessContext is null)
        {
            return Error.Validation(
                "hive.management.identity-required",
                "A resource access context is required.");
        }

        if (accessContext.DeploymentId is null ||
            accessContext.PrincipalId is null)
        {
            return Error.Validation(
                "hive.management.identity-required",
                "Management access requires a deployment and principal identity.");
        }

        return null;
    }

    private static Task<Result<T>> TransitionWorkItem<T>(
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

    private static Error? ValidateTransitionArguments(
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

    private static ResourceVersion ReadVersion(EventEnvelope envelope)
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

    private static WorkItemStatus? ReadStatus(EventEnvelope envelope)
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

    private static string? ReadString(
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

    private static string ActivityMessage(
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


    protected static Error SanitizeTechnicalError(
        Error error,
        string safeMessage)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        return error.Category is ErrorCategory.External or ErrorCategory.Internal
            ? new Error(error.Code, error.Category, safeMessage)
            : error;
    }


    protected static Task<Result<T>> Failure<T>(
        string resourceName,
        string codeSuffix) =>
        Failure<T>(
            Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.{codeSuffix}",
                $"A {resourceName} is required."));


    protected static Task<Result<T>> Failure<T>(Error error) =>
        Task.FromResult(Result<T>.Failure(error));
}

}