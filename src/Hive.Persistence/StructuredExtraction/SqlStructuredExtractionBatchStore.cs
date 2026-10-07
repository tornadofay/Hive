using System.Text.Json;
using Hive.Core;

namespace Hive.Persistence;

public sealed class SqlStructuredExtractionBatchStore : IStructuredExtractionBatchStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IEventPersistenceStore _eventStore;

    public SqlStructuredExtractionBatchStore(
        HiveEventPersistenceComposition persistence)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        _eventStore = persistence.EventStore;
    }

    public async Task<Result<StructuredExtractionBatch>> CreateAsync(
        StructuredExtractionBatch batch,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var accessError = ValidateAccess(
            batch.Resource,
            accessContext,
            requireOwner: true);

        if (accessError is not null)
            return Result<StructuredExtractionBatch>.Failure(accessError);

        if (batch.Resource.Version != ResourceVersion.Initial)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.batch-version-invalid",
                    "A new structured extraction batch must start at resource version 1."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var stream = new ResourceReference(
                ResourceKind.StructuredExtractionBatch,
                batch.Id.Value);

            var envelope = CreateEvent(
                "structured-extraction.batch-created",
                CorrelationId.New(),
                batch);

            var append = await _eventStore
                .AppendAsync(
                    new EventAppendRequest(
                        stream,
                        expectedVersion: null,
                        envelope,
                        CreateSnapshot(batch)),
                    cancellationToken)
                .ConfigureAwait(false);

            return append.IsSuccess
                ? Result<StructuredExtractionBatch>.Success(batch)
                : Result<StructuredExtractionBatch>.Failure(append.Error!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Serialization(
                    "hive.structured-extraction.batch-serialization-failed",
                    "The structured extraction batch could not be serialized."));
        }
        catch (Exception)
        {
            return Result<StructuredExtractionBatch>.Failure(
                new Error(
                    "hive.structured-extraction.batch-create-failed",
                    ErrorCategory.External,
                    "The structured extraction batch could not be persisted."));
        }
    }

    public async Task<Result<StructuredExtractionBatch>> GetAsync(
        StructuredExtractionBatchId batchId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (batchId == default)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.batch-identity-required",
                    "Structured extraction batch identity is required."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var snapshot = await _eventStore
                .GetSnapshotAsync(
                    new ResourceReference(
                        ResourceKind.StructuredExtractionBatch,
                        batchId.Value),
                    cancellationToken)
                .ConfigureAwait(false);

            if (snapshot.IsFailure)
                return Result<StructuredExtractionBatch>.Failure(snapshot.Error!);

            if (snapshot.Value is null)
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.NotFound(
                        "hive.structured-extraction.batch-not-found",
                        "The requested structured extraction batch does not exist."));
            }

            StructuredExtractionBatch? batch;

            try
            {
                batch = JsonSerializer.Deserialize<StructuredExtractionBatch>(
                    snapshot.Value.State,
                    JsonOptions);
            }
            catch (JsonException)
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Serialization(
                        "hive.structured-extraction.batch-state-invalid",
                        "Stored structured extraction batch state is invalid."));
            }

            if (batch is null ||
                batch.Id != batchId ||
                batch.Resource.Version != snapshot.Value.Version)
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Serialization(
                        "hive.structured-extraction.batch-state-invalid",
                        "Stored structured extraction batch state does not match its event snapshot."));
            }

            var accessError = ValidateAccess(
                batch.Resource,
                accessContext,
                requireOwner: false);

            return accessError is null
                ? Result<StructuredExtractionBatch>.Success(batch)
                : Result<StructuredExtractionBatch>.Failure(accessError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<StructuredExtractionBatch>.Failure(
                new Error(
                    "hive.structured-extraction.batch-read-failed",
                    ErrorCategory.External,
                    "The structured extraction batch could not be read."));
        }
    }

    public async Task<Result<StructuredExtractionBatch>> UpdateAsync(
        StructuredExtractionBatch batch,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var accessError = ValidateAccess(
            batch.Resource,
            accessContext,
            requireOwner: true);

        if (accessError is not null)
            return Result<StructuredExtractionBatch>.Failure(accessError);

        if (!expectedVersion.IsValid ||
            expectedVersion.Value <= 0)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.batch-version-invalid",
                    "A positive expected batch version is required."));
        }

        if (batch.Resource.Version.Value != expectedVersion.Value + 1)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.batch-version-sequence-invalid",
                    "The updated batch version must be exactly one greater than the expected version."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var stream = new ResourceReference(
                ResourceKind.StructuredExtractionBatch,
                batch.Id.Value);

            var envelope = CreateEvent(
                "structured-extraction.batch-updated",
                batch.Resource.Provenance.CorrelationId,
                batch);

            var append = await _eventStore
                .AppendAsync(
                    new EventAppendRequest(
                        stream,
                        expectedVersion,
                        envelope,
                        CreateSnapshot(batch)),
                    cancellationToken)
                .ConfigureAwait(false);

            return append.IsSuccess
                ? Result<StructuredExtractionBatch>.Success(batch)
                : Result<StructuredExtractionBatch>.Failure(append.Error!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Serialization(
                    "hive.structured-extraction.batch-serialization-failed",
                    "The structured extraction batch could not be serialized."));
        }
        catch (Exception)
        {
            return Result<StructuredExtractionBatch>.Failure(
                new Error(
                    "hive.structured-extraction.batch-update-failed",
                    ErrorCategory.External,
                    "The structured extraction batch could not be persisted."));
        }
    }

    private static EventSnapshot CreateSnapshot(
        StructuredExtractionBatch batch)
    {
        var stream = new ResourceReference(
            ResourceKind.StructuredExtractionBatch,
            batch.Id.Value);

        var state = JsonSerializer.SerializeToElement(
            batch,
            JsonOptions);

        return new EventSnapshot(
            stream,
            batch.Resource.Version,
            new EventPayloadVersion(1),
            state);
    }

    private static EventEnvelope CreateEvent(
        string eventType,
        CorrelationId correlationId,
        StructuredExtractionBatch batch)
    {
        return new EventEnvelope(
            EventId.New(),
            DateTimeOffset.UtcNow,
            new EventType(eventType),
            new EventPayloadVersion(1),
            correlationId,
            batch.Resource.Provenance.CausationId,
            JsonSerializer.SerializeToElement(
                new
                {
                    batchId = batch.Id.Value,
                    version = batch.Resource.Version.Value,
                    status = batch.Status.ToString()
                },
                JsonOptions));
    }

    private static Error? ValidateAccess(
        ResourceEnvelope<StructuredExtractionBatchId> resource,
        ResourceAccessContext accessContext,
        bool requireOwner)
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.DeploymentId is null ||
            accessContext.PrincipalId is null)
        {
            return Error.Unauthorized(
                "hive.structured-extraction.access-context-invalid",
                "Structured extraction batch access requires deployment and principal identity.");
        }

        if (requireOwner &&
            resource.Owner != accessContext.PrincipalId.Value)
        {
            return Error.Forbidden(
                "hive.structured-extraction.batch-owner-mismatch",
                "The structured extraction batch owner does not match the caller.");
        }

        return resource.Scope.Matches(accessContext)
            ? null
            : Error.Forbidden(
                "hive.structured-extraction.batch-scope-forbidden",
                "The structured extraction batch is outside the caller's authorized scope.");
    }
}
