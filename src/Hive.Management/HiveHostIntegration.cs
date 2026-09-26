using Hive.Core;

namespace Hive.Management;

public interface IHiveHostIntegrationService
{
    Task<Result<HiveHostContextDescriptor>> CaptureAsync(
        IHiveHostIntegrationAdapter adapter,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
        IHiveHostIntegrationAdapter adapter,
        HiveHostInteractionRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
        IHiveHostIntegrationAdapter adapter,
        HiveLookupRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<HiveBusinessOperationComposition>> PrepareBusinessOperationAsync(
        IHiveHostIntegrationAdapter adapter,
        string operationType,
        CorrelationId correlationId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}

public sealed class HiveHostIntegrationService : IHiveHostIntegrationService
{
    private readonly IHiveHostCapabilityAuthorizer _authorizer;

    public HiveHostIntegrationService(
        IHiveHostCapabilityAuthorizer authorizer)
    {
        _authorizer = authorizer
            ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public async Task<Result<HiveHostContextDescriptor>> CaptureAsync(
        IHiveHostIntegrationAdapter adapter,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        var validation = ValidateAccessContext(accessContext);
        if (validation is not null)
            return Result<HiveHostContextDescriptor>.Failure(validation);

        cancellationToken.ThrowIfCancellationRequested();

        return await adapter
            .CaptureAsync(accessContext, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
        IHiveHostIntegrationAdapter adapter,
        HiveHostInteractionRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateAccessContext(accessContext);
        if (validation is not null)
            return Result<HiveHostInteractionResult>.Failure(validation);

        cancellationToken.ThrowIfCancellationRequested();

        var capabilityKind = request.Kind switch
        {
            HiveHostInteractionKind.ReadControl => HiveHostCapabilityKind.ReadControl,
            HiveHostInteractionKind.SetControlValue => HiveHostCapabilityKind.SetControlValue,
            HiveHostInteractionKind.ReadRow => HiveHostCapabilityKind.ReadRow,
            HiveHostInteractionKind.AddRow => HiveHostCapabilityKind.AddRow,
            HiveHostInteractionKind.EditRow => HiveHostCapabilityKind.EditRow,
            HiveHostInteractionKind.DeleteRow => HiveHostCapabilityKind.DeleteRow,
            HiveHostInteractionKind.InvokeAction => HiveHostCapabilityKind.InvokeAction,
            _ => throw new InvalidOperationException("The host interaction kind is invalid.")
        };

        var authorization = _authorizer.Authorize(
            new HiveHostCapabilityRequest(
                request.CapabilityId,
                capabilityKind,
                request.CorrelationId,
                adapter.AdapterId,
                controlId: request.ControlId,
                surfaceId: request.SurfaceId,
                rowIdentity: request.RowIdentity,
                fieldName: request.FieldName,
                action: request.Action),
            accessContext);

        if (authorization.IsFailure)
            return Result<HiveHostInteractionResult>.Failure(authorization.Error!);

        cancellationToken.ThrowIfCancellationRequested();

        return await adapter
            .ExecuteInteractionAsync(request, accessContext, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
        IHiveHostIntegrationAdapter adapter,
        HiveLookupRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateAccessContext(accessContext);
        if (validation is not null)
            return Result<IReadOnlyList<HiveLookupOption>>.Failure(validation);

        cancellationToken.ThrowIfCancellationRequested();

        var authorization = _authorizer.Authorize(
            new HiveHostCapabilityRequest(
                request.CapabilityId,
                HiveHostCapabilityKind.ResolveLookup,
                CorrelationId.New(),
                adapter.AdapterId),
            accessContext);

        if (authorization.IsFailure)
            return Result<IReadOnlyList<HiveLookupOption>>.Failure(authorization.Error!);

        cancellationToken.ThrowIfCancellationRequested();

        return await adapter
            .ResolveLookupAsync(request, accessContext, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<HiveBusinessOperationComposition>> PrepareBusinessOperationAsync(
        IHiveHostIntegrationAdapter adapter,
        string operationType,
        CorrelationId correlationId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        var validation = ValidateAccessContext(accessContext);
        if (validation is not null)
            return Result<HiveBusinessOperationComposition>.Failure(validation);

        if (correlationId == default)
        {
            return Result<HiveBusinessOperationComposition>.Failure(
                Error.Validation(
                    "hive.host.business-operation.correlation-required",
                    "A business-operation correlation identity is required."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var context = await adapter
            .CaptureAsync(accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (context.IsFailure)
            return Result<HiveBusinessOperationComposition>.Failure(context.Error!);

        cancellationToken.ThrowIfCancellationRequested();

        var matchingOperations = context.Value!.BusinessOperations
            .Where(operation =>
                string.Equals(
                    operation.OperationType,
                    operationType.Trim(),
                    StringComparison.Ordinal))
            .ToArray();

        if (matchingOperations.Length == 0)
        {
            return Result<HiveBusinessOperationComposition>.Failure(
                new Error(
                    "hive.host.business-operation-not-found",
                    ErrorCategory.NotFound,
                    $"The host does not expose business operation '{operationType.Trim()}'."));
        }

        if (matchingOperations.Length > 1)
        {
            return Result<HiveBusinessOperationComposition>.Failure(
                new Error(
                    "hive.host.business-operation-ambiguous",
                    ErrorCategory.Conflict,
                    $"The host exposes multiple business operations with the same operation type '{operationType.Trim()}'."));
        }

        var descriptor = matchingOperations[0];

        var authorization = _authorizer.Authorize(
            new HiveHostCapabilityRequest(
                descriptor.CapabilityId,
                HiveHostCapabilityKind.BusinessOperation,
                correlationId,
                adapter.AdapterId),
            accessContext);

        if (authorization.IsFailure)
            return Result<HiveBusinessOperationComposition>.Failure(authorization.Error!);

        return Result<HiveBusinessOperationComposition>.Success(
            new HiveBusinessOperationComposition(
                descriptor.OperationType,
                descriptor.Implementation,
                correlationId,
                adapter.AdapterId));
    }

    private static Error? ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        if (accessContext is null)
        {
            return Error.Validation(
                "hive.host.integration.access-context-required",
                "A host integration access context is required.");
        }

        if (accessContext.DeploymentId is null ||
            accessContext.PrincipalId is null)
        {
            return Error.Validation(
                "hive.host.integration.identity-required",
                "Host integration requires deployment and principal identity.");
        }

        return null;
    }
}
