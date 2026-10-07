using System.Collections.ObjectModel;

namespace Hive.Core;

public readonly record struct StructuredExtractionBatchId
{
    public StructuredExtractionBatchId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Structured extraction batch identity cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static StructuredExtractionBatchId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct StructuredCandidateId
{
    public StructuredCandidateId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Structured candidate identity cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static StructuredCandidateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SemanticFieldId
{
    public SemanticFieldId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();

        if (normalized.Length > 200)
            throw new ArgumentException(
                "Semantic field identity cannot exceed 200 characters.",
                nameof(value));

        if (normalized.Any(char.IsControl))
            throw new ArgumentException(
                "Semantic field identity cannot contain control characters.",
                nameof(value));

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum StructuredValueType
{
    String,
    Int64,
    Decimal,
    Boolean,
    DateTime,
    Guid
}

public enum StructuredValidationState
{
    Valid,
    Missing,
    InvalidType,
    InvalidFormat,
    Unmapped,
    Uncertain
}

public enum StructuredFieldPlacement
{
    Parent,
    Child
}

public sealed record StructuredTargetField
{
    public StructuredTargetField(
        SemanticFieldId id,
        string displayName,
        StructuredValueType valueType,
        bool required,
        StructuredFieldPlacement placement = StructuredFieldPlacement.Parent,
        string? childCollectionKey = null,
        string? dataSourceIdentity = null,
        string? databaseFieldReference = null,
        string? lookupReference = null)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
            throw new ArgumentException("Semantic field identity is required.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (!Enum.IsDefined(valueType))
            throw new ArgumentOutOfRangeException(nameof(valueType));

        if (!Enum.IsDefined(placement))
            throw new ArgumentOutOfRangeException(nameof(placement));

        if (placement == StructuredFieldPlacement.Child)
            ArgumentException.ThrowIfNullOrWhiteSpace(childCollectionKey);
        else if (!string.IsNullOrWhiteSpace(childCollectionKey))
            throw new ArgumentException(
                "A child collection key is valid only for child fields.",
                nameof(childCollectionKey));

        DisplayName = displayName.Trim();
        Id = id;
        ValueType = valueType;
        Required = required;
        Placement = placement;
        ChildCollectionKey = string.IsNullOrWhiteSpace(childCollectionKey)
            ? null
            : childCollectionKey.Trim();
        DataSourceIdentity = NormalizeOptional(dataSourceIdentity, nameof(dataSourceIdentity), 200);
        DatabaseFieldReference = NormalizeOptional(databaseFieldReference, nameof(databaseFieldReference), 200);
        LookupReference = NormalizeOptional(lookupReference, nameof(lookupReference), 200);
    }

    public SemanticFieldId Id { get; }

    public string DisplayName { get; }

    public StructuredValueType ValueType { get; }

    public bool Required { get; }

    public StructuredFieldPlacement Placement { get; }

    public string? ChildCollectionKey { get; }

    public string? DataSourceIdentity { get; }

    public string? DatabaseFieldReference { get; }

    public string? LookupReference { get; }

    private static string? NormalizeOptional(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
            throw new ArgumentException(
                $"{name} is invalid.",
                name);

        return normalized;
    }
}

public sealed class StructuredTargetSchema
{
    public StructuredTargetSchema(
        IReadOnlyList<StructuredTargetField> fields,
        string? schemaId = null)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count == 0)
            throw new ArgumentException(
                "At least one target semantic field is required.",
                nameof(fields));

        var copied = fields.ToArray();

        if (copied.Any(static field => field is null))
            throw new ArgumentException(
                "Target semantic fields cannot contain null values.",
                nameof(fields));

        if (copied.Select(static field => field.Id.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != copied.Length)
        {
            throw new ArgumentException(
                "Target semantic field identities must be unique.",
                nameof(fields));
        }

        var childGroups = copied
            .Where(static field => field.Placement == StructuredFieldPlacement.Child)
            .GroupBy(static field => field.ChildCollectionKey!, StringComparer.Ordinal);

        foreach (var group in childGroups)
        {
            if (group.Any(field => field.Placement != StructuredFieldPlacement.Child))
            {
                throw new ArgumentException(
                    $"Child collection '{group.Key}' contains an invalid field placement.",
                    nameof(fields));
            }
        }

        Fields = new ReadOnlyCollection<StructuredTargetField>(copied);
        SchemaId = string.IsNullOrWhiteSpace(schemaId)
            ? null
            : schemaId.Trim();
    }

    public IReadOnlyList<StructuredTargetField> Fields { get; }

    public string? SchemaId { get; }

    public IReadOnlyList<StructuredTargetField> ParentFields =>
        Fields
            .Where(static field => field.Placement == StructuredFieldPlacement.Parent)
            .ToArray();

    public IReadOnlyDictionary<string, IReadOnlyList<StructuredTargetField>> ChildFieldsByCollection =>
        Fields
            .Where(static field => field.Placement == StructuredFieldPlacement.Child)
            .GroupBy(static field => field.ChildCollectionKey!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<StructuredTargetField>)group.ToArray(),
                StringComparer.Ordinal);
}

public sealed record SpreadsheetMappingContext
{
    public SpreadsheetMappingContext(
        string identity,
        string fileName,
        string worksheetName,
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<IReadOnlyDictionary<string, string>> sampleRows,
        string sourceStructureFingerprint,
        string targetSchemaFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentNullException.ThrowIfNull(sourceColumns);
        ArgumentNullException.ThrowIfNull(sampleRows);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStructureFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSchemaFingerprint);

        if (sourceColumns.Count == 0 || sourceColumns.Count > InputPreparationLimits.MaxWorksheetColumns)
            throw new ArgumentOutOfRangeException(nameof(sourceColumns));

        if (sourceColumns.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Source columns cannot be blank.", nameof(sourceColumns));

        if (sourceColumns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sourceColumns.Count)
            throw new ArgumentException("Source columns must be unique.", nameof(sourceColumns));

        Identity = identity.Trim();
        FileName = fileName.Trim();
        WorksheetName = worksheetName.Trim();
        SourceColumns = new ReadOnlyCollection<string>(sourceColumns.Select(static value => value.Trim()).ToArray());
        SampleRows = new ReadOnlyCollection<IReadOnlyDictionary<string, string>>(
            sampleRows.Select(static row =>
                (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase)))
                .ToArray());
        SourceStructureFingerprint = sourceStructureFingerprint.Trim();
        TargetSchemaFingerprint = targetSchemaFingerprint.Trim();
    }

    public string Identity { get; }

    public string FileName { get; }

    public string WorksheetName { get; }

    public IReadOnlyList<string> SourceColumns { get; }

    public IReadOnlyList<IReadOnlyDictionary<string, string>> SampleRows { get; }

    public string SourceStructureFingerprint { get; }

    public string TargetSchemaFingerprint { get; }
}

