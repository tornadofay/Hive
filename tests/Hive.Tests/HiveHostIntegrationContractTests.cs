using Hive.Core;
using Hive.Management;
using Xunit;

namespace Hive.Tests;

public sealed class HiveHostIntegrationContractTests
{
    [Fact]
    public void HostValues_PreserveSupportedScalarKindsWithoutHostObjects()
    {
        var text = HiveHostValue.FromString("Invoice");
        var boolean = HiveHostValue.FromBoolean(true);
        var integer = HiveHostValue.FromInt64(42);
        var decimalValue = HiveHostValue.FromDecimal(12.50m);
        var date = HiveHostValue.FromDateTime(
            new DateTime(2026, 9, 25, 10, 20, 0, DateTimeKind.Utc));

        Assert.Equal(HiveHostValueKind.String, text.Kind);
        Assert.Equal("Invoice", text.AsString());
        Assert.True(boolean.TryGetBoolean(out var booleanResult));
        Assert.True(booleanResult);
        Assert.True(integer.TryGetInt64(out var integerResult));
        Assert.Equal(42, integerResult);
        Assert.True(decimalValue.TryGetDecimal(out var decimalResult));
        Assert.Equal(12.50m, decimalResult);
        Assert.True(date.TryGetDateTime(out var dateResult));
        Assert.Equal(DateTimeKind.Utc, dateResult.Kind);
    }

    [Fact]
    public void RowIdentity_IsOpaqueAndIndependentFromPosition()
    {
        var first = new HiveHostRow(
            new HiveHostRowIdentity("101"),
            0,
            new Dictionary<string, HiveHostValue>
            {
                ["Id"] = HiveHostValue.FromInt64(101)
            });

        var moved = new HiveHostRow(
            first.Identity,
            4,
            new Dictionary<string, HiveHostValue>
            {
                ["Id"] = HiveHostValue.FromInt64(101)
            });

        Assert.Equal(first.Identity, moved.Identity);
        Assert.NotEqual(first.Position, moved.Position);
    }

    [Fact]
    public void ParentChildSurfaceAndGeneratedComputedFieldSemanticsAreExplicit()
    {
        var parent = new HiveHostDataSurfaceDescriptor(
            "invoice",
            "Invoices",
            1,
            new[]
            {
                new HiveHostFieldDescriptor(
                    "Id",
                    "Id",
                    typeof(long).FullName!,
                    true,
                    true,
                    false,
                    false,
                    true),
                new HiveHostFieldDescriptor(
                    "InvoiceNumber",
                    "InvoiceNumber",
                    typeof(string).FullName!,
                    true,
                    true,
                    false,
                    true,
                    false),
                new HiveHostFieldDescriptor(
                    "Total",
                    "Total",
                    typeof(decimal).FullName!,
                    true,
                    true,
                    true,
                    false,
                    false)
            },
            Array.Empty<HiveHostCapabilityDescriptor>(),
            new[]
            {
                new HiveHostChildDataSurfaceDescriptor(
                    "invoice",
                    "invoice-lines",
                    "Id",
                    "InvoiceId")
            });

        Assert.True(parent.Fields.Single(field => field.Name == "Id").IsPrimaryKey);
        Assert.True(parent.Fields.Single(field => field.Name == "InvoiceNumber").Generated);
        Assert.True(parent.Fields.Single(field => field.Name == "Total").Computed);
        Assert.Single(parent.Children);
        Assert.Equal("InvoiceId", parent.Children[0].ChildKeyField);
    }

    [Theory]
    [InlineData(
        HiveBusinessOperationImplementation.Api,
        1)]
    [InlineData(
        HiveBusinessOperationImplementation.Ui,
        1)]
    [InlineData(
        HiveBusinessOperationImplementation.ApiAndUi,
        2)]
    public void BusinessOperationComposition_MapsImplementationToStages(
        HiveBusinessOperationImplementation implementation,
        int stageCount)
    {
        var correlation = CorrelationId.New();
        var composition = new HiveBusinessOperationComposition(
            "SaveInvoice",
            implementation,
            correlation,
            "ReferenceAdapter");

        Assert.Equal(correlation, composition.CorrelationId);
        Assert.Equal("ReferenceAdapter", composition.AdapterId);
        Assert.Equal(stageCount, composition.Stages.Count);
        Assert.Equal(implementation, composition.Implementation);
    }

