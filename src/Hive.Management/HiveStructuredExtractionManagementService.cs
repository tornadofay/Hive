using Hive.Core;
using Hive.Coordination;
using Hive.Persistence;

namespace Hive.Management;

internal sealed class HiveStructuredExtractionManagementService :
    HiveManagementServiceBase
{
    internal const string MappingExecutionTargetMetadataKey =
        "hive.structured-extraction.mapping-target-id";

    private readonly HiveProviderManagementService _providers;
    private readonly HiveInputPreparationManagementService _inputPreparation;
    private readonly ISecretStore? _secrets;
    private readonly IStructuredExtractionBatchStore? _batches;
    private readonly StructuredExtractionEngine? _engine;
    private readonly IClock _clock;

    internal HiveStructuredExtractionManagementService(
        HiveProviderManagementService providers,
        HiveInputPreparationManagementService inputPreparation,
        ISecretStore? secrets,
        IStructuredExtractionBatchStore? batches,
        StructuredExtractionEngine? engine,
        IClock? clock = null)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _inputPreparation =
            inputPreparation ?? throw new ArgumentNullException(nameof(inputPreparation));
        _secrets = secrets;
        _batches = batches;
        _engine = engine;
        _clock = clock ?? SystemClock.Instance;
    }

    internal Task<Result<StructuredExtractionBatch>> GetBatchAsync(
        StructuredExtractionBatchId batchId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (_batches is null)
            return Failure<StructuredExtractionBatch>(
                Error.Unsupported(
                    "hive.structured-extraction.persistence-unavailable",
                    "Structured extraction durable state is not configured."));

        return _batches.GetAsync(
            batchId,
            accessContext,
            cancellationToken);
    }

    internal async Task<Result<StructuredExtractionBatch>> CreateBatchAsync(
        InputSubmission submission,
        StructuredTargetSchema targetSchema,
        ExecutionTargetId? mappingExecutionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var availability = ValidateDependencies(accessContext);
        if (availability is not null)
            return Result<StructuredExtractionBatch>.Failure(availability);

        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(targetSchema);

        cancellationToken.ThrowIfCancellationRequested();

        if (submission.Items.Any(
                item => item.FileName.Length > 260))
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.source-file-name-invalid",
                    "A structured extraction source file name is invalid."));
        }

        var preparation = await _inputPreparation
            .PrepareInputAsync(
                submission,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (preparation.IsFailure)
            return Result<StructuredExtractionBatch>.Failure(preparation.Error!);

        var prepared = preparation.Value!;
        var preparationFailures = prepared.Failures;

        if (prepared.PreparedInputs.Count == 0 &&
            preparationFailures.Count == 0)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.no-prepared-inputs",
                    "The selected input produced no processable items."));
        }

        var preparedResults = new List<StructuredExtractionItemResult>();
        var nextSyntheticIndex = prepared.PreparedInputs.Count;

        foreach (var input in prepared.PreparedInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (input)
            {
                case PreparedImageInput image:
                    preparedResults.Add(
                        new StructuredExtractionItemResult(
                            preparedResults.Count,
                            image.FileName,
                            InputSourceKind.Image,
                            StructuredExtractionItemStatus.Pending,
                            executionTargetId: image.ExecutionTargetId,
                            sourceFingerprint: ComputeImageFingerprint(image.Content)));
                    break;

                case PreparedSpreadsheetRowInput row:
                    var sourceColumns = row.Values.Keys
                        .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var sourceFingerprint =
                        StructuredExtractionEngine.ComputeSourceStructureFingerprint(
                            row.WorksheetName,
                            sourceColumns);

                    var targetFingerprint =
                        StructuredExtractionEngine.ComputeTargetSchemaFingerprint(
                            targetSchema);

                    var mappingContextIdentity =
                        ComputeMappingContextIdentity(
                            sourceFingerprint,
                            targetFingerprint);

                    preparedResults.Add(
                        new StructuredExtractionItemResult(
                            preparedResults.Count,
                            row.FileName,
                            InputSourceKind.Spreadsheet,
                            StructuredExtractionItemStatus.Pending,
                            worksheetName: row.WorksheetName,
                            rowNumber: row.RowNumber,
                            sourceFingerprint: sourceFingerprint,
                            mappingContextIdentity: mappingContextIdentity,
                            sourceValues: row.Values));
                    break;

                default:
                    return Result<StructuredExtractionBatch>.Failure(
                        Error.Unsupported(
                            "hive.structured-extraction.input-kind-unsupported",
                            "The prepared input kind is not supported by structured extraction."));
            }
        }

        foreach (var failure in preparationFailures)
        {
            var sourceKind = TryGetSourceKind(
                submission,
                failure.ItemIndex);

            preparedResults.Add(
                new StructuredExtractionItemResult(
                    nextSyntheticIndex++,
                    failure.FileName,
                    sourceKind,
                    StructuredExtractionItemStatus.Failed,
                    errorCode: failure.Error.Code,
                    errorCategory: failure.Error.Category,
                    safeErrorMessage: SanitizeTechnicalError(
                        failure.Error,
                        "The input could not be prepared for structured extraction.").Message));
        }

        if (preparedResults.Count > InputPreparationLimits.MaxPreparedSpreadsheetRowsPerSubmission)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.batch-item-limit",
                    "The structured extraction batch exceeds the bounded retained-item limit."));
        }

        var now = _clock.UtcNow;
        var metadata = new Dictionary<string, string>(
            StringComparer.Ordinal);

        if (mappingExecutionTargetId is { } targetId)
        {
            metadata[MappingExecutionTargetMetadataKey] =
                targetId.Value.ToString("D");
        }

        var batch = new StructuredExtractionBatch(
            new ResourceEnvelope<StructuredExtractionBatchId>(
                ResourceKind.StructuredExtractionBatch,
                StructuredExtractionBatchId.New(),
                accessContext.PrincipalId!.Value,
                CreateScope(accessContext),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    accessContext.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now),
                metadata),
            prepared.SubmissionId,
            targetSchema,
            preparedResults,
            Array.Empty<SpreadsheetMapping>(),
            Array.Empty<int>(),
            StructuredExtractionBatchStatus.Created);

        var created = await _batches!
            .CreateAsync(
                batch,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        return created;
    }

    internal async Task<Result<StructuredExtractionBatch>> AuthorizeProcessingAsync(
        StructuredExtractionBatchId batchId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var availability = ValidateDependencies(accessContext);
        if (availability is not null)
            return Result<StructuredExtractionBatch>.Failure(availability);

        var batchResult = await _batches!
            .GetAsync(batchId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (batchResult.IsFailure)
            return batchResult;

        var batch = batchResult.Value!;

        if (batch.Resource.Version != expectedVersion)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Concurrency(
                    "hive.structured-extraction.batch-version-conflict",
                    "The structured extraction batch changed before processing authorization was recorded."));

        if (batch.Status != StructuredExtractionBatchStatus.Created)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.processing-authorization-invalid",
                    "Processing authorization is valid only for a newly created batch."));

        return _batches.UpdateAsync(
            batch.With(
                status: StructuredExtractionBatchStatus.ProcessingAuthorized,
                changedAtUtc: _clock.UtcNow),
            expectedVersion,
            accessContext,
            cancellationToken);
    }

    internal async Task<Result<StructuredExtractionBatch>> ProcessBatchAsync(
        StructuredExtractionBatchId batchId,
        IReadOnlyList<PreparedInput> preparedInputs,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var availability = ValidateDependencies(accessContext);
        if (availability is not null)
            return Result<StructuredExtractionBatch>.Failure(availability);

        ArgumentNullException.ThrowIfNull(preparedInputs);

        if (preparedInputs.Count > InputPreparationLimits.MaxPreparedSpreadsheetRowsPerSubmission)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.prepared-input-limit",
                    "The supplied prepared-input set exceeds the bounded processing limit."));

        var batchResult = await _batches!
            .GetAsync(batchId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (batchResult.IsFailure)
            return batchResult;

        var batch = batchResult.Value!;

        if (batch.Status is not StructuredExtractionBatchStatus.ProcessingAuthorized and
            not StructuredExtractionBatchStatus.Processing and
            not StructuredExtractionBatchStatus.ReviewRequired)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.processing-state-invalid",
                    "The structured extraction batch is not authorized for processing or retry."));
        }

        if (batch.Status == StructuredExtractionBatchStatus.ReviewRequired &&
            !preparedInputs.Any(
                input => batch.Items.Any(
                    item => item.ItemIndex == input.ItemIndex &&
                            item.Status == StructuredExtractionItemStatus.Failed)))
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.no-retryable-items",
                    "A reviewable extraction batch has no failed items that can be retried."));
        }

        if (preparedInputs.Any(input => input.SubmissionId != batch.SubmissionId))
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.submission-mismatch",
                    "The supplied prepared inputs do not belong to the structured extraction batch."));
        }

        if (batch.Status == StructuredExtractionBatchStatus.ProcessingAuthorized)
        {
            var processing = batch.With(
                status: StructuredExtractionBatchStatus.Processing,
                changedAtUtc: _clock.UtcNow);

            var persisted = await _batches.UpdateAsync(
                processing,
                batch.Resource.Version,
                accessContext,
                cancellationToken);

            if (persisted.IsFailure)
                return persisted;

            batch = persisted.Value!;
        }

        var mappingsByIdentity = batch.Mappings
            .ToDictionary(
                static mapping => mapping.Context.Identity,
                StringComparer.Ordinal);

        var processedIndexes = new HashSet<int>(
            batch.Items
                .Where(
                    static item =>
                        item.Status is StructuredExtractionItemStatus.Succeeded
                            or StructuredExtractionItemStatus.Uncertain
                            or StructuredExtractionItemStatus.Excluded
                            or StructuredExtractionItemStatus.Accepted)
                .Select(static item => item.ItemIndex));

        foreach (var input in preparedInputs.OrderBy(static input => input.ItemIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (processedIndexes.Contains(input.ItemIndex))
                continue;

            var currentItem = batch.Items.SingleOrDefault(
                item => item.ItemIndex == input.ItemIndex);

            if (currentItem is null)
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Conflict(
                        "hive.structured-extraction.item-not-in-batch",
                        "A supplied prepared input is not part of the durable extraction batch."));
            }

            StructuredExtractionItemResult updatedItem;

            try
            {
                updatedItem = input switch
                {
                    PreparedImageInput image =>
                        await ProcessImageAsync(
                            image,
                            batch,
                            currentItem,
                            accessContext,
                            cancellationToken).ConfigureAwait(false),

                    PreparedSpreadsheetRowInput row =>
                        await ProcessSpreadsheetAsync(
                            row,
                            batch,
                            currentItem,
                            mappingsByIdentity,
                            accessContext,
                            cancellationToken).ConfigureAwait(false),

                    _ => new StructuredExtractionItemResult(
                        currentItem.ItemIndex,
                        currentItem.FileName,
                        currentItem.SourceKind,
                        StructuredExtractionItemStatus.Failed,
                        errorCode: "hive.structured-extraction.input-kind-unsupported",
                        errorCategory: ErrorCategory.Unsupported,
                        safeErrorMessage: "The prepared input kind is not supported by structured extraction.")
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                updatedItem = new StructuredExtractionItemResult(
                    currentItem.ItemIndex,
                    currentItem.FileName,
                    currentItem.SourceKind,
                    StructuredExtractionItemStatus.Failed,
                    errorCode: "hive.structured-extraction.item-failed",
                    errorCategory: ErrorCategory.External,
                    safeErrorMessage: "The extraction item could not be processed.");
            }

            var items = batch.Items
                .Select(
                    item => item.ItemIndex == updatedItem.ItemIndex
                        ? updatedItem
                        : item)
                .ToArray();

            var persistedItemBatch = batch.With(
                items: items,
                mappings: mappingsByIdentity.Values.ToArray(),
                changedAtUtc: _clock.UtcNow);

            var persistedItemResult = await _batches.UpdateAsync(
                persistedItemBatch,
                batch.Resource.Version,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (persistedItemResult.IsFailure)
                return persistedItemResult;

            batch = persistedItemResult.Value!;
            mappingsByIdentity = batch.Mappings.ToDictionary(
                static mapping => mapping.Context.Identity,
                StringComparer.Ordinal);
        }

        return await _batches
            .UpdateAsync(
                batch.With(
                    status: StructuredExtractionBatchStatus.ReviewRequired,
                    changedAtUtc: _clock.UtcNow),
                batch.Resource.Version,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<Result<StructuredExtractionBatch>> UpdateSpreadsheetMappingAsync(
        StructuredExtractionBatchId batchId,
        string mappingContextIdentity,
        IReadOnlyList<SpreadsheetMappingEntry> entries,
        SpreadsheetMappingReviewState reviewState,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var availability = ValidateDependencies(accessContext);
        if (availability is not null)
            return Result<StructuredExtractionBatch>.Failure(availability);

        ArgumentException.ThrowIfNullOrWhiteSpace(mappingContextIdentity);
        ArgumentNullException.ThrowIfNull(entries);

        var batchResult = await _batches!
            .GetAsync(batchId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (batchResult.IsFailure)
            return batchResult;

        var batch = batchResult.Value!;

        if (batch.Resource.Version != expectedVersion)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Concurrency(
                    "hive.structured-extraction.batch-version-conflict",
                    "The structured extraction batch changed before the mapping edit was recorded."));

        if (batch.Status is StructuredExtractionBatchStatus.Accepted or
            StructuredExtractionBatchStatus.Cancelled)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.mapping-edit-not-allowed",
                    "Accepted or cancelled extraction batches cannot be edited."));
        }

        var existing = batch.Mappings.SingleOrDefault(
            mapping => string.Equals(
                mapping.Context.Identity,
                mappingContextIdentity.Trim(),
                StringComparison.Ordinal));

        if (existing is null)
        {
            return Result<StructuredExtractionBatch>.Failure(
                new Error(
                    "hive.structured-extraction.mapping-context-not-found",
                    ErrorCategory.NotFound,
                    "The requested spreadsheet mapping context does not exist."));
        }

        var validated = StructuredExtractionEngine.ValidateMapping(
            existing.Context,
            batch.TargetSchema,
            entries,
            reviewState);

        var mappings = batch.Mappings
            .Select(
                mapping => mapping.Context.Identity == existing.Context.Identity
                    ? validated
                    : mapping)
            .ToArray();

        var items = batch.Items.ToArray();

        if (validated.IsDeterministicallyValid)
        {
            foreach (var item in items
                .Where(
                    item => item.SourceKind == InputSourceKind.Spreadsheet &&
                            item.MappingContextIdentity == existing.Context.Identity &&
                            item.SourceValues is not null))
            {
                var candidateResult = _engine!.ReplaceSpreadsheetCandidateValues(
                    item.Candidate
                        ?? CreatePlaceholderCandidate(
                            item,
                            batch.SubmissionId,
                            batch.TargetSchema),
                    item.SourceValues!,
                    validated,
                    batch.TargetSchema);

                if (candidateResult.IsFailure)
                    continue;

                var candidate = candidateResult.Value!;
                items[Array.IndexOf(items, item)] =
                    CreateUpdatedCandidateItem(item, candidate);
            }
        }

        var updated = batch.With(
            items: items,
            mappings: mappings,
            status: StructuredExtractionBatchStatus.ReviewRequired,
            changedAtUtc: _clock.UtcNow);

        return _batches.UpdateAsync(
            updated,
            expectedVersion,
            accessContext,
            cancellationToken);
    }

    internal async Task<Result<StructuredExtractionBatch>> UpdateCandidateFieldAsync(
        StructuredExtractionBatchId batchId,
        int itemIndex,
        SemanticFieldId fieldId,
        string? value,
        int? childIndex,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var availability = ValidateDependencies(accessContext);
        if (availability is not null)
            return Result<StructuredExtractionBatch>.Failure(availability);

        if (itemIndex < 0 || childIndex is < 0)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.candidate-index-invalid",
                    "Candidate item and child indexes must be non-negative."));

        var batchResult = await _batches!
            .GetAsync(batchId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (batchResult.IsFailure)
            return Result<StructuredExtractionBatch>.Failure(batchResult.Error!);

        var batch = batchResult.Value!;

        if (batch.Resource.Version != expectedVersion)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Concurrency(
                    "hive.structured-extraction.batch-version-conflict",
                    "The structured extraction batch changed before the candidate edit was recorded."));

        if (batch.Status is StructuredExtractionBatchStatus.Accepted or
            StructuredExtractionBatchStatus.Cancelled)
        {
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.candidate-edit-not-allowed",
                    "Accepted or cancelled extraction batches cannot be edited."));
        }

        var itemPosition = batch.Items
            .Select((item, index) => (item, index))
            .SingleOrDefault(pair => pair.item.ItemIndex == itemIndex);

        if (itemPosition.item is null)
            return Result<StructuredExtractionBatch>.Failure(
                new Error(
                    "hive.structured-extraction.item-not-found",
                    ErrorCategory.NotFound,
                    "The requested extraction item does not exist."));

        var item = itemPosition.item;

        if (item.Candidate is null)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.candidate-edit-no-candidate",
                    "The requested item does not currently have a candidate to edit."));

        var targetField = batch.TargetSchema.Fields.SingleOrDefault(
            field => field.Id == fieldId);

        if (targetField is null)
            return Result<StructuredExtractionBatch>.Failure(
                new Error(
                    "hive.structured-extraction.field-not-found",
                    ErrorCategory.NotFound,
                    "The requested semantic field does not exist in the target schema."));

        var normalizedField = CreateEditedField(targetField, value);

        var candidate = item.Candidate;

        if (targetField.Placement == StructuredFieldPlacement.Parent)
        {
            var fields = candidate.Fields
                .Select(
                    field => field.FieldId == fieldId
                        ? normalizedField
                        : field)
                .ToArray();

            candidate = new StructuredCandidate(
                candidate.Id,
                candidate.Provenance,
                fields,
                candidate.Children,
                candidate.Confidence);
        }
        else
        {
            if (childIndex is null || childIndex.Value >= candidate.Children.Count)
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Validation(
                        "hive.structured-extraction.child-index-invalid",
                        "The requested child candidate index is outside the candidate bounds."));

            var children = candidate.Children.ToArray();
            var child = children[childIndex.Value];

            if (!string.Equals(
                    child.CollectionKey,
                    targetField.ChildCollectionKey,
                    StringComparison.Ordinal))
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Conflict(
                        "hive.structured-extraction.child-collection-mismatch",
                        "The requested child semantic field does not belong to the selected child collection."));
            }

            var childFields = child.Fields
                .Select(
                    field => field.FieldId == fieldId
                        ? normalizedField
                        : field)
                .ToArray();

            children[childIndex.Value] =
                new StructuredChildCandidate(
                    child.CollectionKey,
                    childFields);

            candidate = new StructuredCandidate(
                candidate.Id,
                candidate.Provenance,
                candidate.Fields,
                children,
                candidate.Confidence);
        }

        var updatedItem = CreateUpdatedCandidateItem(
            item,
            candidate);

        var itemsUpdated = batch.Items
            .Select(
                current => current.ItemIndex == itemIndex
                    ? updatedItem
                    : current)
            .ToArray();

        return _batches.UpdateAsync(
            batch.With(
                items: itemsUpdated,
                status: StructuredExtractionBatchStatus.ReviewRequired,
                changedAtUtc: _clock.UtcNow),
            expectedVersion,
            accessContext,
            cancellationToken);
    }

    internal async Task<Result<StructuredExtractionBatch>> AuthorizeAcceptedSetAsync(
        StructuredExtractionBatchId batchId,
        IReadOnlyList<int> acceptedItemIndexes,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var availability = ValidateDependencies(accessContext);
        if (availability is not null)
            return Result<StructuredExtractionBatch>.Failure(availability);

        ArgumentNullException.ThrowIfNull(acceptedItemIndexes);

        var batchResult = await _batches!
            .GetAsync(batchId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (batchResult.IsFailure)
            return Result<StructuredExtractionBatch>.Failure(batchResult.Error!);

        var batch = batchResult.Value!;

        if (batch.Resource.Version != expectedVersion)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Concurrency(
                    "hive.structured-extraction.batch-version-conflict",
                    "The structured extraction batch changed before candidate authorization was recorded."));

        if (batch.Status != StructuredExtractionBatchStatus.ReviewRequired)
            return Result<StructuredExtractionBatch>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.candidate-authorization-invalid",
                    "The accepted candidate set can only be authorized after processing reaches review."));
        
        var accepted = acceptedItemIndexes
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();

        var known = batch.Items
            .Select(static item => item.ItemIndex)
            .ToHashSet();

        if (accepted.Any(index => !known.Contains(index)))
            return Result<StructuredExtractionBatch>.Failure(
                Error.Validation(
                    "hive.structured-extraction.accepted-item-unknown",
                    "The accepted candidate set contains an unknown extraction item."));

        foreach (var mapping in batch.Mappings)
        {
            if (mapping.ReviewState != SpreadsheetMappingReviewState.Accepted ||
                !mapping.IsDeterministicallyValid)
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Conflict(
                        "hive.structured-extraction.mapping-review-incomplete",
                        "Every spreadsheet mapping must be reviewed and accepted before candidate authorization."));
            }
        }

        foreach (var index in accepted)
        {
            var item = batch.Items.Single(item => item.ItemIndex == index);

            if (item.Status != StructuredExtractionItemStatus.Succeeded ||
                item.Candidate is null ||
                !item.Candidate.IsValid)
            {
                return Result<StructuredExtractionBatch>.Failure(
                    Error.Conflict(
                        "hive.structured-extraction.accepted-item-invalid",
                        $"Extraction item {index} is not a valid candidate and cannot be accepted."));
            }
        }

        var items = batch.Items
            .Select(
                item => accepted.Contains(item.ItemIndex)
                    ? new StructuredExtractionItemResult(
                        item.ItemIndex,
                        item.FileName,
                        item.SourceKind,
                        StructuredExtractionItemStatus.Accepted,
                        item.Candidate,
                        item.ErrorCode,
                        item.ErrorCategory,
                        item.SafeErrorMessage,
                        item.WorksheetName,
                        item.RowNumber,
                        item.ExecutionTargetId,
                        item.SourceFingerprint,
                        item.MappingContextIdentity,
                        item.SourceValues)
                    : item.Status is StructuredExtractionItemStatus.Succeeded or
                        StructuredExtractionItemStatus.Uncertain
                        ? new StructuredExtractionItemResult(
                            item.ItemIndex,
                            item.FileName,
                            item.SourceKind,
                            StructuredExtractionItemStatus.Excluded,
                            item.Candidate,
                            item.ErrorCode,
                            item.ErrorCategory,
                            item.SafeErrorMessage,
                            item.WorksheetName,
                            item.RowNumber,
                            item.ExecutionTargetId,
                            item.SourceFingerprint,
                            item.MappingContextIdentity,
                            item.SourceValues)
                        : item)
            .ToArray();

        return _batches.UpdateAsync(
            batch.With(
                items: items,
                acceptedItemIndexes: accepted,
                status: StructuredExtractionBatchStatus.Accepted,
                changedAtUtc: _clock.UtcNow),
            expectedVersion,
            accessContext,
            cancellationToken);
    }

    private async Task<StructuredExtractionItemResult> ProcessImageAsync(
        PreparedImageInput input,
        StructuredExtractionBatch batch,
        StructuredExtractionItemResult currentItem,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var targetResult = await ResolveTargetAsync(
            input.ExecutionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (targetResult.IsFailure)
            return Failed(currentItem, targetResult.Error!);

        var resolved = targetResult.Value!;

        try
        {
            var extraction = await _engine!
                .ExtractImageAsync(
                    input,
                    batch.TargetSchema,
                    resolved.Target,
                    resolved.Credential,
                    cancellationToken)
                .ConfigureAwait(false);

            if (extraction.IsFailure)
                return Failed(currentItem, extraction.Error!);

            var candidate = extraction.Value!;
            return new StructuredExtractionItemResult(
                currentItem.ItemIndex,
                currentItem.FileName,
                InputSourceKind.Image,
                candidate.IsValid
                    ? StructuredExtractionItemStatus.Succeeded
                    : StructuredExtractionItemStatus.Uncertain,
                candidate,
                worksheetName: null,
                rowNumber: null,
                executionTargetId: input.ExecutionTargetId,
                sourceFingerprint: currentItem.SourceFingerprint);
        }
        finally
        {
            resolved.Credential?.Dispose();
        }
    }

    private async Task<StructuredExtractionItemResult> ProcessSpreadsheetAsync(
        PreparedSpreadsheetRowInput input,
        StructuredExtractionBatch batch,
        StructuredExtractionItemResult currentItem,
        Dictionary<string, SpreadsheetMapping> mappingsByIdentity,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var context = BuildMappingContext(
            input,
            batch.TargetSchema,
            mappingsByIdentity.Values);

        if (!mappingsByIdentity.TryGetValue(
                context.Identity,
                out var mapping))
        {
            var mappingTargetId = ResolveMappingTargetId(batch);

            if (mappingTargetId is null)
                return Failed(
                    currentItem,
                    new Error(
                        "hive.structured-extraction.mapping-target-required",
                        ErrorCategory.Validation,
                        "A text-capable execution target is required for spreadsheet semantic mapping."));

            var targetResult = await ResolveTargetAsync(
                mappingTargetId.Value,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (targetResult.IsFailure)
                return Failed(currentItem, targetResult.Error!);

            var resolved = targetResult.Value!;

            try
            {
                var proposed = await _engine!
                    .ProposeSpreadsheetMappingAsync(
                        context,
                        batch.TargetSchema,
                        resolved.Target,
                        resolved.Credential,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (proposed.IsFailure)
                    return Failed(currentItem, proposed.Error!);

                mapping = proposed.Value!;
                mappingsByIdentity[context.Identity] = mapping;

                if (!mapping.IsDeterministicallyValid)
                {
                    return new StructuredExtractionItemResult(
                        currentItem.ItemIndex,
                        currentItem.FileName,
                        InputSourceKind.Spreadsheet,
                        StructuredExtractionItemStatus.Uncertain,
                        errorCode: "hive.structured-extraction.mapping-invalid",
                        errorCategory: ErrorCategory.Validation,
                        safeErrorMessage: "The proposed spreadsheet mapping requires human correction before the candidate can be accepted.",
                        worksheetName: input.WorksheetName,
                        rowNumber: input.RowNumber,
                        sourceFingerprint: currentItem.SourceFingerprint,
                        mappingContextIdentity: context.Identity,
                        sourceValues: input.Values);
                }
            }
            finally
            {
                resolved.Credential?.Dispose();
            }
        }

        if (!mapping.IsDeterministicallyValid)
        {
            return new StructuredExtractionItemResult(
                currentItem.ItemIndex,
                currentItem.FileName,
                InputSourceKind.Spreadsheet,
                StructuredExtractionItemStatus.Uncertain,
                errorCode: "hive.structured-extraction.mapping-invalid",
                errorCategory: ErrorCategory.Validation,
                safeErrorMessage: "The spreadsheet mapping requires human correction before the candidate can be accepted.",
                worksheetName: input.WorksheetName,
                rowNumber: input.RowNumber,
                sourceFingerprint: currentItem.SourceFingerprint,
                mappingContextIdentity: context.Identity,
                sourceValues: input.Values);
        }

        var candidateResult = _engine!.ApplySpreadsheetMapping(
            input,
            mapping,
            batch.TargetSchema);

        if (candidateResult.IsFailure)
            return Failed(currentItem, candidateResult.Error!);

        var candidate = candidateResult.Value!;

        return new StructuredExtractionItemResult(
            currentItem.ItemIndex,
            currentItem.FileName,
            InputSourceKind.Spreadsheet,
            candidate.IsValid
                ? StructuredExtractionItemStatus.Succeeded
                : StructuredExtractionItemStatus.Uncertain,
            candidate,
            worksheetName: input.WorksheetName,
            rowNumber: input.RowNumber,
            sourceFingerprint: currentItem.SourceFingerprint,
            mappingContextIdentity: context.Identity,
            sourceValues: input.Values);
    }

    private SpreadsheetMappingContext BuildMappingContext(
        PreparedSpreadsheetRowInput input,
        StructuredTargetSchema targetSchema,
        IEnumerable<SpreadsheetMapping> mappings)
    {
        var contextRows = mappings
            .Select(mapping => mapping.Context)
            .Where(
                context =>
                    string.Equals(
                        context.SourceStructureFingerprint,
                        currentSourceFingerprint(input),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        context.TargetSchemaFingerprint,
                        StructuredExtractionEngine.ComputeTargetSchemaFingerprint(targetSchema),
                        StringComparison.Ordinal))
            .SelectMany(static context => context.SampleRows)
            .Take(8)
            .ToList();

        if (contextRows.Count == 0)
            contextRows.Add(input.Values);

        var columns = input.Values.Keys
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourceFingerprint =
            StructuredExtractionEngine.ComputeSourceStructureFingerprint(
                input.WorksheetName,
                columns);

        var targetFingerprint =
            StructuredExtractionEngine.ComputeTargetSchemaFingerprint(
                targetSchema);

        return new SpreadsheetMappingContext(
            ComputeMappingContextIdentity(
                sourceFingerprint,
                targetFingerprint),
            input.FileName,
            input.WorksheetName,
            columns,
            contextRows,
            sourceFingerprint,
            targetFingerprint);

        string currentSourceFingerprint(
            PreparedSpreadsheetRowInput value) =>
            StructuredExtractionEngine.ComputeSourceStructureFingerprint(
                value.WorksheetName,
                value.Values.Keys);
    }

    private async Task<Result<(ExecutionTarget Target, SecretMaterial? Credential)>> ResolveTargetAsync(
        ExecutionTargetId targetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var targetResult = await _providers
            .GetExecutionTargetAsync(
                targetId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (targetResult.IsFailure)
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(targetResult.Error!);

        var target = targetResult.Value!;

        var providerResult = await _providers
            .GetProviderAsync(
                target.ProviderId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (providerResult.IsFailure)
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(providerResult.Error!);

        var provider = providerResult.Value!;

        if (!string.Equals(
                provider.TransportKind,
                "openai-compatible",
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(
                Error.Unsupported(
                    "hive.structured-extraction.provider-transport-unsupported",
                    "The selected provider does not use the supported OpenAI-compatible extraction transport."));
        }

        var accountResult = await _providers
            .GetProviderAccountAsync(
                target.ProviderAccountId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (accountResult.IsFailure)
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(accountResult.Error!);

        var account = accountResult.Value!;

        if (account.ProviderId != target.ProviderId)
        {
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.provider-account-mismatch",
                    "The execution target provider account does not belong to its provider."));
        }

        if (account.CredentialSecret is null)
            return Result<(ExecutionTarget, SecretMaterial?)>.Success((target, null));

        if (_secrets is null)
        {
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(
                Error.Unsupported(
                    "hive.structured-extraction.credential-store-unavailable",
                    "The selected execution target requires protected credential material, but the Hive secret store is not configured."));
        }

        var secret = await _secrets
            .GetAsync(
                account.CredentialSecret.Value.Id,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (secret.IsFailure)
            return Result<(ExecutionTarget, SecretMaterial?)>.Failure(secret.Error!);

        return Result<(ExecutionTarget, SecretMaterial?)>.Success(
            (target, secret.Value!.Material));
    }

    private StructuredCandidateField CreateEditedField(
        StructuredTargetField targetField,
        string? value)
    {
        if (value is null)
        {
            return new StructuredCandidateField(
                targetField.Id,
                targetField.ValueType,
                null,
                targetField.Required
                    ? StructuredValidationState.Missing
                    : StructuredValidationState.Valid,
                targetField.Required
                    ? "The required semantic field is empty."
                    : null);
        }

        return StructuredExtractionValueNormalizer.Normalize(
            targetField,
            value);
    }

    private static StructuredExtractionItemResult CreateUpdatedCandidateItem(
        StructuredExtractionItemResult item,
        StructuredCandidate candidate)
    {
        return new StructuredExtractionItemResult(
            item.ItemIndex,
            item.FileName,
            item.SourceKind,
            candidate.IsValid
                ? StructuredExtractionItemStatus.Succeeded
                : StructuredExtractionItemStatus.Uncertain,
            candidate,
            null,
            null,
            null,
            item.WorksheetName,
            item.RowNumber,
            item.ExecutionTargetId,
            item.SourceFingerprint,
            item.MappingContextIdentity,
            item.SourceValues);
    }

    private StructuredCandidate CreatePlaceholderCandidate(
        StructuredExtractionItemResult item,
        Guid submissionId,
        StructuredTargetSchema targetSchema)
    {
        var provenance = new StructuredCandidateProvenance(
            submissionId,
            item.ItemIndex,
            item.FileName,
            InputSourceKind.Spreadsheet,
            item.WorksheetName,
            item.RowNumber,
            null,
            item.MappingContextIdentity);

        return new StructuredCandidate(
            StructuredCandidateId.New(),
            provenance,
            targetSchema.ParentFields
                .Select(
                    field => new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        null,
                        field.Required
                            ? StructuredValidationState.Missing
                            : StructuredValidationState.Valid,
                        field.Required ? "No candidate value is available." : null))
                .ToArray(),
            targetSchema.ChildFieldsByCollection
                .Select(
                    pair => new StructuredChildCandidate(
                        pair.Key,
                        pair.Value
                            .Select(
                                field => new StructuredCandidateField(
                                    field.Id,
                                    field.ValueType,
                                    null,
                                    field.Required
                                        ? StructuredValidationState.Missing
                                        : StructuredValidationState.Valid,
                                    field.Required ? "No candidate value is available." : null))
                            .ToArray()))
                .ToArray());
    }

    private static StructuredExtractionItemResult Failed(
        StructuredExtractionItemResult item,
        Error error)
    {
        var safe = SanitizeTechnicalError(
            error,
            "The extraction item could not be processed.");

        return new StructuredExtractionItemResult(
            item.ItemIndex,
            item.FileName,
            item.SourceKind,
            StructuredExtractionItemStatus.Failed,
            errorCode: safe.Code,
            errorCategory: safe.Category,
            safeErrorMessage: safe.Message,
            worksheetName: item.WorksheetName,
            rowNumber: item.RowNumber,
            executionTargetId: item.ExecutionTargetId,
            sourceFingerprint: item.SourceFingerprint,
            mappingContextIdentity: item.MappingContextIdentity,
            sourceValues: item.SourceValues);
    }

    private static InputSourceKind? TryGetSourceKind(
        InputSubmission submission,
        int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= submission.Items.Count)
            return null;

        var mediaType = submission.Items[itemIndex].MediaType;

        if (mediaType.StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase))
            return InputSourceKind.Image;

        if (string.Equals(
                mediaType,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                StringComparison.OrdinalIgnoreCase))
            return InputSourceKind.Spreadsheet;

        return null;
    }

    private static string ComputeImageFingerprint(
        ReadOnlyMemory<byte> content) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(content.Span))
        .ToLowerInvariant();

    private static string ComputeMappingContextIdentity(
        string sourceFingerprint,
        string targetFingerprint) =>
        StructuredExtractionEngine.ComputeContextFingerprint(
            sourceFingerprint,
            targetFingerprint);

    private static string ComputeSourceStructureFingerprint(
        string worksheetName,
        IEnumerable<string> sourceColumns) =>
        StructuredExtractionEngine.ComputeSourceStructureFingerprint(
            worksheetName,
            sourceColumns.ToArray());

    private Error? ValidateDependencies(
        ResourceAccessContext accessContext)
    {
        var contextError = ValidateAccessContext(accessContext);

        if (contextError is not null)
            return contextError;

        if (_batches is null || _engine is null)
        {
            return Error.Unsupported(
                "hive.structured-extraction.persistence-unavailable",
                "Structured extraction durable state is not configured.");
        }

        return null;
    }

    private static ResourceScope CreateScope(
        ResourceAccessContext context)
    {
        if (context.WorkspaceId is not null)
            return ResourceScope.Workspace(context.WorkspaceId.Value);

        if (context.TenantId is not null)
            return ResourceScope.Tenant(context.TenantId.Value);

        return ResourceScope.Global();
    }
}
