using System.Collections.ObjectModel;

namespace Hive.Core;

public enum InputSourceKind
{
    Image,
    Spreadsheet
}

public sealed class InputItem
{
    public const int MaxContentBytes = 32 * 1024 * 1024;

    public InputItem(
        string fileName,
        string mediaType,
        ReadOnlyMemory<byte> content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(
                "File name is required.",
                nameof(fileName));

        var normalizedFileName = fileName.Trim();

        if (normalizedFileName.Length > 260 ||
            normalizedFileName.Contains('/') ||
            normalizedFileName.Contains('\\') ||
            normalizedFileName.Contains(':') ||
            normalizedFileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "File name must be a single safe leaf name no longer than 260 characters.",
                nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new ArgumentException(
                "Media type is required.",
                nameof(mediaType));

        var normalizedMediaType = mediaType.Trim();

        if (normalizedMediaType.Length > 200 ||
            normalizedMediaType.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Media type cannot exceed 200 characters or contain control characters.",
                nameof(mediaType));
        }

        if (content.Length <= 0)
            throw new ArgumentException(
                "Input content cannot be empty.",
                nameof(content));

        if (content.Length > MaxContentBytes)
        {
            throw new ArgumentException(
                $"Input content cannot exceed {MaxContentBytes} bytes.",
                nameof(content));
        }

        FileName = normalizedFileName;
        MediaType = normalizedMediaType.ToLowerInvariant();
        Content = content.ToArray();
    }

    public string FileName { get; }

    public string MediaType { get; }

    public ReadOnlyMemory<byte> Content { get; }
}

public sealed class InputSubmission
{
    public const int MaxItemCount = 32;
    public const long MaxTotalContentBytes = 32L * 1024 * 1024;

    public InputSubmission(
        IReadOnlyList<InputItem> items,
        Guid? submissionId = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException(
                "At least one input item is required.",
                nameof(items));
        }

        if (items.Count > MaxItemCount)
        {
            throw new ArgumentException(
                $"A submission cannot contain more than {MaxItemCount} input items.",
                nameof(items));
        }

        var totalBytes = 0L;
        var copied = new List<InputItem>(items.Count);

        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);

            totalBytes += item.Content.Length;

            if (totalBytes > MaxTotalContentBytes)
            {
                throw new ArgumentException(
                    $"A submission cannot exceed {MaxTotalContentBytes} total content bytes.",
                    nameof(items));
            }

            copied.Add(item);
        }

        SubmissionId = submissionId.GetValueOrDefault(Guid.NewGuid());

        if (SubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Submission identity must be non-empty.",
                nameof(submissionId));
        }

        Items = new ReadOnlyCollection<InputItem>(copied);
    }

    public Guid SubmissionId { get; }

    public IReadOnlyList<InputItem> Items { get; }
}

public abstract class PreparedInput
{
    protected PreparedInput(
        Guid submissionId,
        int itemIndex,
        string fileName,
        string mediaType,
        InputSourceKind sourceKind)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException(
                "Submission identity is required.",
                nameof(submissionId));

        if (itemIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(itemIndex));

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        if (!Enum.IsDefined(sourceKind))
            throw new ArgumentOutOfRangeException(nameof(sourceKind));

        SubmissionId = submissionId;
        ItemIndex = itemIndex;
        FileName = fileName.Trim();
        MediaType = mediaType.Trim().ToLowerInvariant();
        SourceKind = sourceKind;
    }

    public Guid SubmissionId { get; }

    public int ItemIndex { get; }

    public string FileName { get; }

    public string MediaType { get; }

    public InputSourceKind SourceKind { get; }
}

public sealed class PreparedImageInput : PreparedInput
{
    public PreparedImageInput(
        Guid submissionId,
        int itemIndex,
        string fileName,
        string mediaType,
        ReadOnlyMemory<byte> content,
        ExecutionTargetId executionTargetId,
        IReadOnlyList<ExecutionTargetSelectionDiagnostic> routingDiagnostics)
        : base(
            submissionId,
            itemIndex,
            fileName,
            mediaType,
            InputSourceKind.Image)
    {
        if (content.Length <= 0)
            throw new ArgumentException(
                "Prepared image content cannot be empty.",
                nameof(content));

        if (content.Length > WorkItemImageSubmission.MaxContentBytes)
        {
            throw new ArgumentException(
                $"Prepared image content cannot exceed {WorkItemImageSubmission.MaxContentBytes} bytes.",
                nameof(content));
        }

        if (executionTargetId == default)
            throw new ArgumentException(
                "A routed execution target is required for image input.",
                nameof(executionTargetId));

        ArgumentNullException.ThrowIfNull(routingDiagnostics);

        Content = content.ToArray();
        ExecutionTargetId = executionTargetId;
        RoutingDiagnostics = new ReadOnlyCollection<ExecutionTargetSelectionDiagnostic>(
            routingDiagnostics.ToList());
    }

