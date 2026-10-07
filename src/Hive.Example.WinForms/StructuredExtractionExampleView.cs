using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Hive.Coordination;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class StructuredExtractionExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly HiveExampleTestSurface _surface;

    public StructuredExtractionExampleView(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run structured extraction"
        };

        _surface.SetInformation(
            "Exercises the Phase 1.17 boundary from bounded folder selection through semantic mapping, independent vision extraction, deterministic validation, human review, and accepted-subset authorization.",
            "The scenario is deterministic and local-only. It creates two XLSX files, an image, and an unsupported text file, uses one fake mapping response for the compatible spreadsheet context, reuses that mapping for every matching row, extracts the image independently, records failures, edits the mapping/candidates through Management, and never mutates host business state.",
            "Scenario",
            "Single folder selection, bounded mixed input, one-time mapping reuse, vision, parent/child candidates, provenance, review, acceptance, no host mutation");

        _surface.CodeSnippet = """
            var selection = await InputSelectionBuilder.BuildFolderAsync(
                folderPath,
                includeSubfolders: true,
                cancellationToken);

            var batch = await management.CreateStructuredExtractionBatchAsync(
                selection.Value.Submission,
                targetSchema,
                mappingExecutionTargetId,
                accessContext,
                cancellationToken);

            await management.AuthorizeStructuredExtractionProcessingAsync(...);
            await management.ProcessStructuredExtractionBatchAsync(...);
            await management.UpdateStructuredExtractionMappingAsync(...);
            await management.UpdateStructuredCandidateFieldAsync(...);
            await management.AuthorizeStructuredExtractionAcceptedSetAsync(...);
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
        var database = HiveDatabaseOptions.LocalDevelopment(
            $"Hive_Example_Phase117_{Guid.NewGuid():N}");

        EnsureSuccess(
            await new HiveDatabaseMigrator(database)
                .MigrateAsync(cancellationToken),
            "Hive database migration");

        var eventPersistence = HiveEventPersistence.CreateSql(database);
        var providerStore = new SqlProviderResourceStore(database);
        var agentStore = new SqlAgentDefinitionResourceStore(database);
        var workItemStore = new SqlWorkItemResourceStore(database);

        var clock = new FixedClock(
            new DateTimeOffset(
                2026,
                10,
                7,
                0,
                0,
                0,
                TimeSpan.Zero));

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var provider = CreateProvider(context, clock.UtcNow);
        var account = CreateAccount(provider.Id, context, clock.UtcNow);
        var target = CreateTarget(
            provider.Id,
            account.Id,
            context,
            clock.UtcNow);

        var capabilityDiscovery = new StaticProviderDiscovery(
            clock,
            provider.Id,
            account.Id,
            target.Endpoint,
            target.Model!);

        var responseHandler = new StructuredExtractionResponseHandler();
        using var httpClient = new HttpClient(responseHandler);

        var extractionEngine = new StructuredExtractionEngine(httpClient);
        var batchStore = new SqlStructuredExtractionBatchStore(
            eventPersistence,
            clock);

        using var management = new HiveManagementFacade(
            providerStore,
            agentStore,
            workItemStore,
            providerCapabilityDiscovery: capabilityDiscovery,
            clock: clock,
            structuredExtractionBatches: batchStore,
            structuredExtractionEngine: extractionEngine);

        EnsureSuccess(
            await management.CreateProviderAsync(
                provider,
                context,
                cancellationToken),
            "Provider creation");

        EnsureSuccess(
            await management.CreateProviderAccountAsync(
                account,
                context,
                cancellationToken),
            "Provider account creation");

        EnsureSuccess(
            await management.CreateExecutionTargetAsync(
                target,
                context,
                cancellationToken),
            "Execution target creation");

        var schema = CreateTargetSchema();
        var folderPath = CreateScenarioFolder();

        try
        {
            var selected = await InputSelectionBuilder.BuildFolderAsync(
                folderPath,
                includeSubfolders: true,
                cancellationToken);

            EnsureSuccess(selected, "Folder input selection");

            var submission = selected.Value!.Submission
                ?? throw new InvalidOperationException(
                    "The example folder did not produce any input items.");

            var prepared = await management.PrepareInputAsync(
                submission,
                context,
                cancellationToken);

            EnsureSuccess(prepared, "Input preparation");

            var batchResult = await management.CreateStructuredExtractionBatchAsync(
                submission,
                schema,
                target.Id,
                context,
                cancellationToken);

            EnsureSuccess(batchResult, "Structured extraction batch creation");
            var batch = batchResult.Value!;

            var authorized = await management.AuthorizeStructuredExtractionProcessingAsync(
                batch.Id,
                batch.Resource.Version,
                context,
                cancellationToken);

            EnsureSuccess(authorized, "Processing authorization");
            batch = authorized.Value!;

            var processed = await management.ProcessStructuredExtractionBatchAsync(
                batch.Id,
                prepared.Value!.PreparedInputs,
                context,
                cancellationToken);

            EnsureSuccess(processed, "Structured extraction processing");
            batch = processed.Value!;

            var mapping = batch.Mappings.SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "The example did not produce the expected spreadsheet mapping.");

            var acceptedMapping = await management.UpdateStructuredExtractionMappingAsync(
                batch.Id,
                mapping.Context.Identity,
                mapping.Entries,
                SpreadsheetMappingReviewState.Accepted,
                batch.Resource.Version,
                context,
                cancellationToken);

            EnsureSuccess(acceptedMapping, "Mapping review");
            batch = acceptedMapping.Value!;

            var customerField = new SemanticFieldId("customer.name");
            var editedCandidate = batch.Items
                .FirstOrDefault(
                    item => item.Candidate is not null &&
                            item.SourceKind == InputSourceKind.Spreadsheet)
                ?? throw new InvalidOperationException(
                    "The example did not produce a spreadsheet candidate.");

            var edited = await management.UpdateStructuredCandidateFieldAsync(
                batch.Id,
                editedCandidate.ItemIndex,
                customerField,
                "Ada Lovelace",
                childIndex: null,
                batch.Resource.Version,
                context,
                cancellationToken);

            EnsureSuccess(edited, "Candidate review edit");
            batch = edited.Value!;

            var acceptedIndexes = batch.Items
                .Where(
                    item => item.Status == StructuredExtractionItemStatus.Succeeded &&
                            item.Candidate is not null &&
                            item.Candidate.IsValid)
                .Select(static item => item.ItemIndex)
                .ToArray();

            var accepted = await management.AuthorizeStructuredExtractionAcceptedSetAsync(
                batch.Id,
                acceptedIndexes,
                batch.Resource.Version,
                context,
                cancellationToken);

            EnsureSuccess(accepted, "Accepted candidate authorization");
            batch = accepted.Value!;

            var reloaded = await management.GetStructuredExtractionBatchAsync(
                batch.Id,
                context,
                cancellationToken);

            EnsureSuccess(reloaded, "Durable batch reload");

            _output.Write(
                "Phase 1.17 Structured Extraction & Validation",
                FormatOutput(
                    folderPath,
                    submission,
                    prepared.Value!,
                    batch,
                    responseHandler.MappingCalls,
                    responseHandler.CandidateCalls,
                    reloaded.Value!));
        }
        finally
        {
            try
            {
                Directory.Delete(folderPath, recursive: true);
            }
            catch
            {
                // Example cleanup must not hide the scenario result.
            }
        }
    }

    private static string FormatOutput(
        string folderPath,
        InputSubmission submission,
        InputPreparationResult prepared,
        StructuredExtractionBatch batch,
        int mappingCalls,
        int candidateCalls,
        StructuredExtractionBatch reloaded)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Scenario folder: {folderPath}");
        builder.AppendLine($"Source items: {submission.Items.Count}");
        builder.AppendLine($"Prepared inputs: {prepared.PreparedInputs.Count}");
        builder.AppendLine($"Preparation failures: {prepared.Failures.Count}");
        builder.AppendLine($"Mapping LLM calls: {mappingCalls}");
        builder.AppendLine($"Structured candidate calls: {candidateCalls}");
        builder.AppendLine($"Mapping contexts: {batch.Mappings.Count}");
        builder.AppendLine($"Final batch status: {batch.Status}");
        builder.AppendLine($"Reloaded status: {reloaded.Status}");
        builder.AppendLine();
        builder.AppendLine("Items:");

        foreach (var item in batch.Items.OrderBy(static item => item.ItemIndex))
        {
            var source =
                item.SourceKind?.ToString() ?? "Unsupported";
            var location =
                item.RowNumber is null
                    ? string.Empty
                    : $" / {item.WorksheetName}!row {item.RowNumber}";

            builder.AppendLine(
                $"  [{item.ItemIndex}] {source} {item.FileName}{location} -> {item.Status}");

            if (item.Candidate is { } candidate)
            {
                builder.AppendLine(
                    $"      candidate={candidate.Id}, valid={candidate.IsValid}, provenance={candidate.Provenance.SourceKind}");

                foreach (var field in candidate.Fields)
                {
                    builder.AppendLine(
                        $"      {field.FieldId} = {field.Value ?? "<null>"} [{field.ValidationState}]");
                }

                foreach (var child in candidate.Children)
                {
                    builder.AppendLine(
                        $"      child {child.CollectionKey}: {child.Fields.Count} fields");
                }
            }

            if (item.SafeErrorMessage is not null)
            {
                builder.AppendLine(
                    $"      failure={item.ErrorCode} [{item.ErrorCategory}] {item.SafeErrorMessage}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            "Business writes performed: 0");
        builder.AppendLine(
            "Host controls or business database mutated: 0");

        return builder.ToString();
    }

    private static StructuredTargetSchema CreateTargetSchema() =>
        new(
            [
                new StructuredTargetField(
                    new SemanticFieldId("invoice.number"),
                    "Invoice Number",
                    StructuredValueType.String,
                    required: true),
                new StructuredTargetField(
                    new SemanticFieldId("customer.name"),
                    "Customer Name",
                    StructuredValueType.String,
                    required: true),
                new StructuredTargetField(
                    new SemanticFieldId("invoice.amount"),
                    "Invoice Amount",
                    StructuredValueType.Decimal,
                    required: true),
                new StructuredTargetField(
                    new SemanticFieldId("line.description"),
                    "Line Description",
                    StructuredValueType.String,
                    required: true,
                    placement: StructuredFieldPlacement.Child,
                    childCollectionKey: "lines"),
                new StructuredTargetField(
                    new SemanticFieldId("line.quantity"),
                    "Line Quantity",
                    StructuredValueType.Int64,
                    required: true,
                    placement: StructuredFieldPlacement.Child,
                    childCollectionKey: "lines")
            ],
            schemaId: "phase1.17.example.invoice");

    private static string CreateScenarioFolder()
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "HivePhase117_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "nested"));

        File.WriteAllBytes(
            Path.Combine(folder, "invoice-a.png"),
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

        File.WriteAllBytes(
            Path.Combine(folder, "invoice-a.xlsx"),
            CreateWorkbook(
                "Orders",
                [
                    ["Invoice Number", "Customer", "Amount", "Line Description", "Quantity"],
                    ["INV-100", "Grace", "100.50", "Widget", "2"],
                    ["INV-101", "Alan", "25.00", "Cable", "1"]
                ]));

        File.WriteAllBytes(
            Path.Combine(folder, "invoice-b.xlsx"),
            CreateWorkbook(
                "Orders",
                [
                    ["Invoice Number", "Customer", "Amount", "Line Description", "Quantity"],
                    ["INV-200", "Ada", "75.00", "Adapter", "3"]
                ]));

        File.WriteAllText(
            Path.Combine(folder, "notes.txt"),
            "Unsupported example input.");

        File.WriteAllText(
            Path.Combine(folder, "nested", "extra.txt"),
            "Nested unsupported example input.");

        return folder;
    }

    private static byte[] CreateWorkbook(
        string sheetName,
        string[][] rows)
    {
        const string contentTypesNamespace =
            "http://schemas.openxmlformats.org/package/2006/content-types";
        const string relationshipsNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        const string officeRelationshipsNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string spreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var spreadsheet = XNamespace.Get(spreadsheetNamespace);
        var contentTypes = XNamespace.Get(contentTypesNamespace);
        var relationships = XNamespace.Get(relationshipsNamespace);
        var officeRelationships = XNamespace.Get(officeRelationshipsNamespace);

        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(
                   memory,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            var types = new XDocument(
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
                        new XAttribute("ContentType", "application/xml")),
                    new XElement(
                        contentTypes + "Override",
                        new XAttribute("PartName", "/xl/workbook.xml"),
                        new XAttribute(
                            "ContentType",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(
                        contentTypes + "Override",
                        new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                        new XAttribute(
                            "ContentType",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));

            WriteXml(archive, "[Content_Types].xml", types);

            WriteBytes(
                archive,
                "_rels/.rels",
                Encoding.UTF8.GetBytes(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"{relationshipsNamespace}\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"));

            var workbook = new XDocument(
                new XElement(
                    spreadsheet + "workbook",
                    new XElement(
                        spreadsheet + "sheets",
                        new XElement(
                            spreadsheet + "sheet",
                            new XAttribute("name", sheetName),
                            new XAttribute("sheetId", 1),
                            new XAttribute(
                                officeRelationships + "id",
                                "rId1")))));

            WriteXml(archive, "xl/workbook.xml", workbook);

            var workbookRels = new XDocument(
                new XElement(
                    relationships + "Relationships",
                    new XElement(
                        relationships + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute(
                            "Type",
                            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute(
                            "Target",
                            "worksheets/sheet1.xml"))));

            WriteXml(
                archive,
                "xl/_rels/workbook.xml.rels",
                workbookRels);

            var worksheet = new XDocument(
                new XElement(
                    spreadsheet + "worksheet",
                    new XElement(
                        spreadsheet + "sheetData",
                        rows.Select(
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
                                                        value)))))))));

            WriteXml(
                archive,
                "xl/worksheets/sheet1.xml",
                worksheet);
        }

        return memory.ToArray();
    }

    private static void WriteXml(
        ZipArchive archive,
        string path,
        XDocument document) =>
        WriteBytes(
            archive,
            path,
            Encoding.UTF8.GetBytes(
                document.ToString(SaveOptions.DisableFormatting)));

    private static void WriteBytes(
        ZipArchive archive,
        string path,
        byte[] bytes)
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

    private static Provider CreateProvider(
        ResourceAccessContext context,
        DateTimeOffset now)
    {
        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "phase117-example-provider-" + Guid.NewGuid().ToString("N"),
            "Phase 1.17 Example Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        ResourceAccessContext context,
        DateTimeOffset now)
    {
        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            "phase117-example-account-" + Guid.NewGuid().ToString("N"),
            "Phase 1.17 Example Account");
    }

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        ResourceAccessContext context,
        DateTimeOffset now)
    {
        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            "phase117-example-target-" + Guid.NewGuid().ToString("N"),
            "Phase 1.17 Vision and Mapping Target",
            new Uri("https://example.invalid/v1/"),
            "phase117-structured-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Supported),
                new CapabilityStateEntry(
                    new CapabilityKey("structured.output"),
                    CapabilityState.Supported)
            ]);
    }

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) =>
            UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class StaticProviderDiscovery : IProviderCapabilityDiscovery
    {
        private readonly IClock _clock;
        private readonly ProviderId _providerId;
        private readonly ProviderAccountId _accountId;
        private readonly Uri _endpoint;
        private readonly string _modelId;

        public StaticProviderDiscovery(
            IClock clock,
            ProviderId providerId,
            ProviderAccountId accountId,
            Uri endpoint,
            string modelId)
        {
            _clock = clock;
            _providerId = providerId;
            _accountId = accountId;
            _endpoint = endpoint;
            _modelId = modelId;
        }

        public Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = _clock.UtcNow;

            return Task.FromResult(
                Result<ProviderDiscoverySnapshot>.Success(
                    new ProviderDiscoverySnapshot(
                        _providerId,
                        _accountId,
                        _endpoint,
                        new ProviderOperationalMetadata(
                            ProviderAvailabilityStatus.Available,
                            ProviderHealthStatus.Unknown,
                            now,
                            now.AddMinutes(10)),
                        ProviderDiscoveryState.Supported,
                        [
                            new ProviderModelMetadata(
                                modelId: _modelId,
                                ownedBy: "phase117-example",
                                createdAtUtc: null,
                                availability: ProviderAvailabilityStatus.Available,
                                health: ProviderHealthStatus.Unknown,
                                discoveredCapabilities:
                                [
                                    new CapabilityStateEntry(
                                        new CapabilityKey("vision"),
                                        CapabilityState.Supported),
                                    new CapabilityStateEntry(
                                        new CapabilityKey("structured.output"),
                                        CapabilityState.Supported)
                                ],
                                pricing: null,
                                observedAtUtc: now,
                                staleAfterUtc: now.AddMinutes(10))
                        ])));
        }
    }

    private sealed class StructuredExtractionResponseHandler :
        HttpMessageHandler
    {
        public int MappingCalls { get; private set; }

        public int CandidateCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var body = request.Content is null
                ? string.Empty
                : await request.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

            var isMapping = body.Contains(
                "hive_spreadsheet_mapping",
                StringComparison.Ordinal);

            if (isMapping)
                MappingCalls++;
            else
                CandidateCalls++;

            var responseBody = isMapping
                ? """
                  {"id":"phase117-mapping","model":"phase117-structured-model","choices":[{"message":{"role":"assistant","content":"{\"mappings\":[{\"sourceColumn\":\"Invoice Number\",\"targetFieldId\":\"invoice.number\"},{\"sourceColumn\":\"Customer\",\"targetFieldId\":\"customer.name\"},{\"sourceColumn\":\"Amount\",\"targetFieldId\":\"invoice.amount\"},{\"sourceColumn\":\"Line Description\",\"targetFieldId\":\"line.description\"},{\"sourceColumn\":\"Quantity\",\"targetFieldId\":\"line.quantity\"}]}"}}]}
                  """
                : """
                  {"id":"phase117-candidate","model":"phase117-structured-model","choices":[{"message":{"role":"assistant","content":"{\"invoice.number\":\"IMG-001\",\"customer.name\":\"Grace Hopper\",\"invoice.amount\":50.50,\"lines\":[{\"line.description\":\"Vision Item\",\"line.quantity\":2}],\"confidence\":0.91}"}}]}
                  """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };

            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");

            return response;
        }
    }
}