public sealed record SpreadsheetMappingEntry
{
    public SpreadsheetMappingEntry(
        string sourceColumn,
        SemanticFieldId targetFieldId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceColumn);

        if (string.IsNullOrWhiteSpace(targetFieldId.Value))
            throw new ArgumentException("Target semantic field identity is required.", nameof(targetFieldId));

        SourceColumn = sourceColumn.Trim();
        TargetFieldId = targetFieldId;
    }

    public string SourceColumn { get; }

    public SemanticFieldId TargetFieldId { get; }
}

public enum SpreadsheetMappingReviewState
{
    Proposed,
    Edited,
    Accepted
}

public sealed record SpreadsheetMapping
{
    public SpreadsheetMapping(
        SpreadsheetMappingContext context,
        IReadOnlyList<SpreadsheetMappingEntry> entries,
        SpreadsheetMappingReviewState reviewState,
        IReadOnlyList<string>? validationMessages = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(entries);

        if (!Enum.IsDefined(reviewState))
            throw new ArgumentOutOfRangeException(nameof(reviewState));

        var copied = entries.ToArray();

        if (copied.Any(static entry => entry is null))
            throw new ArgumentException("Mapping entries cannot contain null values.", nameof(entries));

        if (copied.Select(static entry => entry.SourceColumn)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != copied.Length)
        {
            throw new ArgumentException(
                "Each source column may be mapped at most once.",
                nameof(entries));
        }

        if (copied.Select(static entry => entry.TargetFieldId.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != copied.Length)
        {
            throw new ArgumentException(
                "Each target semantic field may be mapped at most once.",
                nameof(entries));
        }

        Entries = new ReadOnlyCollection<SpreadsheetMappingEntry>(copied);
        ReviewState = reviewState;
        ValidationMessages = new ReadOnlyCollection<string>(
            (validationMessages ?? Array.Empty<string>())
                .Where(static message => !string.IsNullOrWhiteSpace(message))
                .Select(static message => message.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    public SpreadsheetMappingContext Context { get; }

    public IReadOnlyList<SpreadsheetMappingEntry> Entries { get; }

    public SpreadsheetMappingReviewState ReviewState { get; }

    public IReadOnlyList<string> ValidationMessages { get; }

    public bool IsDeterministicallyValid => ValidationMessages.Count == 0;
}

public sealed record StructuredCandidateField
{
    public StructuredCandidateField(
        SemanticFieldId fieldId,
        StructuredValueType valueType,
        string? value,
        StructuredValidationState validationState,
        string? validationMessage = null)
    {
        if (string.IsNullOrWhiteSpace(fieldId.Value))
            throw new ArgumentException("Semantic field identity is required.", nameof(fieldId));

        if (!Enum.IsDefined(valueType))
            throw new ArgumentOutOfRangeException(nameof(valueType));

        if (!Enum.IsDefined(validationState))
            throw new ArgumentOutOfRangeException(nameof(validationState));

        FieldId = fieldId;
        ValueType = valueType;
        Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        ValidationState = validationState;
        ValidationMessage = string.IsNullOrWhiteSpace(validationMessage)
            ? null
            : validationMessage.Trim();
    }

    public SemanticFieldId FieldId { get; }

    public StructuredValueType ValueType { get; }

    public string? Value { get; }

    public StructuredValidationState ValidationState { get; }

    public string? ValidationMessage { get; }
}

public sealed record StructuredChildCandidate
{
    public StructuredChildCandidate(
        string collectionKey,
        IReadOnlyList<StructuredCandidateField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionKey);
        ArgumentNullException.ThrowIfNull(fields);

        CollectionKey = collectionKey.Trim();
        Fields = new ReadOnlyCollection<StructuredCandidateField>(fields.ToArray());
    }

    public string CollectionKey { get; }

    public IReadOnlyList<StructuredCandidateField> Fields { get; }
}

public sealed record StructuredCandidateProvenance
{
    public StructuredCandidateProvenance(
        Guid submissionId,
        int itemIndex,
        string fileName,
        InputSourceKind sourceKind,
        string? worksheetName,
        int? rowNumber,
        ExecutionTargetId? executionTargetId,
        string? mappingContextIdentity)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException("Submission identity is required.", nameof(submissionId));

        if (itemIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(itemIndex));

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!Enum.IsDefined(sourceKind))
            throw new ArgumentOutOfRangeException(nameof(sourceKind));

        if (rowNumber is <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowNumber));

        if (sourceKind == InputSourceKind.Spreadsheet && string.IsNullOrWhiteSpace(worksheetName))
            throw new ArgumentException(
                "Spreadsheet provenance requires a worksheet name.",
                nameof(worksheetName));

        if (sourceKind == InputSourceKind.Image && rowNumber is not null)
            throw new ArgumentException(
                "Image provenance cannot contain a spreadsheet row.",
                nameof(rowNumber));

        SubmissionId = submissionId;
        ItemIndex = itemIndex;
        FileName = fileName.Trim();
        SourceKind = sourceKind;
        WorksheetName = string.IsNullOrWhiteSpace(worksheetName) ? null : worksheetName.Trim();
        RowNumber = rowNumber;
        ExecutionTargetId = executionTargetId;
        MappingContextIdentity = string.IsNullOrWhiteSpace(mappingContextIdentity)
            ? null
            : mappingContextIdentity.Trim();
    }