    [Fact]
    public async Task Management_DeniesHostCapabilityBeforeAdapterInvocation()
    {
        var adapter = new FakeHostAdapter();
        var deniedCapability = Guid.NewGuid();
        var authorizer = new FakeAuthorizer(deniedCapability);
        var service = new HiveHostIntegrationService(authorizer);
        var context = CreateAccessContext();

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                deniedCapability,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: "control:0/0",
                value: HiveHostValue.FromString("blocked")),
            context);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error!.Category);
        Assert.Equal(0, adapter.InteractionCalls);
    }

    [Fact]
    public async Task Management_ForwardsAuthorizedInteractionAndPreservesCorrelation()
    {
        var adapter = new FakeHostAdapter();
        var authorizer = new FakeAuthorizer();
        var service = new HiveHostIntegrationService(authorizer);
        var context = CreateAccessContext();
        var correlation = CorrelationId.New();

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                adapter.EditCapabilityId,
                HiveHostInteractionKind.SetControlValue,
                correlation,
                controlId: "control:0/0",
                value: HiveHostValue.FromString("accepted")),
            context);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, adapter.InteractionCalls);
        Assert.Equal(correlation, result.Value!.CorrelationId);
    }

    [Fact]
    public async Task Management_RequiresAuthorizedDependentLookup()
    {
        var adapter = new FakeHostAdapter();
        var authorizer = new FakeAuthorizer();
        var service = new HiveHostIntegrationService(authorizer);
        var context = CreateAccessContext();

        var request = new HiveLookupRequest(
            "products",
            adapter.LookupCapabilityId,
            new Dictionary<string, HiveHostValue>
            {
                ["CategoryId"] = HiveHostValue.FromInt64(10)
            });

        var result = await service.ResolveLookupAsync(
            adapter,
            request,
            context);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!);
        Assert.Equal(10L, adapter.LastLookupCategoryId);
    }

    [Fact]
    public async Task Management_PreparesApiUiCompositionWithoutExecutingBusinessWrite()
    {
        var adapter = new FakeHostAdapter();
        var authorizer = new FakeAuthorizer();
        var service = new HiveHostIntegrationService(authorizer);
        var context = CreateAccessContext();
        var correlation = CorrelationId.New();

        var result = await service.PrepareBusinessOperationAsync(
            adapter,
            "SaveInvoice",
            correlation,
            context);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            HiveBusinessOperationImplementation.ApiAndUi,
            result.Value!.Implementation);
        Assert.Equal(correlation, result.Value.CorrelationId);
        Assert.Equal(0, adapter.BusinessWriteCalls);
    }

    [Fact]
    public async Task Management_RejectsAmbiguousDuplicateBusinessOperationTypes()
    {
        var adapter = new FakeHostAdapter(
            duplicateBusinessOperationTypes: true);
        var service = new HiveHostIntegrationService(new FakeAuthorizer());
        var context = CreateAccessContext();

        var result = await service.PrepareBusinessOperationAsync(
            adapter,
            "SaveInvoice",
            CorrelationId.New(),
            context);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.business-operation-ambiguous",
            result.Error!.Code);
        Assert.Equal(
            ErrorCategory.Conflict,
            result.Error.Category);
        Assert.Equal(0, adapter.BusinessWriteCalls);
    }

    [Fact]
    public async Task Management_CancellationStopsBeforeHostInvocation()
    {
        var adapter = new FakeHostAdapter();
        var service = new HiveHostIntegrationService(new FakeAuthorizer());
        var context = CreateAccessContext();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ExecuteInteractionAsync(
                adapter,
                new HiveHostInteractionRequest(
                    adapter.EditCapabilityId,
                    HiveHostInteractionKind.ReadControl,
                    CorrelationId.New(),
                    controlId: "control:0/0"),
                context,
                cancellation.Token));

        Assert.Equal(0, adapter.InteractionCalls);
    }

    [Fact]
    public async Task Management_RejectsMissingDeploymentOrPrincipalIdentity()
    {
        var adapter = new FakeHostAdapter();
        var service = new HiveHostIntegrationService(new FakeAuthorizer());

        var result = await service.CaptureAsync(
            adapter,
            new ResourceAccessContext());

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.integration.identity-required",
            result.Error!.Code);
    }

    private static ResourceAccessContext CreateAccessContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private sealed class FakeAuthorizer : IHiveHostCapabilityAuthorizer
    {
        private readonly Guid? _deniedCapabilityId;

        public FakeAuthorizer(Guid? deniedCapabilityId = null)
        {
            _deniedCapabilityId = deniedCapabilityId;
        }

        public Result Authorize(
            HiveHostCapabilityRequest request,
            ResourceAccessContext accessContext) =>
            request.CapabilityId == _deniedCapabilityId
                ? Result.Failure(
                    new Error(
                        "hive.tests.host-capability-forbidden",
                        ErrorCategory.Forbidden,
                        "The test authorizer denied the host capability."))
                : Result.Success();
    }

    private sealed class FakeHostAdapter : IHiveHostIntegrationAdapter
    {
        private readonly bool _duplicateBusinessOperationTypes;

        public FakeHostAdapter(
            bool duplicateBusinessOperationTypes = false)
        {
            _duplicateBusinessOperationTypes = duplicateBusinessOperationTypes;
        }

        public Guid EditCapabilityId { get; } = Guid.NewGuid();

        public Guid LookupCapabilityId { get; } = Guid.NewGuid();

        public int InteractionCalls { get; private set; }

        public int BusinessWriteCalls { get; private set; }

        public long LastLookupCategoryId { get; private set; }

        public string AdapterId => "Hive.Tests.ReferenceHost";

        public Task<Result<HiveHostContextDescriptor>> CaptureAsync(
            ResourceAccessContext accessContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lookup = new HiveHostLookupDescriptor(
                "products",
                LookupCapabilityId,
                "Name",
                "Id",
                new[] { "CategoryId" });

            var lineSurface = new HiveHostDataSurfaceDescriptor(
                "invoice-lines",
                "Invoice lines",
                2,
                new[]
                {
                    new HiveHostFieldDescriptor(
                        "Id",
                        "Id",
                        typeof(long).FullName!,
                        true,
                        true,
                        false,
                        false,
                        true),
                    new HiveHostFieldDescriptor(
                        "CategoryId",
                        "CategoryId",
                        typeof(long).FullName!,
                        true,
                        false,
                        false,
                        false,
                        false),
                    new HiveHostFieldDescriptor(
                        "ProductId",
                        "ProductId",
                        typeof(long).FullName!,
                        true,
                        false,
                        false,
                        false,
                        false,
                        lookup: lookup)
                },
                new[]
                {
                    new HiveHostCapabilityDescriptor(
                        EditCapabilityId,
                        HiveHostCapabilityKind.EditRow,
                        "Edit invoice line")
                });

            var correlation = CorrelationId.New();

            var saveInvoiceOperations = _duplicateBusinessOperationTypes
                ? new[]
                {
                    new HiveHostBusinessOperationDescriptor(
                        Guid.NewGuid(),
                        "SaveInvoice",
                        "Save invoice",
                        HiveBusinessOperationImplementation.ApiAndUi),
                    new HiveHostBusinessOperationDescriptor(
                        Guid.NewGuid(),
                        "SaveInvoice",
                        "Duplicate save invoice",
                        HiveBusinessOperationImplementation.Api)
                }
                : new[]
                {
                    new HiveHostBusinessOperationDescriptor(
                        Guid.NewGuid(),
                        "SaveInvoice",
                        "Save invoice",
                        HiveBusinessOperationImplementation.ApiAndUi)
                };

            return Task.FromResult(
                Result<HiveHostContextDescriptor>.Success(
                    new HiveHostContextDescriptor(
                        Guid.NewGuid(),
                        "Reference Host",
                        new HiveHostProvenance(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            DateTimeOffset.UtcNow,
                            correlation,
                            "ReferenceAdapter",
                            accessContext),
                        new[]
                        {
                            new HiveHostControlDescriptor(
                                "control:0/0",
                                "0/0",
                                1,
                                typeof(System.Windows.Forms.TextBox).FullName!,
                                "customer",
                                "Customer",
                                true,
                                true,
                                false,
                                false,
                                new HiveHostFieldDescriptor(
                                    "Customer",
                                    "Customer",
                                    typeof(string).FullName!,
                                    false,
                                    false,
                                    false,
                                    false,
                                    false),
                                new[]
                                {
                                    new HiveHostCapabilityDescriptor(
                                        Guid.NewGuid(),
                                        HiveHostCapabilityKind.ReadControl,
                                        "Read customer")
                                })
                        },
                        new[] { lineSurface },
                        saveInvoiceOperations)));
        }

        public Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
            HiveHostInteractionRequest request,
            ResourceAccessContext accessContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InteractionCalls++;

            return Task.FromResult(
                Result<HiveHostInteractionResult>.Success(
                    new HiveHostInteractionResult(
                        request.CorrelationId,
                        request.Kind)));
        }

        public Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
            HiveLookupRequest request,
            ResourceAccessContext accessContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.CurrentValues.TryGetValue(
                    "CategoryId",
                    out var category))
            {
                category.TryGetInt64(out var categoryId);
                LastLookupCategoryId = categoryId;
            }

            return Task.FromResult(
                Result<IReadOnlyList<HiveLookupOption>>.Success(
                    new[]
                    {
                        new HiveLookupOption(
                            new HiveHostRowIdentity("1"),
                            HiveHostValue.FromInt64(1),
                            "Product A")
                    }));
        }
    }
}
