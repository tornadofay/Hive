using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hive.Coordination;
using Hive.Core;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

public sealed class StructuredExtractionEngineTests
{
    [Fact]
    public async Task ImageExtraction_ProducesTypedParentChildCandidateAndSendsImageContent()
    {
        var target = CreateTarget(
            "engine-vision",
            CapabilityState.Supported,
            CapabilityState.Supported);

        var handler = new RecordingStructuredResponseHandler(
            """{"id":"candidate","model":"test-model","choices":[{"message":{"role":"assistant","content":"{\"invoice.number\":\"IMG-1\",\"invoice.amount\":25.50,\"lines\":[{\"line.description\":\"Cable\",\"line.quantity\":2}],\"confidence\":0.9}"}}]}""");
        using var client = new HttpClient(handler);
        var engine = new StructuredExtractionEngine(client);

        var input = new PreparedImageInput(
            Guid.NewGuid(),
            0,
            "invoice.png",
            "image/png",
            [1, 2, 3, 4],
            target.Id,
            [
                new ExecutionTargetSelectionDiagnostic(
                    target,
                    ExecutionTargetSelectionDiagnosticStatus.Qualified,
                    100,
                    ["vision supported"])
            ]);

        var result = await engine.ExtractImageAsync(
            input,
            CreateSchema(),
            target);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var candidate = result.Value!;
        Assert.True(candidate.IsValid);
        Assert.Equal("IMG-1", candidate.Fields.Single(
            field => field.FieldId == new SemanticFieldId("invoice.number")).Value);
        Assert.Equal("25.5", candidate.Fields.Single(
            field => field.FieldId == new SemanticFieldId("invoice.amount")).Value);
        Assert.Single(candidate.Children);
        Assert.Equal(
            "Cable",
            candidate.Children.Single().Fields.Single(
                field => field.FieldId == new SemanticFieldId("line.description")).Value);
        Assert.Contains(
            ""type":"image_url"",
            handler.LastRequestBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImageExtraction_MissingRequiredValue_RemainsReviewableAndInvalid()
    {
        var target = CreateTarget(
            "engine-missing",
            CapabilityState.Supported,
            CapabilityState.Supported);

        using var client = new HttpClient(
            new RecordingStructuredResponseHandler(
                """{"id":"candidate","model":"test-model","choices":[{"message":{"role":"assistant","content":"{\"invoice.number\":null,\"invoice.amount\":25.50,\"lines\":[]}"}}]}"""));

        var engine = new StructuredExtractionEngine(client);

        var input = CreatePreparedImage(target);

        var result = await engine.ExtractImageAsync(
            input,
            CreateSchema(),
            target);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value!.IsValid);
        Assert.Equal(
            StructuredValidationState.Missing,
            result.Value.Fields.Single(
                field => field.FieldId == new SemanticFieldId("invoice.number"))
                .ValidationState);
    }

    [Fact]
    public async Task ImageExtraction_RejectsExplicitStructuredOutputUnsupported()
    {
        var target = CreateTarget(
            "engine-unsupported",
            CapabilityState.Unsupported,
            CapabilityState.Supported);

        using var client = new HttpClient(
            new RecordingStructuredResponseHandler(
                """{"id":"unused","choices":[]}"""));

        var result = await new StructuredExtractionEngine(client)
            .ExtractImageAsync(
                CreatePreparedImage(target),
                CreateSchema(),
                target);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.structured-extraction.structured-output-unsupported",
            result.Error!.Code);
    }