    public Guid SubmissionId { get; }

    public int ItemIndex { get; }

    public string FileName { get; }

    public InputSourceKind SourceKind { get; }

    public string? WorksheetName { get; }

    public int? RowNumber { get; }

    public ExecutionTargetId? ExecutionTargetId { get; }

    public string? MappingContextIdentity { get; }
}

public sealed record StructuredCandidate
{
    public StructuredCandidate(
        StructuredCandidateId id,
        StructuredCandidateProvenance provenance,
        IReadOnlyList<StructuredCandidateField> fields,
        IReadOnlyList<StructuredChildCandidate>? children = null,
        double? confidence = null)
    {
        if (id == default)
            throw new ArgumentException("Structured candidate identity is required.", nameof(id));

        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(fields);

        var copiedFields = fields.ToArray();

        if (copiedFields.Any(static field => field is null))
            throw new ArgumentException("Candidate fields cannot contain null values.", nameof(fields));

        if (copiedFields.Select(static field => field.FieldId.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != copiedFields.Length)
        {
            throw new ArgumentException(
                "Candidate field identities must be unique.",
                nameof(fields));
        }

        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Id = id;
        Provenance = provenance;
        Fields = new ReadOnlyCollection<StructuredCandidateField>(copiedFields);
        Children = new ReadOnlyCollection<StructuredChildCandidate>(
            (children ?? Array.Empty<StructuredChildCandidate>()).ToArray());
        Confidence = confidence;
    }

    public StructuredCandidateId Id { get; }

    public StructuredCandidateProvenance Provenance { get; }

    public IReadOnlyList<StructuredCandidateField> Fields { get; }

    public IReadOnlyList<StructuredChildCandidate> Children { get; }

    public double? Confidence { get; }

    public bool IsValid =>
        Fields.All(static field =>
            field.ValidationState == StructuredValidationState.Valid) &&
        Children.All(static child =>
            child.Fields.All(static field =>
                field.ValidationState == StructuredValidationState.Valid));
}

public enum StructuredExtractionItemStatus
{
    Pending,
    Succeeded,
    Failed,
    Uncertain,
    Excluded,
    Accepted
}

public sealed record StructuredExtractionItemResult
{
    public StructuredExtractionItemResult(
        int itemIndex,
        string fileName,
        InputSourceKind sourceKind,
        StructuredExtractionItemStatus status,
        StructuredCandidate? candidate = null,
        string? errorCode = null,
        ErrorCategory? errorCategory = null,
        string? safeErrorMessage = null,
        string? worksheetName = null,
        int? rowNumber = null,
        ExecutionTargetId? executionTargetId = null,
        string? sourceFingerprint = null,
        string? mappingContextIdentity = null)
    {
        if (itemIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(itemIndex));

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!Enum.IsDefined(sourceKind))
            throw new ArgumentOutOfRangeException(nameof(sourceKind));

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        if (rowNumber is <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowNumber));

