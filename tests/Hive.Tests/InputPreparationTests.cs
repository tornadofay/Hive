using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class InputPreparationTests
{
    [Fact]
    public void ImageInput_RoutesToExplicitlySupportedVisionTarget()
    {
        var target = CreateTarget(
            "vision-target",
            CapabilityState.Supported);

        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.png",
                "image/png",
                new byte[] { 1, 2, 3, 4 })
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            [target]);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var prepared = Assert.Single(result.Value!.PreparedInputs);
        var image = Assert.IsType<PreparedImageInput>(prepared);

        Assert.Equal(target.Id, image.ExecutionTargetId);
        Assert.Equal(0, image.ItemIndex);
        Assert.Equal("invoice.png", image.FileName);
        Assert.Single(image.RoutingDiagnostics);
        Assert.Equal(
            ExecutionTargetSelectionDiagnosticStatus.Qualified,
            image.RoutingDiagnostics[0].Status);
    }

    [Fact]
    public void ImageInput_DoesNotTreatUnknownVisionAsSupported()
    {
        var target = CreateTarget(
            "unknown-vision",
            CapabilityState.Unknown);

        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.png",
                "image/png",
                new byte[] { 1, 2, 3, 4 })
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            [target]);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.PreparedInputs);

        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal(
            "hive.execution-target.selection.no-qualifying-target",
            failure.Error.Code);
        Assert.Equal(ErrorCategory.Unsupported, failure.Error.Category);
    }

    [Fact]
    public void SpreadsheetInput_PreparesRowsAcrossWorksheets()
    {
        var workbook = CreateWorkbook(
            ("Orders",
                [
                    ["Customer", "Amount"],
                    ["Ada", "10"],
                    ["Grace", "20"]
                ]),
            ("Credits",
                [
                    ["Customer", "Amount"],
                    ["Alan", "5"]
                ]));

        var submission = new InputSubmission(
        [
            new InputItem(
                "orders.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                workbook)
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.Failures);
        Assert.Equal(3, result.Value.PreparedInputs.Count);

        var first = Assert.IsType<PreparedSpreadsheetRowInput>(
            result.Value.PreparedInputs[0]);
        Assert.Equal("Orders", first.WorksheetName);
        Assert.Equal(2, first.RowNumber);
        Assert.Equal("Ada", first.Values["Customer"]);
        Assert.Equal("10", first.Values["Amount"]);

        var second = Assert.IsType<PreparedSpreadsheetRowInput>(
            result.Value.PreparedInputs[1]);
        Assert.Equal("Orders", second.WorksheetName);
        Assert.Equal(3, second.RowNumber);

        var third = Assert.IsType<PreparedSpreadsheetRowInput>(
            result.Value.PreparedInputs[2]);
        Assert.Equal("Credits", third.WorksheetName);
        Assert.Equal(2, third.RowNumber);
    }

    [Fact]
    public void SpreadsheetInput_UsesSharedStrings()
    {
        var workbook = CreateWorkbook(
            new[]
            {
                (
                    "Customers",
                    new string[][]
                    {
                        new[] { "Name", "City" },
                        new[] { "Ada", "Cairo" },
                        new[] { "Grace", "Giza" }
                    })
            },
            useSharedStrings: true);

        var result = InputPreparationEngine.Prepare(
            new InputSubmission(
            [
                new InputItem(
                    "customers.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    workbook)
            ]),
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.Failures);
        Assert.Equal(2, result.Value.PreparedInputs.Count);

        var row = Assert.IsType<PreparedSpreadsheetRowInput>(
            result.Value.PreparedInputs[0]);

        Assert.Equal("Ada", row.Values["Name"]);
        Assert.Equal("Cairo", row.Values["City"]);
    }

    [Fact]
    public void Submission_ContinuesAfterUnsupportedItem()
    {
        var workbook = CreateWorkbook(
            ("Orders",
                [
                    ["Customer", "Amount"],
                    ["Ada", "10"]
                ]));

        var result = InputPreparationEngine.Prepare(
            new InputSubmission(
            [
                new InputItem(
                    "notes.txt",
                    "text/plain",
                    Encoding.UTF8.GetBytes("not supported")),
                new InputItem(
                    "orders.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    workbook)
            ]),
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!.PreparedInputs);
        Assert.Single(result.Value.Failures);
        Assert.Equal("hive.input.unsupported", result.Value.Failures[0].Error.Code);
    }

    [Fact]
    public void SpreadsheetInput_RowFailureDoesNotAbortLaterRows()
    {
        var workbook = CreateWorkbook(
            ("Orders",
                [
                    ["Customer", "Amount"],
                    ["Bad", new string('x', InputPreparationLimits.MaxCellValueLength + 1)],
                    ["Good", "20"]
                ]));

        var result = InputPreparationEngine.Prepare(
            new InputSubmission(
            [
                new InputItem(
                    "orders.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    workbook)
            ]),
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        var prepared = Assert.Single(result.Value!.PreparedInputs);
        var row = Assert.IsType<PreparedSpreadsheetRowInput>(prepared);
        Assert.Equal(3, row.RowNumber);
        Assert.Equal("Good", row.Values["Customer"]);

        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal("Orders!2", failure.SourceLocation);
        Assert.Equal(
            "hive.input.spreadsheet.cell-too-large",
            failure.Error.Code);
    }

    [Fact]
    public void SpreadsheetInput_RejectsTooManyPhysicalRows()
    {
        var rows = new List<string[]>
        {
            new[] { "Name" }
        };

        for (var index = 0; index < InputPreparationLimits.MaxWorksheetRows; index++)
            rows.Add([$"Row-{index + 1}"]);

        var workbook = CreateWorkbook(("Large", rows.ToArray()));

        var result = InputPreparationEngine.Prepare(
            new InputSubmission(
            [
                new InputItem(
                    "large.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    workbook)
            ]),
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.PreparedInputs);

        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal("Large", failure.SourceLocation);
        Assert.Equal(
            "hive.input.spreadsheet.too-many-rows",
            failure.Error.Code);
        Assert.Equal(ErrorCategory.Validation, failure.Error.Category);
    }

    [Fact]
    public void SpreadsheetInput_FileLimitIsReportedAsItemFailure()
    {
        var bytes = new byte[InputPreparationLimits.MaxSpreadsheetBytes + 1];

        var result = InputPreparationEngine.Prepare(
            new InputSubmission(
            [
                new InputItem(
                    "large.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    bytes)
            ]),
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);

        var failure = Assert.Single(result.Value!.Failures);
        Assert.Equal(
            "hive.input.spreadsheet-too-large",
            failure.Error.Code);
        Assert.Empty(result.Value.PreparedInputs);
    }

    [Fact]
    public void InputPreparation_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.png",
                "image/png",
                new byte[] { 1, 2, 3 })
        ]);

        Assert.Throws<OperationCanceledException>(() =>
        {
            _ = InputPreparationEngine.Prepare(
                submission,
                Array.Empty<ExecutionTarget>(),
                cancellation.Token);
        });
    }

    [Fact]
    public void InputItemAndSubmission_EnforceAggregateLimits()
    {
        var tooManyItems = Enumerable.Range(
                0,
                InputSubmission.MaxItemCount + 1)
            .Select(_ => new InputItem(
                "item.png",
                "image/png",
                new byte[] { 1 }))
            .ToArray();

        var tooManyItemsException = Assert.Throws<ArgumentException>(
            () => new InputSubmission(tooManyItems));

        Assert.Contains(
            "more than",
            tooManyItemsException.Message,
            StringComparison.OrdinalIgnoreCase);

        var aggregateFirst = new byte[17 * 1024 * 1024];
        var aggregateSecond = new byte[17 * 1024 * 1024];

        var aggregateException = Assert.Throws<ArgumentException>(
            () => new InputSubmission(
            [
                new InputItem(
                    "first.bin",
                    "application/octet-stream",
                    aggregateFirst),
                new InputItem(
                    "second.bin",
                    "application/octet-stream",
                    aggregateSecond)
            ]));

        Assert.Contains(
            "total content",
            aggregateException.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ExecutionTarget CreateTarget(
        string key,
        CapabilityState visionState)
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            ProviderId.New(),
            ProviderAccountId.New(),
            key,
            key,
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    visionState)
            ]);
    }

    private static byte[] CreateWorkbook(
        params (string Name, string[][] Rows)[] sheets) =>
        CreateWorkbook(
            sheets,
            useSharedStrings: false);

    private static byte[] CreateWorkbook(
        (string Name, string[][] Rows)[] sheets,
        bool useSharedStrings)
    {
        const string mainNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string relationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string packageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        const string contentTypesNamespace =
            "http://schemas.openxmlformats.org/package/2006/content-types";

        var spreadsheet = XNamespace.Get(mainNamespace);
        var relationship = XNamespace.Get(relationshipNamespace);
        var packageRelationship = XNamespace.Get(packageRelationshipNamespace);
        var contentTypes = XNamespace.Get(contentTypesNamespace);

        var sharedStrings = new List<string>();
        var sharedStringMap = new Dictionary<string, int>(StringComparer.Ordinal);

        if (useSharedStrings)
        {
            foreach (var sheet in sheets)
            foreach (var row in sheet.Rows)
            foreach (var value in row)
            {
                if (!sharedStringMap.ContainsKey(value))
                {
                    sharedStringMap[value] = sharedStrings.Count;
                    sharedStrings.Add(value);
                }
            }
        }

        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(
                   memory,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            var contentTypeOverrides = sheets
                .Select(
                    (sheet, index) =>
                        new XElement(
                            contentTypes + "Override",
                            new XAttribute(
                                "PartName",
                                $"/xl/worksheets/sheet{index + 1}.xml"),
                            new XAttribute(
                                "ContentType",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));

            var contentTypesDocument = new XDocument(
                new XElement(
                    contentTypes + "Types",
                    new XElement(
                        contentTypes + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute(
                            "ContentType",
                            "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(
                        contentTypes + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute(
                            "ContentType",
                            "application/xml")),
                    new XElement(
                        contentTypes + "Override",
                        new XAttribute("PartName", "/xl/workbook.xml"),
                        new XAttribute(
                            "ContentType",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    contentTypeOverrides));

            WriteEntry(
                archive,
                "[Content_Types].xml",
                contentTypesDocument,
                Encoding.UTF8);

            var packageRootRelationships = new XDocument(
                new XElement(
                    packageRelationship + "Relationships",
                    new XElement(
                        packageRelationship + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute(
                            "Type",
                            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml"))));

            WriteEntry(
                archive,
                "_rels/.rels",
                packageRootRelationships,
                Encoding.UTF8);

            var workbookDocument = new XDocument(
                new XElement(
                    spreadsheet + "workbook",
                    new XElement(
                        spreadsheet + "sheets",
                        sheets.Select(
                            (sheet, index) =>
                                new XElement(
                                    spreadsheet + "sheet",
                                    new XAttribute("name", sheet.Name),
                                    new XAttribute("sheetId", index + 1),
                                    new XAttribute(
                                        relationship + "id",
                                        $"rId{index + 1}"))))));

            WriteEntry(
                archive,
                "xl/workbook.xml",
                workbookDocument,
                Encoding.UTF8);

            var workbookRelationshipsDocument = new XDocument(
                new XElement(
                    packageRelationship + "Relationships",
                    sheets.Select(
                        (_, index) =>
                            new XElement(
                                packageRelationship + "Relationship",
                                new XAttribute("Id", $"rId{index + 1}"),
                                new XAttribute(
                                    "Type",
                                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                                new XAttribute(
                                    "Target",
                                    $"worksheets/sheet{index + 1}.xml")))));

            WriteEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                workbookRelationshipsDocument,
                Encoding.UTF8);

            if (useSharedStrings)
            {
                var sharedStringsDocument = new XDocument(
                    new XElement(
                        spreadsheet + "sst",
                        new XAttribute("count", sharedStrings.Count),
                        new XAttribute("uniqueCount", sharedStrings.Count),
                        sharedStrings.Select(
                            value =>
                                new XElement(
                                    spreadsheet + "si",
                                    new XElement(
                                        spreadsheet + "t",
                                        value)))));

                WriteEntry(
                    archive,
                    "xl/sharedStrings.xml",
                    sharedStringsDocument,
                    Encoding.UTF8);
            }

            for (var index = 0; index < sheets.Length; index++)
            {
                var sheet = sheets[index];

                var rows = sheet.Rows.Select(
                    (row, rowIndex) =>
                        new XElement(
                            spreadsheet + "row",
                            new XAttribute("r", rowIndex + 1),
                            row.Select(
                                (value, columnIndex) =>
                                {
                                    var cell = new XElement(
                                        spreadsheet + "c",
                                        new XAttribute(
                                            "r",
                                            ToColumnName(columnIndex + 1) +
                                            (rowIndex + 1)));

                                    if (useSharedStrings)
                                    {
                                        cell.Add(
                                            new XAttribute("t", "s"),
                                            new XElement(
                                                spreadsheet + "v",
                                                sharedStringMap[value]));
                                    }
                                    else
                                    {
                                        cell.Add(
                                            new XAttribute("t", "inlineStr"),
                                            new XElement(
                                                spreadsheet + "is",
                                                new XElement(
                                                    spreadsheet + "t",
                                                    value)));
                                    }

                                    return cell;
                                })));

                var worksheetDocument = new XDocument(
                    new XElement(
                        spreadsheet + "worksheet",
                        new XElement(
                            spreadsheet + "sheetData",
                            rows)));

                WriteEntry(
                    archive,
                    $"xl/worksheets/sheet{index + 1}.xml",
                    worksheetDocument,
                    Encoding.UTF8);
            }
        }

        return memory.ToArray();
    }

    private static void WriteEntry(
        ZipArchive archive,
        string path,
        XDocument document,
        Encoding encoding) =>
        WriteEntry(
            archive,
            path,
            encoding.GetBytes(document.ToString(SaveOptions.DisableFormatting)));

    private static void WriteEntry(
        ZipArchive archive,
        string path,
        byte[] content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string ToColumnName(int column)
    {
        var value = column;
        var builder = new StringBuilder();

        while (value > 0)
        {
            value--;
            builder.Insert(
                0,
                (char)('A' + value % 26));
            value /= 26;
        }

        return builder.ToString();
    }
}