    public ReadOnlyMemory<byte> Content { get; }

    public ExecutionTargetId ExecutionTargetId { get; }

    public IReadOnlyList<ExecutionTargetSelectionDiagnostic> RoutingDiagnostics { get; }
}

public sealed class PreparedSpreadsheetRowInput : PreparedInput
{
    public PreparedSpreadsheetRowInput(
        Guid submissionId,
        int itemIndex,
        string fileName,
        string mediaType,
        string worksheetName,
        int rowNumber,
        IReadOnlyDictionary<string, string> values)
        : base(
            submissionId,
            itemIndex,
            fileName,
            mediaType,
            InputSourceKind.Spreadsheet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);

        if (rowNumber <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(rowNumber),
                rowNumber,
                "Spreadsheet row number must be positive.");

        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
            throw new ArgumentException(
                "At least one mapped spreadsheet column is required.",
                nameof(values));

        var copied = new Dictionary<string, string>(
            values.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException(
                    "Spreadsheet column names cannot be empty.",
                    nameof(values));

            copied[pair.Key.Trim()] = pair.Value ?? string.Empty;
        }

        WorksheetName = worksheetName.Trim();
        RowNumber = rowNumber;
        Values = new ReadOnlyDictionary<string, string>(copied);
    }

    public string WorksheetName { get; }

    public int RowNumber { get; }

    public IReadOnlyDictionary<string, string> Values { get; }
}

public sealed record InputPreparationFailure
{
    public InputPreparationFailure(
        int itemIndex,
        string fileName,
        string? sourceLocation,
        Error error)
    {
        if (itemIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(itemIndex));

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(error);

        if (sourceLocation is not null &&
            (sourceLocation.Length > 260 || sourceLocation.Any(char.IsControl)))
        {
            throw new ArgumentException(
                "Source location is invalid.",
                nameof(sourceLocation));
        }

        ItemIndex = itemIndex;
        FileName = fileName.Trim();
        SourceLocation = string.IsNullOrWhiteSpace(sourceLocation)
            ? null
            : sourceLocation.Trim();
        Error = error;
    }

    public int ItemIndex { get; }

    public string FileName { get; }

    public string? SourceLocation { get; }

    public Error Error { get; }
}

public sealed class InputPreparationResult
{
    public InputPreparationResult(
        Guid submissionId,
        IReadOnlyList<PreparedInput> preparedInputs,
        IReadOnlyList<InputPreparationFailure> failures)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException(
                "Submission identity is required.",
                nameof(submissionId));

        ArgumentNullException.ThrowIfNull(preparedInputs);
        ArgumentNullException.ThrowIfNull(failures);

        PreparedInputs = new ReadOnlyCollection<PreparedInput>(
            preparedInputs.ToList());
        Failures = new ReadOnlyCollection<InputPreparationFailure>(
            failures.ToList());
        SubmissionId = submissionId;
    }

    public Guid SubmissionId { get; }

    public IReadOnlyList<PreparedInput> PreparedInputs { get; }

    public IReadOnlyList<InputPreparationFailure> Failures { get; }

    public bool HasFailures => Failures.Count != 0;
}

public static class InputPreparationLimits
{
    public const int MaxSpreadsheetBytes = 16 * 1024 * 1024;
    public const int MaxSpreadsheetWorksheets = 32;
    public const int MaxWorksheetRows = 5000;
    public const int MaxWorksheetColumns = 128;
    public const int MaxCellValueLength = 4096;
    public const int MaxSharedStrings = 100000;
    public const int MaxSharedStringCharacters = 16 * 1024 * 1024;
    public const int MaxZipEntries = 512;
    public const int MaxXmlEntryBytes = 16 * 1024 * 1024;
    public const long MaxXmlCharacters = 12000000;
    public const int SpreadsheetHeaderRowNumber = 1;
}