        if (sourceKind == InputSourceKind.Spreadsheet &&
            (string.IsNullOrWhiteSpace(worksheetName) || rowNumber is null))
        {
            throw new ArgumentException(
                "Spreadsheet results require worksheet and row provenance.",
                nameof(worksheetName));
        }

        if (sourceKind == InputSourceKind.Image && rowNumber is not null)
            throw new ArgumentException(
                "Image results cannot contain spreadsheet row provenance.",
                nameof(rowNumber));

        if (status is StructuredExtractionItemStatus.Succeeded or
            StructuredExtractionItemStatus.Accepted or
            StructuredExtractionItemStatus.Uncertain)
        {
            if (candidate is null)
                throw new ArgumentException(
                    "A candidate is required for this item status.",
                    nameof(candidate));
        }

        ItemIndex = itemIndex;
        FileName = fileName.Trim();
        SourceKind = sourceKind;
        Status = status;
        Candidate = candidate;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        ErrorCategory = errorCategory;
        SafeErrorMessage = string.IsNullOrWhiteSpace(safeErrorMessage)
            ? null
            : safeErrorMessage.Trim();
        WorksheetName = string.IsNullOrWhiteSpace(worksheetName)
            ? null
            : worksheetName.Trim();
        RowNumber = rowNumber;
        ExecutionTargetId = executionTargetId;
        SourceFingerprint = string.IsNullOrWhiteSpace(sourceFingerprint)
            ? null
            : sourceFingerprint.Trim();
        MappingContextIdentity = string.IsNullOrWhiteSpace(mappingContextIdentity)
            ? null
            : mappingContextIdentity.Trim();
    }

    public int ItemIndex { get; }

    public string FileName { get; }

    public InputSourceKind SourceKind { get; }

    public StructuredExtractionItemStatus Status { get; }

    public StructuredCandidate? Candidate { get; }

    public string? ErrorCode { get; }

    public ErrorCategory? ErrorCategory { get; }

    public string? SafeErrorMessage { get; }

    public string? WorksheetName { get; }

    public int? RowNumber { get; }

    public ExecutionTargetId? ExecutionTargetId { get; }

    public string? SourceFingerprint { get; }

    public string? MappingContextIdentity { get; }
}

