using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Hive.Core;

public static class InputPreparationEngine
{
    private const string SpreadsheetMediaType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static bool RequiresVisionTargetRouting(
        InputSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        return submission.Items.Any(IsImage);
    }

    public static Result<InputPreparationResult> Prepare(
        InputSubmission submission,
        IReadOnlyList<ExecutionTarget> executionTargets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(executionTargets);

        cancellationToken.ThrowIfCancellationRequested();

        if (executionTargets.Any(static target => target is null))
        {
            return Result<InputPreparationResult>.Failure(
                Error.Validation(
                    "hive.input.execution-targets-invalid",
                    "Execution target routing input cannot contain null targets."));
        }

        var prepared = new List<PreparedInput>();
        var failures = new List<InputPreparationFailure>();

        for (var index = 0; index < submission.Items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = submission.Items[index];

            try
            {
                if (IsImage(item))
                {
                    PrepareImage(
                        submission,
                        index,
                        item,
                        executionTargets,
                        prepared,
                        failures);
                    continue;
                }

                if (IsSpreadsheet(item))
                {
                    PrepareSpreadsheet(
                        submission,
                        index,
                        item,
                        prepared,
                        failures,
                        cancellationToken);
                    continue;
                }

                AddFailure(
                    failures,
                    index,
                    item,
                    null,
                    Error.Unsupported(
                        "hive.input.unsupported",
                        $"Input type '{item.MediaType}' is not supported by Phase 1.15."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                AddFailure(
                    failures,
                    index,
                    item,
                    null,
                    new Error(
                        "hive.input.item-failed",
                        ErrorCategory.Internal,
                        "Input preparation failed at the item boundary."));
            }
        }

        return Result<InputPreparationResult>.Success(
            new InputPreparationResult(
                submission.SubmissionId,
                prepared,
                failures));
    }

    private static void PrepareImage(
        InputSubmission submission,
        int itemIndex,
        InputItem item,
        IReadOnlyList<ExecutionTarget> executionTargets,
        List<PreparedInput> prepared,
        List<InputPreparationFailure> failures)
    {
        if (item.Content.Length > WorkItemImageSubmission.MaxContentBytes)
        {
            AddFailure(
                failures,
                itemIndex,
                item,
                null,
                Error.Validation(
                    "hive.input.image-too-large",
                    $"Image input exceeds the {WorkItemImageSubmission.MaxContentBytes}-byte image limit."));
            return;
        }

        if (executionTargets.Count == 0)
        {
            AddFailure(
                failures,
                itemIndex,
                item,
                null,
                Error.Unsupported(
                    "hive.input.vision-target-unavailable",
                    "No execution target with explicitly supported vision capability is available."));
            return;
        }

        Result<ExecutionTargetSelectionResult> selection;

        try
        {
            selection = ExecutionTargetSelector.Select(
                new ExecutionTargetSelectionRequest(
                    executionTargets,
                    [
                        new CapabilityRequirement(
                            new CapabilityKey("vision"),
                            CapabilityRequirementKind.Required)
                    ],
                    ExecutionTargetSelectionMode.Auto));
        }
        catch (ArgumentException)
        {
            AddFailure(
                failures,
                itemIndex,
                item,
                null,
                Error.Validation(
                    "hive.input.execution-targets-invalid",
                    "Execution target routing input is invalid."));
            return;
        }

        if (selection.IsFailure)
        {
            AddFailure(
                failures,
                itemIndex,
                item,
                null,
                selection.Error!);
            return;
        }

        prepared.Add(
            new PreparedImageInput(
                submission.SubmissionId,
                itemIndex,
                item.FileName,
                item.MediaType,
                item.Content,
                selection.Value!.SelectedTarget.Id,
                selection.Value.Diagnostics));
    }

    private static void PrepareSpreadsheet(
        InputSubmission submission,
        int itemIndex,
        InputItem item,
        List<PreparedInput> prepared,
        List<InputPreparationFailure> failures,
        CancellationToken cancellationToken)
    {
        if (item.Content.Length > InputPreparationLimits.MaxSpreadsheetBytes)
        {
            AddFailure(
                failures,
                itemIndex,
                item,
                null,
                Error.Validation(
                    "hive.input.spreadsheet-too-large",
                    $"Spreadsheet input exceeds the {InputPreparationLimits.MaxSpreadsheetBytes}-byte file limit."));
            return;
        }

        var localPrepared = new List<PreparedInput>();

        try
        {
            using var input = new MemoryStream(
                item.Content.ToArray(),
                writable: false);
            using var archive = new ZipArchive(
                input,
                ZipArchiveMode.Read,
                leaveOpen: false);

            if (archive.Entries.Count > InputPreparationLimits.MaxZipEntries)
            {
                throw new SpreadsheetPackageLimitException(
                    "hive.input.spreadsheet.archive-too-many-entries",
                    "Spreadsheet package exceeds the maximum number of archive entries.");
            }

            var workbookEntry = archive.GetEntry("xl/workbook.xml");

            if (workbookEntry is null)
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.workbook-missing",
                    "Spreadsheet workbook metadata is missing.");

            var workbookRelationshipsEntry =
                archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookRelationshipsEntry is null)
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.relationships-missing",
                    "Spreadsheet workbook relationships are missing.");

            var workbook = LoadXml(
                workbookEntry,
                cancellationToken);

            var relationships = LoadXml(
                workbookRelationshipsEntry,
                cancellationToken);

            var worksheetTargets = ReadWorksheetTargets(
                workbook,
                relationships);

            if (worksheetTargets.Count == 0)
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.no-worksheets",
                    "The spreadsheet contains no worksheets.");
            }

            if (worksheetTargets.Count > InputPreparationLimits.MaxSpreadsheetWorksheets)
            {
                throw new SpreadsheetPackageLimitException(
                    "hive.input.spreadsheet.too-many-worksheets",
                    $"Spreadsheet exceeds the {InputPreparationLimits.MaxSpreadsheetWorksheets}-worksheet limit.");
            }

            var sharedStrings = ReadSharedStrings(
                archive,
                cancellationToken);

            for (var worksheetIndex = 0;
                 worksheetIndex < worksheetTargets.Count;
                 worksheetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var worksheet = worksheetTargets[worksheetIndex];

                try
                {
                    var entryPath = ResolvePackagePath(
                        "xl/workbook.xml",
                        worksheet.Target);

                    var entry = archive.GetEntry(entryPath);

                    if (entry is null)
                    {
                        failures.Add(
                            new InputPreparationFailure(
                                itemIndex,
                                item.FileName,
                                worksheet.Name,
                                SerializationError(
                                    "hive.input.spreadsheet.worksheet-missing",
                                    "The worksheet package entry could not be found.")));
                        continue;
                    }

                    var worksheetDocument = LoadXml(
                        entry,
                        cancellationToken);

                    var sheetRows = ParseWorksheet(
                        submission,
                        itemIndex,
                        item,
                        worksheet.Name,
                        worksheetDocument,
                        sharedStrings,
                        failures,
                        cancellationToken);

                    localPrepared.AddRange(sheetRows);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (SpreadsheetPackageLimitException exception)
                {
                    failures.Add(
                        new InputPreparationFailure(
                            itemIndex,
                            item.FileName,
                            worksheet.Name,
                            new Error(
                                exception.Code,
                                ErrorCategory.Validation,
                                exception.Message)));
                }
                catch (SpreadsheetPackageException exception)
                {
                    failures.Add(
                        new InputPreparationFailure(
                            itemIndex,
                            item.FileName,
                            worksheet.Name,
                            new Error(
                                exception.Code,
                                ErrorCategory.Serialization,
                                exception.Message)));
                }
                catch (XmlException)
                {
                    failures.Add(
                        new InputPreparationFailure(
                            itemIndex,
                            item.FileName,
                            worksheet.Name,
                            new Error(
                                "hive.input.spreadsheet.worksheet-invalid",
                                ErrorCategory.Serialization,
                                "The worksheet XML is malformed.")));
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SpreadsheetPackageLimitException exception)
        {
            failures.Add(
                new InputPreparationFailure(
                    itemIndex,
                    item.FileName,
                    null,
                    new Error(
                        exception.Code,
                        ErrorCategory.Validation,
                        exception.Message)));
            return;
        }
        catch (SpreadsheetPackageException exception)
        {
            failures.Add(
                new InputPreparationFailure(
                    itemIndex,
                    item.FileName,
                    null,
                    new Error(
                        exception.Code,
                        ErrorCategory.Serialization,
                        exception.Message)));
            return;
        }
        catch (InvalidDataException)
        {
            failures.Add(
                new InputPreparationFailure(
                    itemIndex,
                    item.FileName,
                    null,
                    new Error(
                        "hive.input.spreadsheet.package-invalid",
                        ErrorCategory.Serialization,
                        "The spreadsheet package is invalid or cannot be read.")));
            return;
        }
        catch (XmlException)
        {
            failures.Add(
                new InputPreparationFailure(
                    itemIndex,
                    item.FileName,
                    null,
                    new Error(
                        "hive.input.spreadsheet.xml-invalid",
                        ErrorCategory.Serialization,
                        "The spreadsheet XML is malformed.")));
            return;
        }
        catch (IOException)
        {
            failures.Add(
                new InputPreparationFailure(
                    itemIndex,
                    item.FileName,
                    null,
                    new Error(
                        "hive.input.spreadsheet.read-failed",
                        ErrorCategory.Serialization,
                        "The spreadsheet could not be read safely.")));
            return;
        }

        prepared.AddRange(localPrepared);
    }

    private static IReadOnlyList<PreparedInput> ParseWorksheet(
        InputSubmission submission,
        int itemIndex,
        InputItem item,
        string worksheetName,
        XDocument document,
        IReadOnlyList<string>? sharedStrings,
        List<InputPreparationFailure> failures,
        CancellationToken cancellationToken)
    {
        if (worksheetName.Length > 200)
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.worksheet-name-invalid",
                "Worksheet name exceeds the 200-character limit.");
        }

        var rows = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "row")
            .ToArray();

        if (rows.Length == 0)
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.worksheet-empty",
                "Worksheet contains no rows.");
        }

        if (rows.Length > InputPreparationLimits.MaxWorksheetRows)
        {
            throw new SpreadsheetPackageLimitException(
                "hive.input.spreadsheet.too-many-rows",
                $"Worksheet exceeds the {InputPreparationLimits.MaxWorksheetRows}-row limit.");
        }

        var normalizedRows = new List<(int Number, Dictionary<int, string> Cells)>(
            rows.Length);
        var lastRowNumber = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = ReadRowNumber(
                row,
                lastRowNumber == 0 ? 1 : lastRowNumber + 1);

            if (rowNumber <= lastRowNumber)
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.row-order-invalid",
                    "Worksheet row numbers must increase in document order.");
            }

            lastRowNumber = rowNumber;

            try
            {
                var cells = ReadCells(
                    row,
                    sharedStrings);

                normalizedRows.Add((rowNumber, cells));
            }
            catch (SpreadsheetRowException exception)
            {
                failures.Add(
                    new InputPreparationFailure(
                        itemIndex,
                        item.FileName,
                        worksheetName + "!" + rowNumber,
                        new Error(
                            exception.Code,
                            ErrorCategory.Validation,
                            exception.Message)));
            }
        }

        var header = normalizedRows.FirstOrDefault(
            row => row.Number == InputPreparationLimits.SpreadsheetHeaderRowNumber);

        if (header.Number == 0)
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.header-missing",
                "Worksheet does not contain the required header row.");
        }

        var headers = BuildHeaderMap(header.Cells);

        var result = new List<PreparedInput>();

        foreach (var row in normalizedRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row.Number <= InputPreparationLimits.SpreadsheetHeaderRowNumber)
                continue;

            try
            {
                if (row.Cells.Keys.Any(
                        column => column > headers.MaxColumn))
                {
                    throw new SpreadsheetRowException(
                        "hive.input.spreadsheet.row-extra-column",
                        "Spreadsheet row contains values beyond the mapped header columns.");
                }

                var values = new Dictionary<string, string>(
                    headers.Names.Count,
                    StringComparer.OrdinalIgnoreCase);

                var hasValue = false;

                foreach (var headerEntry in headers.Names)
                {
                    var value = row.Cells.TryGetValue(
                        headerEntry.Key,
                        out var cellValue)
                        ? cellValue
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(value))
                        hasValue = true;

                    values.Add(
                        headerEntry.Value,
                        value);
                }

                if (!hasValue)
                    continue;

                result.Add(
                    new PreparedSpreadsheetRowInput(
                        submission.SubmissionId,
                        itemIndex,
                        item.FileName,
                        item.MediaType,
                        worksheetName,
                        row.Number,
                        values));
            }
            catch (SpreadsheetRowException exception)
            {
                failures.Add(
                    new InputPreparationFailure(
                        itemIndex,
                        item.FileName,
                        worksheetName + "!" + row.Number,
                        new Error(
                            exception.Code,
                            ErrorCategory.Validation,
                            exception.Message)));
            }
        }

        return result;
    }

    private static HeaderMap BuildHeaderMap(
        IReadOnlyDictionary<int, string> headerCells)
    {
        var maxColumn = headerCells.Keys.DefaultIfEmpty(0).Max();

        if (maxColumn <= 0)
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.header-empty",
                "Worksheet header row contains no mapped columns.");
        }

        if (maxColumn > InputPreparationLimits.MaxWorksheetColumns)
        {
            throw new SpreadsheetPackageLimitException(
                "hive.input.spreadsheet.too-many-columns",
                $"Worksheet exceeds the {InputPreparationLimits.MaxWorksheetColumns}-column limit.");
        }

        var names = new Dictionary<int, string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var column = 1; column <= maxColumn; column++)
        {
            if (!headerCells.TryGetValue(column, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.header-incomplete",
                    "Worksheet header columns must be contiguous and non-empty.");
            }

            var name = value.Trim();

            if (!seen.Add(name))
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.header-duplicate",
                    $"Worksheet header '{name}' is duplicated.");
            }

            names[column] = name;
        }

        return new HeaderMap(
            maxColumn,
            names);
    }

    private static Dictionary<int, string> ReadCells(
        XElement row,
        IReadOnlyList<string>? sharedStrings)
    {
        var result = new Dictionary<int, string>();
        var nextColumn = 1;

        foreach (var cell in row.Elements())
        {
            if (!string.Equals(
                    cell.Name.LocalName,
                    "c",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var reference = AttributeByLocalName(cell, "r");
            var column = string.IsNullOrWhiteSpace(reference)
                ? nextColumn
                : ParseColumnIndex(reference);

            if (column <= 0 ||
                column > InputPreparationLimits.MaxWorksheetColumns)
            {
                throw new SpreadsheetRowException(
                    "hive.input.spreadsheet.column-out-of-range",
                    $"Spreadsheet cell column exceeds the {InputPreparationLimits.MaxWorksheetColumns}-column limit.");
            }

            if (!result.TryAdd(
                    column,
                    ReadCellValue(cell, sharedStrings)))
            {
                throw new SpreadsheetRowException(
                    "hive.input.spreadsheet.duplicate-cell",
                    "Spreadsheet row contains duplicate cell columns.");
            }

            nextColumn = column + 1;
        }

        return result;
    }

    private static string ReadCellValue(
        XElement cell,
        IReadOnlyList<string>? sharedStrings)
    {
        var type = AttributeByLocalName(cell, "t")?.Trim();
        var valueElement = cell
            .Elements()
            .FirstOrDefault(
                static element =>
                    element.Name.LocalName == "v");

        var value = valueElement?.Value ?? string.Empty;

        if (string.Equals(type, "s", StringComparison.Ordinal))
        {
            if (!int.TryParse(
                    value,
                    out var index) ||
                sharedStrings is null ||
                index < 0 ||
                index >= sharedStrings.Count)
            {
                throw new SpreadsheetRowException(
                    "hive.input.spreadsheet.shared-string-invalid",
                    "Spreadsheet cell references an invalid shared string.");
            }

            value = sharedStrings[index];
        }
        else if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
        {
            value = string.Concat(
                cell
                    .Descendants()
                    .Where(static element => element.Name.LocalName == "t")
                    .Select(static element => element.Value));
        }
        else if (string.Equals(type, "b", StringComparison.Ordinal))
        {
            value = value switch
            {
                "1" => "TRUE",
                "0" => "FALSE",
                _ => throw new SpreadsheetRowException(
                    "hive.input.spreadsheet.boolean-invalid",
                    "Spreadsheet boolean cell contains an invalid value.")
            };
        }

        if (value.Length > InputPreparationLimits.MaxCellValueLength)
        {
            throw new SpreadsheetRowException(
                "hive.input.spreadsheet.cell-too-large",
                $"Spreadsheet cell exceeds the {InputPreparationLimits.MaxCellValueLength}-character limit.");
        }

        return value;
    }

    private static IReadOnlyList<string>? ReadSharedStrings(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");

        if (entry is null)
            return null;

        var document = LoadXml(
            entry,
            cancellationToken);

        var strings = new List<string>();
        var totalCharacters = 0L;

        foreach (var item in document
                     .Descendants()
                     .Where(static element => element.Name.LocalName == "si"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (strings.Count >= InputPreparationLimits.MaxSharedStrings)
            {
                throw new SpreadsheetPackageLimitException(
                    "hive.input.spreadsheet.too-many-shared-strings",
                    $"Spreadsheet exceeds the {InputPreparationLimits.MaxSharedStrings}-shared-string limit.");
            }

            var value = string.Concat(
                item
                    .Descendants()
                    .Where(static element => element.Name.LocalName == "t")
                    .Select(static element => element.Value));

            if (value.Length > InputPreparationLimits.MaxCellValueLength)
            {
                throw new SpreadsheetPackageLimitException(
                    "hive.input.spreadsheet.shared-string-too-large",
                    $"A shared string exceeds the {InputPreparationLimits.MaxCellValueLength}-character cell limit.");
            }

            totalCharacters += value.Length;

            if (totalCharacters > InputPreparationLimits.MaxSharedStringCharacters)
            {
                throw new SpreadsheetPackageLimitException(
                    "hive.input.spreadsheet.shared-strings-too-large",
                    $"Spreadsheet shared strings exceed the {InputPreparationLimits.MaxSharedStringCharacters}-character limit.");
            }

            strings.Add(value);
        }

        return strings;
    }

    private static IReadOnlyList<WorksheetDescriptor> ReadWorksheetTargets(
        XDocument workbook,
        XDocument relationships)
    {
        var relationshipMap = new Dictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var relationship in relationships
                     .Descendants()
                     .Where(static element => element.Name.LocalName == "Relationship"))
        {
            var id = AttributeByLocalName(relationship, "Id");
            var target = AttributeByLocalName(relationship, "Target");
            var mode = AttributeByLocalName(relationship, "TargetMode");

            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (string.Equals(
                    mode,
                    "External",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!relationshipMap.TryAdd(id.Trim(), target.Trim()))
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.relationship-duplicate",
                    "Spreadsheet workbook contains duplicate relationship identities.");
            }
        }

        var results = new List<WorksheetDescriptor>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in workbook
                     .Descendants()
                     .Where(static element => element.Name.LocalName == "sheet"))
        {
            var name = AttributeByLocalName(sheet, "name");
            var relationshipId = AttributeByLocalName(sheet, "id");

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(relationshipId))
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.sheet-invalid",
                    "Spreadsheet worksheet metadata is incomplete.");
            }

            var normalizedName = name.Trim();

            if (!names.Add(normalizedName))
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.sheet-duplicate",
                    $"Worksheet '{normalizedName}' is duplicated.");
            }

            if (!relationshipMap.TryGetValue(
                    relationshipId.Trim(),
                    out var target))
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.sheet-relationship-missing",
                    $"Worksheet '{normalizedName}' has no valid package relationship.");
            }

            results.Add(
                new WorksheetDescriptor(
                    normalizedName,
                    target));
        }

        return results;
    }

    private static int ReadRowNumber(
        XElement row,
        int fallback)
    {
        var value = AttributeByLocalName(row, "r");

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (!int.TryParse(value, out var number) ||
            number <= 0)
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.row-number-invalid",
                "Spreadsheet row number is invalid.");
        }

        return number;
    }

    private static int ParseColumnIndex(
        string cellReference)
    {
        var letters = cellReference
            .TakeWhile(static character =>
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z'))
            .ToArray();

        if (letters.Length == 0)
        {
            throw new SpreadsheetRowException(
                "hive.input.spreadsheet.cell-reference-invalid",
                "Spreadsheet cell reference is invalid.");
        }

        var result = 0;

        foreach (var letter in letters)
        {
            var normalized = char.ToUpperInvariant(letter) - 'A' + 1;

            if (normalized is < 1 or > 26)
            {
                throw new SpreadsheetRowException(
                    "hive.input.spreadsheet.cell-reference-invalid",
                    "Spreadsheet cell reference is invalid.");
            }

            result = checked(result * 26 + normalized);

            if (result > InputPreparationLimits.MaxWorksheetColumns)
            {
                return result;
            }
        }

        return result;
    }

    private static XDocument LoadXml(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        var bytes = ReadEntryBytes(
            entry,
            cancellationToken);

        using var stream = new MemoryStream(
            bytes,
            writable: false);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = InputPreparationLimits.MaxXmlCharacters,
            IgnoreComments = true
        };

        using var reader = XmlReader.Create(
            stream,
            settings);

        return XDocument.Load(
            reader,
            LoadOptions.None);
    }

    private static byte[] ReadEntryBytes(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.Length > InputPreparationLimits.MaxXmlEntryBytes)
        {
            throw new SpreadsheetPackageLimitException(
                "hive.input.spreadsheet.xml-entry-too-large",
                $"Spreadsheet XML entry exceeds the {InputPreparationLimits.MaxXmlEntryBytes}-byte limit.");
        }

        using var source = entry.Open();
        using var destination = new MemoryStream(
            entry.Length > 0
                ? checked((int)Math.Min(
                    entry.Length,
                    InputPreparationLimits.MaxXmlEntryBytes))
                : 0);

        var buffer = new byte[8192];
        long total = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = source.Read(
                buffer,
                0,
                buffer.Length);

            if (read == 0)
                break;

            total += read;

            if (total > InputPreparationLimits.MaxXmlEntryBytes)
            {
                throw new SpreadsheetPackageLimitException(
                    "hive.input.spreadsheet.xml-entry-too-large",
                    $"Spreadsheet XML entry exceeds the {InputPreparationLimits.MaxXmlEntryBytes}-byte limit.");
            }

            destination.Write(
                buffer,
                0,
                read);
        }

        return destination.ToArray();
    }

    private static string ResolvePackagePath(
        string baseEntry,
        string target)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            target.Contains('\\'))
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.relationship-target-invalid",
                "Spreadsheet relationship target is invalid.");
        }

        var baseDirectoryIndex = baseEntry.LastIndexOf('/');

        if (baseDirectoryIndex < 0)
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.relationship-target-invalid",
                "Spreadsheet package base path is invalid.");

        var baseDirectory = baseEntry[..(baseDirectoryIndex + 1)];
        var combined = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : baseDirectory + target;

        var segments = new List<string>();

        foreach (var segment in combined.Split('/'))
        {
            if (string.IsNullOrEmpty(segment) ||
                segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                throw new SpreadsheetPackageException(
                    "hive.input.spreadsheet.relationship-target-invalid",
                    "Spreadsheet relationship target attempts to leave the package.");
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new SpreadsheetPackageException(
                "hive.input.spreadsheet.relationship-target-invalid",
                "Spreadsheet relationship target is empty.");
        }

        return string.Join("/", segments);
    }

    private static bool IsImage(InputItem item) =>
        item.MediaType.StartsWith(
            "image/",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSpreadsheet(InputItem item) =>
        string.Equals(
            item.MediaType,
            SpreadsheetMediaType,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            Path.GetExtension(item.FileName),
            ".xlsx",
            StringComparison.OrdinalIgnoreCase);

    private static Error SerializationError(
        string code,
        string message) =>
        new(code, ErrorCategory.Serialization, message);

    private static string? AttributeByLocalName(
        XElement element,
        string localName) =>
        element
            .Attributes()
            .FirstOrDefault(
                attribute =>
                    string.Equals(
                        attribute.Name.LocalName,
                        localName,
                        StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static void AddFailure(
        List<InputPreparationFailure> failures,
        int itemIndex,
        InputItem item,
        string? sourceLocation,
        Error error)
    {
        failures.Add(
            new InputPreparationFailure(
                itemIndex,
                item.FileName,
                sourceLocation,
                error));
    }

    private sealed record WorksheetDescriptor(
        string Name,
        string Target);

    private sealed record HeaderMap(
        int MaxColumn,
        IReadOnlyDictionary<int, string> Names);

    private class SpreadsheetPackageException : Exception
    {
        public SpreadsheetPackageException(
            string code,
            string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    private sealed class SpreadsheetPackageLimitException : SpreadsheetPackageException
    {
        public SpreadsheetPackageLimitException(
            string code,
            string message)
            : base(code, message)
        {
        }
    }

    private sealed class SpreadsheetRowException : Exception
    {
        public SpreadsheetRowException(
            string code,
            string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
