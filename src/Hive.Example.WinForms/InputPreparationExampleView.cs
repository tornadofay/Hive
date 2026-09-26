using System.IO.Compression;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Example.WinForms;

internal sealed class InputPreparationExampleView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _context;
    private readonly IHiveExampleOutput _output;
    private readonly HiveExampleTestSurface _surface;

    public InputPreparationExampleView(
        IHiveManagementFacade management,
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        ArgumentNullException.ThrowIfNull(themeManager);

        _context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run input preparation"
        };

        _surface.SetInformation(
            "Prepares a mixed V1 submission through Hive.Management: images are routed to an execution target with explicitly supported vision capability, while .xlsx rows are mapped directly from workbook and worksheet data.",
            "The result is a set of prepared inputs plus isolated item/sheet/row failures. No provider request, structured candidate extraction, WorkItem creation, host mutation, or business write is performed in this phase.",
            "Scope",
            "Image routing, workbook/worksheet/row mapping, bounds, unsupported input, and per-item failure isolation");

        _surface.CodeSnippet = """
            var result = await management.PrepareInputAsync(
                submission,
                accessContext,
                cancellationToken);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        var provider = CreateProvider();
        EnsureSuccess(
            await _management.CreateProviderAsync(provider, _context, cancellationToken),
            "Provider creation");

        var account = CreateProviderAccount(provider.Id);
        EnsureSuccess(
            await _management.CreateProviderAccountAsync(account, _context, cancellationToken),
            "Provider account creation");

        var target = CreateExecutionTarget(provider.Id, account.Id);
        EnsureSuccess(
            await _management.CreateExecutionTargetAsync(target, _context, cancellationToken),
            "Vision target creation");

        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.png",
                "image/png",
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")),
            new InputItem(
                "orders.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                CreateWorkbook(
                    ("Orders",
                        [
                            ["Customer", "Amount"],
                            ["Ada", "10"],
                            ["Grace", "20"]
                        ]))),
            new InputItem(
                "notes.txt",
                "text/plain",
                Encoding.UTF8.GetBytes("unsupported example input"))
        ]);

        var prepared = await _management.PrepareInputAsync(
            submission,
            _context,
            cancellationToken);

        EnsureSuccess(prepared, "Input preparation");
        var result = prepared.Value!;

        _output.Write(
            "V1 Input Preparation & Routing",
            $"""
            Submission: {result.SubmissionId}
            Total source items: {submission.Items.Count}
            Prepared inputs: {result.PreparedInputs.Count}
            Isolated failures: {result.Failures.Count}

            Prepared:
            {FormatPrepared(result.PreparedInputs)}

            Failures:
            {FormatFailures(result.Failures)}

            Next boundary: Phase 1.16 structured extraction/validation
            WorkItem binding: Phase 1.18 end-to-end MAF pipeline
            Provider calls: none
            """);
    }

    private static string FormatPrepared(IReadOnlyList<PreparedInput> inputs)
    {
        if (inputs.Count == 0) return "  none";
        var builder = new StringBuilder();
        foreach (var input in inputs)
        {
            if (input is PreparedImageInput image)
            {
                builder.AppendLine($"  Image: {image.FileName} -> target {image.ExecutionTargetId}");
                continue;
            }
            if (input is PreparedSpreadsheetRowInput row)
            {
                builder.AppendLine($"  Spreadsheet: {row.FileName} / {row.WorksheetName}!{row.RowNumber} -> " +
                    $"{string.Join(", ", row.Values.Select(pair => $"{pair.Key}={pair.Value}"))}");
                continue;
            }
            builder.AppendLine($"  {input.SourceKind}: {input.FileName}");
        }
        return builder.ToString().TrimEnd();
    }

    private static string FormatFailures(IReadOnlyList<InputPreparationFailure> failures)
    {
        if (failures.Count == 0) return "  none";
        var builder = new StringBuilder();
        foreach (var failure in failures)
        {
            var location = failure.SourceLocation is null ? string.Empty : $" / {failure.SourceLocation}";
            builder.AppendLine($"  {failure.FileName}{location} -> {failure.Error.Code} [{failure.Error.Category}]");
        }
        return builder.ToString().TrimEnd();
    }

    private Provider CreateProvider()
    {
        var now = DateTimeOffset.UtcNow;
        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                _context.PrincipalId!.Value,
                ResourceScope.Tenant(_context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(_context.PrincipalId.Value, now, CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            $"phase15-example-provider-{Guid.NewGuid():N}",
            "Phase 1.15 Example Provider",
            "openai-compatible");
    }

    private ProviderAccount CreateProviderAccount(ProviderId providerId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                _context.PrincipalId!.Value,
                ResourceScope.Tenant(_context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(_context.PrincipalId.Value, now, CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            $"phase15-example-account-{Guid.NewGuid():N}",
            "Phase 1.15 Example Account");
    }

    private ExecutionTarget CreateExecutionTarget(ProviderId providerId, ProviderAccountId providerAccountId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                _context.PrincipalId!.Value,
                ResourceScope.Tenant(_context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(_context.PrincipalId.Value, now, CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            providerAccountId,
            $"phase15-example-vision-target-{Guid.NewGuid():N}",
            "Phase 1.15 Vision Target",
            new Uri("https://example.test/v1"),
            "example-vision-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Supported)
            ]);
    }

    private static byte[] CreateWorkbook(params (string Name, string[][] Rows)[] sheets)
    {
        const string packageContentTypesNamespace =
            "http://schemas.openxmlformats.org/package/2006/content-types";
        const string packageRelationshipsNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        const string officeRelationshipsNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string mainNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var spreadsheet = XNamespace.Get(mainNamespace);
        var contentTypes = XNamespace.Get(packageContentTypesNamespace);
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(
                   memory,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            var contentTypeOverrides =
                sheets.Select(
                    (sheet, index) =>
                        new XElement(
                            contentTypes + "Override",
                            new XAttribute(
                                "PartName",
                                $"/xl/worksheets/sheet{index + 1}.xml"),
                            new XAttribute(
                                "ContentType",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));

            var contentTypesDocument =
                new XDocument(
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

            WriteXml(
                archive,
                "[Content_Types].xml",
                contentTypesDocument);

            WriteBytes(
                archive,
                "_rels/.rels",
                Encoding.UTF8.GetBytes(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"{packageRelationshipsNamespace}\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"));

            var relationshipAttribute =
                XNamespace.Get(officeRelationshipsNamespace) + "id";

            var workbookDocument =
                new XDocument(
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
                                            relationshipAttribute,
                                            $"rId{index + 1}")))));

            WriteXml(
                archive,
                "xl/workbook.xml",
                workbookDocument);

            var workbookRelationshipsDocument =
                new XDocument(
                    new XElement(
                        XNamespace.Get(packageRelationshipsNamespace) + "Relationships",
                        sheets.Select(
                            (_, index) =>
                                new XElement(
                                    XNamespace.Get(packageRelationshipsNamespace) + "Relationship",
                                    new XAttribute("Id", $"rId{index + 1}"),
                                    new XAttribute(
                                        "Type",
                                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                                    new XAttribute(
                                        "Target",
                                        $"worksheets/sheet{index + 1}.xml")))));

            WriteXml(
                archive,
                "xl/_rels/workbook.xml.rels",
                workbookRelationshipsDocument);

            foreach (var (sheet, index) in sheets.Select(
                         (value, index) => (value, index)))
            {
                var rows =
                    sheet.Rows.Select(
                        (row, rowIndex) =>
                            new XElement(
                                spreadsheet + "row",
                                new XAttribute("r", rowIndex + 1),
                                row.Select(
                                    (value, columnIndex) =>
                                        new XElement(
                                            spreadsheet + "c",
                                            new XAttribute(
                                                "r",
                                                ToColumnName(columnIndex + 1) +
                                                (rowIndex + 1)),
                                            new XAttribute("t", "inlineStr"),
                                            new XElement(
                                                spreadsheet + "is",
                                                new XElement(
                                                    spreadsheet + "t",
                                                    value)))));

                var worksheetDocument =
                    new XDocument(
                        new XElement(
                            spreadsheet + "worksheet",
                            new XElement(
                                spreadsheet + "sheetData",
                                rows)));

                WriteXml(
                    archive,
                    $"xl/worksheets/sheet{index + 1}.xml",
                    worksheetDocument);
            }
        }

        return memory.ToArray();
    }

    private static void WriteXml(ZipArchive archive, string path, XDocument document) =>
        WriteBytes(archive, path, Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));

    private static void WriteBytes(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ToColumnName(int column)
    {
        var result = string.Empty;
        while (column > 0)
        {
            column--;
            result = (char)('A' + column % 26) + result;
            column /= 26;
        }
        return result;
    }

    private static void EnsureSuccess<T>(Result<T> result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }
}