public enum StructuredExtractionBatchStatus
{
    Created,
    ProcessingAuthorized,
    Processing,
    ReviewRequired,
    Accepted,
    Failed,
    Cancelled
}

public sealed class StructuredExtractionBatch
{
    public StructuredExtractionBatch(
        ResourceEnvelope<StructuredExtractionBatchId> resource,
        Guid submissionId,
        StructuredTargetSchema targetSchema,
        IReadOnlyList<StructuredExtractionItemResult> items,
        IReadOnlyList<SpreadsheetMapping> mappings,
        IReadOnlyList<int> acceptedItemIndexes,
        StructuredExtractionBatchStatus status)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Kind != ResourceKind.StructuredExtractionBatch)
            throw new ArgumentException(
                "Structured extraction batch resources must use ResourceKind.StructuredExtractionBatch.",
                nameof(resource));

        if (submissionId == Guid.Empty)
            throw new ArgumentException("Submission identity is required.", nameof(submissionId));

        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(acceptedItemIndexes);

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        var itemIndexes = items.Select(static item => item.ItemIndex).ToArray();

        if (itemIndexes.Distinct().Count() != itemIndexes.Length)
            throw new ArgumentException(
                "Structured extraction item indexes must be unique.",
                nameof(items));

        if (acceptedItemIndexes.Any(index => !itemIndexes.Contains(index)))
            throw new ArgumentException(
                "Accepted item indexes must refer to known batch items.",
                nameof(acceptedItemIndexes));

        Resource = resource;
        SubmissionId = submissionId;
        TargetSchema = targetSchema;
        Items = new ReadOnlyCollection<StructuredExtractionItemResult>(items.ToArray());
        Mappings = new ReadOnlyCollection<SpreadsheetMapping>(mappings.ToArray());
        AcceptedItemIndexes = new ReadOnlyCollection<int>(
            acceptedItemIndexes.Distinct().OrderBy(static index => index).ToArray());
        Status = status;
    }

    public ResourceEnvelope<StructuredExtractionBatchId> Resource { get; }

    public StructuredExtractionBatchId Id => Resource.Identity;

    public Guid SubmissionId { get; }

    public StructuredTargetSchema TargetSchema { get; }

    public IReadOnlyList<StructuredExtractionItemResult> Items { get; }

    public IReadOnlyList<SpreadsheetMapping> Mappings { get; }

    public IReadOnlyList<int> AcceptedItemIndexes { get; }

    public StructuredExtractionBatchStatus Status { get; }

    public StructuredExtractionBatch With(
        IReadOnlyList<StructuredExtractionItemResult>? items = null,
        IReadOnlyList<SpreadsheetMapping>? mappings = null,
        IReadOnlyList<int>? acceptedItemIndexes = null,
        StructuredExtractionBatchStatus? status = null,
        DateTimeOffset? changedAtUtc = null)
    {
        var changedAt = changedAtUtc ?? DateTimeOffset.UtcNow;
        return new StructuredExtractionBatch(
            new ResourceEnvelope<StructuredExtractionBatchId>(
                ResourceKind.StructuredExtractionBatch,
                Id,
                Resource.Owner,
                Resource.Scope,
                Resource.Version.Next(),
                Resource.Provenance,
                Resource.Lifecycle.TransitionTo(
                    ResourceLifecycleStatus.Active,
                    changedAt),
                Resource.Metadata),
            SubmissionId,
            TargetSchema,
            items ?? Items,
            mappings ?? Mappings,
            acceptedItemIndexes ?? AcceptedItemIndexes,
            status ?? Status);
    }
}