    [Fact]
    public void SpreadsheetMapping_IsReusableWithoutAnotherProviderCall()
    {
        var schema = CreateSchema();
        var context = new SpreadsheetMappingContext(
            "mapping-context",
            "invoice.xlsx",
            "Orders",
            [
                "Invoice Number",
                "Customer",
                "Amount",
                "Line Description",
                "Quantity"
            ],
            [
                new Dictionary<string, string>
                {
                    ["Invoice Number"] = "INV-1",
                    ["Customer"] = "Ada",
                    ["Amount"] = "10.50",
                    ["Line Description"] = "Widget",
                    ["Quantity"] = "2"
                }
            ],
            "source",
            StructuredExtractionEngine.ComputeTargetSchemaFingerprint(schema));

        var mapping = StructuredExtractionEngine.ValidateMapping(
            context,
            schema,
            [
                new SpreadsheetMappingEntry(
                    "Invoice Number",
                    new SemanticFieldId("invoice.number")),
                new SpreadsheetMappingEntry(
                    "Customer",
                    new SemanticFieldId("customer.name")),
                new SpreadsheetMappingEntry(
                    "Amount",
                    new SemanticFieldId("invoice.amount")),
                new SpreadsheetMappingEntry(
                    "Line Description",
                    new SemanticFieldId("line.description")),
                new SpreadsheetMappingEntry(
                    "Quantity",
                    new SemanticFieldId("line.quantity"))
            ],
            SpreadsheetMappingReviewState.Accepted);

        Assert.True(mapping.IsSuccess);
        Assert.True(mapping.Value!.IsDeterministicallyValid);

        var submission = Guid.NewGuid();

        var first = new PreparedSpreadsheetRowInput(
            submission,
            0,
            "invoice.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Orders",
            2,
            context.SampleRows[0]);

        var second = new PreparedSpreadsheetRowInput(
            submission,
            1,
            "invoice.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Orders",
            3,
            new Dictionary<string, string>
            {
                ["Invoice Number"] = "INV-2",
                ["Customer"] = "Grace",
                ["Amount"] = "20.00",
                ["Line Description"] = "Adapter",
                ["Quantity"] = "3"
            });

        var engine = new StructuredExtractionEngine(
            new HttpClient(
                new RecordingStructuredResponseHandler(
                    """{"choices":[{"message":{"content":"unused"}}]}""")));

        var firstResult = engine.ApplySpreadsheetMapping(
            first,
            mapping.Value!,
            schema);
        var secondResult = engine.ApplySpreadsheetMapping(
            second,
            mapping.Value!,
            schema);

        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);
        Assert.True(secondResult.IsSuccess, secondResult.Error?.Message);
        Assert.True(firstResult.Value!.IsValid);
        Assert.True(secondResult.Value!.IsValid);
        Assert.Equal(
            "Ada",
            firstResult.Value.Fields.Single(
                field => field.FieldId == new SemanticFieldId("customer.name")).Value);
        Assert.Equal(
            "Grace",
            secondResult.Value.Fields.Single(
                field => field.FieldId == new SemanticFieldId("customer.name")).Value);
    }

    [Fact]
    public async Task ImageExtraction_CancellationDoesNotBecomeBusinessFailure()
    {
        var target = CreateTarget(
            "engine-cancel",
            CapabilityState.Supported,
            CapabilityState.Supported);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var engine = new StructuredExtractionEngine(
            new HttpClient(
                new RecordingStructuredResponseHandler(
                    """{"choices":[{"message":{"content":"unused"}}]}""")));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.ExtractImageAsync(
                CreatePreparedImage(target),
                CreateSchema(),
                target,
                cancellationToken: cancellation.Token));
    }

    private static PreparedImageInput CreatePreparedImage(
        ExecutionTarget target) =>
        new(
            Guid.NewGuid(),
            0,
            "invoice.png",
            "image/png",
            [1, 2, 3, 4],
            target.Id,
            [
                new ExecutionTargetSelectionDiagnostic(
                    target,
                    ExecutionTargetSelectionDiagnosticStatus.Qualified,
                    100,
                    ["qualified"])
            ]);

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

    private static ExecutionTarget CreateTarget(
        string key,
        CapabilityState structuredOutput,
        CapabilityState vision)
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        var providerId = ProviderId.New();
        var accountId = ProviderAccountId.New();

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
            providerId,
            accountId,
            key,
            "Test target",
            new Uri("https://example.invalid/v1/"),
            "test-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("structured.output"),
                    structuredOutput),
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    vision)
            ]);
    }

    private sealed class RecordingStructuredResponseHandler :
        HttpMessageHandler
    {
        private readonly string _responseBody;

        public RecordingStructuredResponseHandler(string responseBody) =>
            _responseBody = responseBody;

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");
            return response;
        }
    }
}
