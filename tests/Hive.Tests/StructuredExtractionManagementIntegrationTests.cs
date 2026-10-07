using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Hive.Coordination;
using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class StructuredExtractionManagementIntegrationTests
{
    [Fact]
    public async Task SpreadsheetMapping_IsProposedOnceAndReusedAcrossRows()
    {
        var database = new PersistenceTestDatabase("Hive_Test_Phase117_Management");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var now = new DateTimeOffset(
            2026,
            10,
            7,
            0,
            0,
            0,
            TimeSpan.Zero);

        var provider = CreateProvider(context, now);
        var account = CreateAccount(provider.Id, context, now);
        var target = CreateTarget(provider.Id, account.Id, context, now);
        var handler = new MappingResponseHandler();

        using var httpClient = new HttpClient(handler);

        using var management = new HiveManagementFacade(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerCapabilityDiscovery: null,
            clock: new FixedClock(now),
            structuredExtractionBatches: new SqlStructuredExtractionBatchStore(
                HiveEventPersistence.CreateSql(database.Options)),
            structuredExtractionEngine: new StructuredExtractionEngine(httpClient));

        Assert.True(
            (await management.CreateProviderAsync(provider, context)).IsSuccess);
        Assert.True(
            (await management.CreateProviderAccountAsync(account, context)).IsSuccess);
        Assert.True(
            (await management.CreateExecutionTargetAsync(target, context)).IsSuccess);

        var schema = CreateSchema();

        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                CreateWorkbook(
                    "Orders",
                    [
                        ["Invoice Number", "Customer", "Amount", "Line Description", "Quantity"],
                        ["INV-1", "Ada", "10.50", "Widget", "2"],
                        ["INV-2", "Grace", "20.00", "Cable", "3"]
                    ]))
        ]);

        var prepared = await management.PrepareInputAsync(
            submission,
            context);
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        Assert.Equal(2, prepared.Value!.PreparedInputs.Count);

        var created = await management.CreateStructuredExtractionBatchAsync(
            submission,
            schema,
            target.Id,
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var authorized = await management.AuthorizeStructuredExtractionProcessingAsync(
            created.Value!.Id,
            created.Value.Resource.Version,
            context);
        Assert.True(authorized.IsSuccess, authorized.Error?.Message);

        var processed = await management.ProcessStructuredExtractionBatchAsync(
            authorized.Value!.Id,
            prepared.Value.PreparedInputs,
            context);

        Assert.True(processed.IsSuccess, processed.Error?.Message);
        Assert.Equal(1, handler.CallCount);
        Assert.Single(processed.Value!.Mappings);
        Assert.Equal(
            SpreadsheetMappingReviewState.Proposed,
            processed.Value.Mappings.Single().ReviewState);
        Assert.All(
            processed.Value.Items.Where(
                item => item.SourceKind == InputSourceKind.Spreadsheet),
            item => Assert.Equal(
                StructuredExtractionItemStatus.Succeeded,
                item.Status));

        var acceptedMapping = await management.UpdateStructuredExtractionMappingAsync(
            processed.Value.Id,
            processed.Value.Mappings.Single().Context.Identity,
            processed.Value.Mappings.Single().Entries,
            SpreadsheetMappingReviewState.Accepted,
            processed.Value.Resource.Version,
            context);

        Assert.True(acceptedMapping.IsSuccess, acceptedMapping.Error?.Message);

        var edited = await management.UpdateStructuredCandidateFieldAsync(
            acceptedMapping.Value!.Id,
            0,
            new SemanticFieldId("customer.name"),
            "Ada Lovelace",
            null,
            acceptedMapping.Value.Resource.Version,
            context);

        Assert.True(edited.IsSuccess, edited.Error?.Message);
        Assert.Equal(
            "Ada Lovelace",
            edited.Value!.Items
                .Single(item => item.ItemIndex == 0)
                .Candidate!
                .Fields
                .Single(field => field.FieldId == new SemanticFieldId("customer.name"))
                .Value);

        var final = await management.AuthorizeStructuredExtractionAcceptedSetAsync(
            edited.Value.Id,
            [0, 1],
            edited.Value.Resource.Version,
            context);

        Assert.True(final.IsSuccess, final.Error?.Message);
        Assert.Equal(
            StructuredExtractionBatchStatus.Accepted,
            final.Value!.Status);
        Assert.Equal(
            [0, 1],
            final.Value.AcceptedItemIndexes.ToArray());
        Assert.All(
            final.Value.Items,
            item => Assert.Equal(
                StructuredExtractionItemStatus.Accepted,
                item.Status));
    }

    private static StructuredTargetSchema CreateSchema() =>
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
            ]);

    private static Provider CreateProvider(
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
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
            "phase117-management-provider-" + Guid.NewGuid().ToString("N"),
            "Phase 1.17 Management Provider",
            "openai-compatible");

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
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
            "phase117-management-account-" + Guid.NewGuid().ToString("N"),
            "Phase 1.17 Management Account");

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
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
            "phase117-management-target-" + Guid.NewGuid().ToString("N"),
            "Phase 1.17 Management Target",
            new Uri("https://example.invalid/v1/"),
            "phase117-test-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("structured.output"),
                    CapabilityState.Supported)
            ]);

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
            WriteXml(
                archive,
                "[Content_Types].xml",
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
                            new XAttribute(
                                "PartName",
                                "/xl/workbook.xml"),
                            new XAttribute(
                                "ContentType",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                        new XElement(
                            contentTypes + "Override",
                            new XAttribute(
                                "PartName",
                                "/xl/worksheets/sheet1.xml"),
                            new XAttribute(
                                "ContentType",
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));

            WriteBytes(
                archive,
                "_rels/.rels",
                Encoding.UTF8.GetBytes(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"{relationshipsNamespace}\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"));

            WriteXml(
                archive,
                "xl/workbook.xml",
                new XDocument(
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
                                    "rId1"))))));

            WriteXml(
                archive,
                "xl/_rels/workbook.xml.rels",
                new XDocument(
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
                                "worksheets/sheet1.xml")))));

            WriteXml(
                archive,
                "xl/worksheets/sheet1.xml",
                new XDocument(
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
                                                    new XAttribute(
                                                        "t",
                                                        "inlineStr"),
                                                    new XElement(
                                                        spreadsheet + "is",
                                                        new XElement(
                                                            spreadsheet + "t",
                                                            value)))))))))));
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

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) =>
            UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class MappingResponseHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"id":"mapping","model":"phase117-test-model","choices":[{"message":{"role":"assistant","content":"{\"mappings\":[{\"sourceColumn\":\"Invoice Number\",\"targetFieldId\":\"invoice.number\"},{\"sourceColumn\":\"Customer\",\"targetFieldId\":\"customer.name\"},{\"sourceColumn\":\"Amount\",\"targetFieldId\":\"invoice.amount\"},{\"sourceColumn\":\"Line Description\",\"targetFieldId\":\"line.description\"},{\"sourceColumn\":\"Quantity\",\"targetFieldId\":\"line.quantity\"}]}"}}]}""",
                        Encoding.UTF8,
                        "application/json")
                });
        }
    }
}
