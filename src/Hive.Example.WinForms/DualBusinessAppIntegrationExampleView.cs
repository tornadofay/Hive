using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Example.WinForms;

internal sealed class DualBusinessAppIntegrationExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;
    private readonly IServiceProvider _services;

    public DualBusinessAppIntegrationExampleView(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(services);

        _output = output;
        _services = services;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run host integration"
        };

        _surface.SetInformation(
            "Demonstrates the Phase 1.14 contract-first host integration boundary with a deterministic WinForms reference host.",
            "The fixture supplies explicit semantic data/operation contracts while Hive provides bounded WinForms control adaptation, Management-owned authorization, lookup plumbing, provenance, and API/UI composition. No SQL or consequential business write is performed.",
            "Host Integration",
            "Host / WinForms Integration / Dual Business-App Integration Contract");

        _surface.CodeSnippet = """
            var host = new Phase14FixtureForm(provider);
            using var adapter =
                new HiveWinFormsHostIntegrationAdapter(
                    host,
                    accessContext,
                    provider);

            var integration =
                new HiveHostIntegrationService(authorizer);

            var context =
                await integration.CaptureAsync(
                    adapter,
                    accessContext,
                    cancellationToken);

            var composition =
                await integration.PrepareBusinessOperationAsync(
                    adapter,
                    "SaveInvoice",
                    CorrelationId.New(),
                    accessContext,
                    cancellationToken);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        themeManager.Apply(this);
    }

    private async Task RunExampleAsync(
        CancellationToken cancellationToken)
    {
        var exampleServices = _services.GetService(
            typeof(HiveExampleServices)) as HiveExampleServices
            ?? throw new InvalidOperationException(
                "Hive Example services are unavailable.");

        var accessContext = exampleServices.AccessContext;
        var provider = new Phase14SemanticProvider();
        using var fixture = new Phase14FixtureForm(provider);
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            fixture,
            accessContext,
            provider);
        var authorizer = new Phase14Authorizer(
            provider.DeniedEditCapabilityId);
        var integration = new HiveHostIntegrationService(authorizer);

        var context = await integration
            .CaptureAsync(
                adapter,
                accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        EnsureSuccess(context, "Host context capture");

        var descriptor = context.Value!;
        var invoiceNumber = descriptor.Controls
            .Single(control => control.Name == "invoiceNumber");

        var setCapability = invoiceNumber.Capabilities
            .Single(capability =>
                capability.Kind == HiveHostCapabilityKind.SetControlValue);

        var setResult = await integration
            .ExecuteInteractionAsync(
                adapter,
                new HiveHostInteractionRequest(
                    setCapability.Id,
                    HiveHostInteractionKind.SetControlValue,
                    CorrelationId.New(),
                    controlId: invoiceNumber.Id,
                    value: HiveHostValue.FromString("INV-1001-EDITED")),
                accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        EnsureSuccess(setResult, "Bounded control edit");

        var linesSurface = descriptor.DataSurfaces
            .Single(surface => surface.Id == "surface:invoiceLines");

        var primaryKey = linesSurface.Fields
            .Single(field => field.Name == "Id");

        var productId = linesSurface.Fields
            .Single(field => field.Name == "ProductId");

        var editCapability = linesSurface.Capabilities
            .Single(capability =>
                capability.Kind == HiveHostCapabilityKind.EditRow);

        var deniedEdit = await integration
            .ExecuteInteractionAsync(
                adapter,
                new HiveHostInteractionRequest(
                    editCapability.Id,
                    HiveHostInteractionKind.EditRow,
                    CorrelationId.New(),
                    surfaceId: linesSurface.Id,
                    rowIdentity: new HiveHostRowIdentity("101"),
                    fieldName: "Quantity",
                    value: HiveHostValue.FromInt64(3)),
                accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (!deniedEdit.IsFailure ||
            deniedEdit.Error!.Category != ErrorCategory.Forbidden)
        {
            throw new InvalidOperationException(
                "The authorization boundary did not deny the host-exposed edit capability.");
        }

        var lookup = productId.Lookup
            ?? throw new InvalidOperationException(
                "The ProductId field did not expose its lookup contract.");

        var lookupResult = await integration
            .ResolveLookupAsync(
                adapter,
                new HiveLookupRequest(
                    lookup.Id,
                    lookup.CapabilityId,
                    new Dictionary<string, HiveHostValue>
                    {
                        ["CategoryId"] = HiveHostValue.FromInt64(10)
                    }),
                accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        EnsureSuccess(lookupResult, "Dependent lookup");
        if (lookupResult.Value!.Count != 2)
        {
            throw new InvalidOperationException(
                "The dependent lookup did not return the expected bounded result set.");
        }

        foreach (var operation in descriptor.BusinessOperations)
        {
            var composition = await integration
                .PrepareBusinessOperationAsync(
                    adapter,
                    operation.OperationType,
                    CorrelationId.New(),
                    accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            EnsureSuccess(
                composition,
                $"Business-operation composition: {operation.OperationType}");

            if (composition.Value!.Stages.Count !=
                operation.Stages.Count)
            {
                throw new InvalidOperationException(
                    $"Business-operation composition for '{operation.OperationType}' lost an implementation stage.");
            }
        }

        var saveInvoice = descriptor.BusinessOperations
            .Single(operation => operation.OperationType == "SaveInvoice");
        var correlation = CorrelationId.New();

        var saveComposition = await integration
            .PrepareBusinessOperationAsync(
                adapter,
                saveInvoice.OperationType,
                correlation,
                accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        EnsureSuccess(saveComposition, "API/UI composition");

        if (saveComposition.Value!.Implementation !=
            HiveBusinessOperationImplementation.ApiAndUi ||
            saveComposition.Value.CorrelationId != correlation)
        {
            throw new InvalidOperationException(
                "The combined API/UI operation did not preserve one logical correlation identity.");
        }

        if (!primaryKey.IsPrimaryKey ||
            !primaryKey.ReadOnly ||
            linesSurface.Children.Count != 0)
        {
            throw new InvalidOperationException(
                "The stable child-row primary-key semantics were not represented correctly.");
        }

        var invoiceSurface = descriptor.DataSurfaces
            .Single(surface => surface.Id == "surface:invoice");

        if (!invoiceSurface.Fields.Any(field => field.Generated) ||
            !invoiceSurface.Fields.Any(field => field.Computed) ||
            invoiceSurface.Children.Count != 1)
        {
            throw new InvalidOperationException(
                "Generated/computed parent fields or the explicit child relationship are missing.");
        }

        var stableRow = await integration
            .ExecuteInteractionAsync(
                adapter,
                new HiveHostInteractionRequest(
                    linesSurface.Capabilities.Single(capability =>
                        capability.Kind == HiveHostCapabilityKind.ReadRow).Id,
                    HiveHostInteractionKind.ReadRow,
                    CorrelationId.New(),
                    surfaceId: linesSurface.Id,
                    rowIdentity: new HiveHostRowIdentity("101")),
                accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        EnsureSuccess(stableRow, "Stable row lookup");

        if (stableRow.Value!.Row!.Identity.Value != "101" ||
            stableRow.Value.Row.Position < 0)
        {
            throw new InvalidOperationException(
                "The stable row identity was not preserved independently from position.");
        }

        _output.Write(
            "Dual Business-App Integration Contract",
            $"Captured {descriptor.Controls.Count} controls, {descriptor.DataSurfaces.Count} semantic data surfaces, {descriptor.BusinessOperations.Count} business-operation paths. " +
            $"Hidden primary key '{primaryKey.Name}' remained stable as row ID {stableRow.Value.Row.Identity.Value}; " +
            $"dependent lookup returned {lookupResult.Value!.Count} bounded options; authorization denied the exposed EditRow capability; " +
            $"SaveInvoice composed API + UI under correlation {correlation.Value:D}. " +
            "No business write was executed.");

        _surface.SetStatus(
            "Passed — contract discovery, bounded UI interaction, lookup, authorization, stable identity, and API/UI composition.");
    }

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error!.Code}: {result.Error.Message}");
        }
    }

    private sealed class Phase14FixtureForm : HiveForm
    {
        public Phase14FixtureForm(
            Phase14SemanticProvider provider)
            : base(
                "Phase 1.14 Fixture",
                "Hive base form + base controls + explicit semantic overrides",
                new Size(900, 520),
                new Size(760, 420))
        {
            ArgumentNullException.ThrowIfNull(provider);

            Name = "phase14Fixture";

            var invoiceNumber = new HiveTextBox
            {
                Name = "invoiceNumber",
                Text = "INV-1001",
                Location = new Point(12, 12),
                Width = 260
            };

            var invoiceGrid = new HiveDataGridView
            {
                Name = "invoiceGrid",
                Location = new Point(12, 48),
                Size = new Size(856, 160),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };

            invoiceGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id",
                Visible = false
            });
            invoiceGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "InvoiceNumber",
                DataPropertyName = "InvoiceNumber",
                HeaderText = "Invoice number"
            });
            invoiceGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                DataPropertyName = "Customer"
            });
            invoiceGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Total",
                DataPropertyName = "Total"
            });
            invoiceGrid.DataSource = provider.Invoices;

            var linesGrid = new HiveDataGridView
            {
                Name = "invoiceLinesGrid",
                Location = new Point(12, 224),
                Size = new Size(856, 240),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };

            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id",
                Visible = false
            });
            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "InvoiceId",
                DataPropertyName = "InvoiceId",
                Visible = false
            });
            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CategoryId",
                DataPropertyName = "CategoryId",
                HeaderText = "Category"
            });
            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                HeaderText = "Product"
            });
            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Quantity",
                DataPropertyName = "Quantity"
            });
            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Price",
                DataPropertyName = "Price"
            });
            linesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LineTotal",
                DataPropertyName = "LineTotal",
                HeaderText = "Line total",
                ReadOnly = true
            });
            linesGrid.DataSource = provider.Lines;

            invoiceGrid.HiveDataSurface.SurfaceId = "invoice";
            invoiceGrid.HiveDataSurface.Name = "Invoices";
            invoiceGrid.HiveDataSurface.PrimaryKeyField = "Id";
            invoiceGrid.HiveDataSurface.ConfigureField("InvoiceNumber").Generated = true;
            invoiceGrid.HiveDataSurface.ConfigureField("Total").Computed = true;

            foreach (var capability in new[]
            {
                new HiveHostCapabilityDescriptor(
                    provider.InvoiceReadCapabilityId,
                    HiveHostCapabilityKind.ReadDataSurface,
                    "Read invoices"),
                new HiveHostCapabilityDescriptor(
                    provider.InvoiceEditCapabilityId,
                    HiveHostCapabilityKind.EditRow,
                    "Edit invoice"),
                new HiveHostCapabilityDescriptor(
                    provider.PreviewCapabilityId,
                    HiveHostCapabilityKind.InvokeAction,
                    "Preview invoice",
                    action: HiveHostActionKind.Preview)
            })
            {
                invoiceGrid.HiveDataSurface.AddCapability(capability);
            }

            linesGrid.HiveDataSurface.SurfaceId = "invoiceLines";
            linesGrid.HiveDataSurface.Name = "Invoice lines";
            linesGrid.HiveDataSurface.PrimaryKeyField = "Id";
            linesGrid.HiveDataSurface.ParentSurfaceId = "surface:invoice";
            linesGrid.HiveDataSurface.ParentKeyField = "Id";
            linesGrid.HiveDataSurface.ChildKeyField = "InvoiceId";
            linesGrid.HiveDataSurface.ConfigureField("Id").IsPrimaryKey = true;
            linesGrid.HiveDataSurface.ConfigureField("ProductId").Lookup =
                new HiveHostLookupDescriptor(
                    "lookup:products",
                    provider.LookupCapabilityId,
                    "Name",
                    "Id",
                    new[] { "CategoryId" });

            foreach (var capability in new[]
            {
                new HiveHostCapabilityDescriptor(
                    provider.LineReadCapabilityId,
                    HiveHostCapabilityKind.ReadDataSurface,
                    "Read invoice lines"),
                new HiveHostCapabilityDescriptor(
                    provider.LineReadRowCapabilityId,
                    HiveHostCapabilityKind.ReadRow,
                    "Read invoice line"),
                new HiveHostCapabilityDescriptor(
                    provider.LineAddCapabilityId,
                    HiveHostCapabilityKind.AddRow,
                    "Add invoice line"),
                new HiveHostCapabilityDescriptor(
                    provider.LineEditCapabilityId,
                    HiveHostCapabilityKind.EditRow,
                    "Edit invoice line"),
                new HiveHostCapabilityDescriptor(
                    provider.LineDeleteCapabilityId,
                    HiveHostCapabilityKind.DeleteRow,
                    "Delete invoice line")
            })
            {
                linesGrid.HiveDataSurface.AddCapability(capability);
            }

            BodyPanel.Controls.Add(invoiceNumber);
            BodyPanel.Controls.Add(invoiceGrid);
            BodyPanel.Controls.Add(linesGrid);
            ThemeManager.Apply(BodyPanel);
        }
    }

    private sealed class Phase14Invoice
    {
        public long Id { get; init; } = 1001;

        public string InvoiceNumber { get; set; } = "INV-1001";

        public string Customer { get; set; } = "Example Customer";

        public decimal Total =>
            2m * 12.50m + 1m * 24.00m;
    }

    private sealed class Phase14Line
    {
        public long Id { get; init; }

        public long InvoiceId { get; init; }

        public long CategoryId { get; set; }

        public long ProductId { get; set; }

        public long Quantity { get; set; }

        public decimal Price { get; set; }

        public decimal LineTotal => Quantity * Price;
    }

    private sealed class Phase14SemanticProvider :
        IHiveWinFormsSemanticProvider
    {
        private readonly Guid _invoiceReadCapability = Guid.NewGuid();
        private readonly Guid _invoiceEditCapability = Guid.NewGuid();
        private readonly Guid _lineReadCapability = Guid.NewGuid();
        private readonly Guid _lineReadRowCapability = Guid.NewGuid();
        private readonly Guid _lineAddCapability = Guid.NewGuid();
        private readonly Guid _lineEditCapability = Guid.NewGuid();
        private readonly Guid _lineDeleteCapability = Guid.NewGuid();
        private readonly Guid _previewCapability = Guid.NewGuid();
        private readonly Guid _lookupCapability = Guid.NewGuid();
        private readonly Guid _apiCapability = Guid.NewGuid();
        private readonly Guid _uiCapability = Guid.NewGuid();
        private readonly Guid _apiUiCapability = Guid.NewGuid();

        private long _nextLineId = 103;
        private long _hostVersion;

        public Phase14SemanticProvider()
        {
            Invoices = new BindingList<Phase14Invoice>
            {
                new()
            };

            Lines = new BindingList<Phase14Line>
            {
                new()
                {
                    Id = 101,
                    InvoiceId = 1001,
                    CategoryId = 10,
                    ProductId = 1,
                    Quantity = 2,
                    Price = 12.50m
                },
                new()
                {
                    Id = 102,
                    InvoiceId = 1001,
                    CategoryId = 20,
                    ProductId = 2,
                    Quantity = 1,
                    Price = 24.00m
                }
            };

            DeniedEditCapabilityId = _lineEditCapability;
        }

        public BindingList<Phase14Invoice> Invoices { get; }

        public BindingList<Phase14Line> Lines { get; }

        public Guid DeniedEditCapabilityId { get; }

        public Guid InvoiceReadCapabilityId => _invoiceReadCapability;

        public Guid InvoiceEditCapabilityId => _invoiceEditCapability;

        public Guid LineReadCapabilityId => _lineReadCapability;

        public Guid LineReadRowCapabilityId => _lineReadRowCapability;

        public Guid LineAddCapabilityId => _lineAddCapability;

        public Guid LineEditCapabilityId => _lineEditCapability;

        public Guid LineDeleteCapabilityId => _lineDeleteCapability;

        public Guid PreviewCapabilityId => _previewCapability;

        public Guid LookupCapabilityId => _lookupCapability;

        public bool TryDescribeDataSurface(
            DataGridView grid,
            string surfaceId,
            out HiveHostDataSurfaceDescriptor descriptor)
        {
            descriptor = null!;
            return false;
        }

        public IReadOnlyList<HiveHostBusinessOperationDescriptor>
            GetBusinessOperations() =>
            new[]
            {
                new HiveHostBusinessOperationDescriptor(
                    _apiCapability,
                    "CreateInvoice",
                    "Create invoice",
                    HiveBusinessOperationImplementation.Api),
                new HiveHostBusinessOperationDescriptor(
                    _uiCapability,
                    "OpenInvoice",
                    "Open invoice",
                    HiveBusinessOperationImplementation.Ui),
                new HiveHostBusinessOperationDescriptor(
                    _apiUiCapability,
                    "SaveInvoice",
                    "Save invoice",
                    HiveBusinessOperationImplementation.ApiAndUi)
            };

        public async Task<Result<HiveHostInteractionResult>>
            ExecuteInteractionAsync(
                HiveHostInteractionRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return request.Kind switch
            {
                HiveHostInteractionKind.ReadRow =>
                    ReadRow(request),

                HiveHostInteractionKind.AddRow =>
                    AddRow(request),

                HiveHostInteractionKind.EditRow =>
                    EditRow(request),

                HiveHostInteractionKind.DeleteRow =>
                    DeleteRow(request),

                HiveHostInteractionKind.InvokeAction =>
                    InvokeAction(request),

                _ =>
                    Result<HiveHostInteractionResult>.Failure(
                        Error.Unsupported(
                            "hive.example.phase14.interaction-unsupported",
                            "The Phase 1.14 fixture does not implement this interaction."))
            };
        }

        public Task<Result<IReadOnlyList<HiveLookupOption>>>
            ResolveLookupAsync(
                HiveLookupRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.LookupId != "lookup:products")
            {
                return Task.FromResult(
                    Result<IReadOnlyList<HiveLookupOption>>.Failure(
                        new Error(
                            "hive.example.phase14.lookup-not-found",
                            ErrorCategory.NotFound,
                            "The requested fixture lookup is not available.")));
            }

            var category = request.CurrentValues.TryGetValue(
                "CategoryId",
                out var categoryValue) &&
                categoryValue.TryGetInt64(out var categoryId)
                    ? categoryId
                    : 0;

            var options = category switch
            {
                10 =>
                    new[]
                    {
                        new HiveLookupOption(
                            new HiveHostRowIdentity("1"),
                            HiveHostValue.FromInt64(1),
                            "Product A"),
                        new HiveLookupOption(
                            new HiveHostRowIdentity("3"),
                            HiveHostValue.FromInt64(3),
                            "Product C")
                    },
                20 =>
                    new[]
                    {
                        new HiveLookupOption(
                            new HiveHostRowIdentity("2"),
                            HiveHostValue.FromInt64(2),
                            "Product B"),
                        new HiveLookupOption(
                            new HiveHostRowIdentity("4"),
                            HiveHostValue.FromInt64(4),
                            "Product D")
                    },
                _ =>
                    new[]
                    {
                        new HiveLookupOption(
                            new HiveHostRowIdentity("1"),
                            HiveHostValue.FromInt64(1),
                            "Product A"),
                        new HiveLookupOption(
                            new HiveHostRowIdentity("2"),
                            HiveHostValue.FromInt64(2),
                            "Product B")
                    }
            };

            return Task.FromResult(
                Result<IReadOnlyList<HiveLookupOption>>.Success(options));
        }

        private Result<HiveHostInteractionResult> ReadRow(
            HiveHostInteractionRequest request)
        {
            if (request.SurfaceId != "surface:invoiceLines" ||
                request.RowIdentity is not { } identity)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.example.phase14.row-required",
                        "A stable invoice-line row identity is required."));
            }

            var line = Lines.SingleOrDefault(item =>
                item.Id.ToString() == identity.Value);

            return line is null
                ? Result<HiveHostInteractionResult>.Failure(
                    new Error(
                        "hive.example.phase14.row-not-found",
                        ErrorCategory.NotFound,
                        "The requested invoice-line row was not found."))
                : Result<HiveHostInteractionResult>.Success(
                    new HiveHostInteractionResult(
                        request.CorrelationId,
                        request.Kind,
                        row: ToRow(line),
                        hostVersion: _hostVersion.ToString()));
        }

        private Result<HiveHostInteractionResult> AddRow(
            HiveHostInteractionRequest request)
        {
            if (request.SurfaceId != "surface:invoiceLines")
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.example.phase14.surface-invalid",
                        "Rows may only be added to invoice lines in this fixture."));
            }

            var line = new Phase14Line
            {
                Id = _nextLineId++,
                InvoiceId = 1001,
                CategoryId = 10,
                ProductId = 1,
                Quantity = 1,
                Price = 0
            };

            Lines.Add(line);
            _hostVersion++;

            return Result<HiveHostInteractionResult>.Success(
                new HiveHostInteractionResult(
                    request.CorrelationId,
                    request.Kind,
                    row: ToRow(line),
                    resultingRowIdentity: new HiveHostRowIdentity(
                        line.Id.ToString()),
                    hostVersion: _hostVersion.ToString()));
        }

        private Result<HiveHostInteractionResult> EditRow(
            HiveHostInteractionRequest request)
        {
            if (request.SurfaceId != "surface:invoiceLines" ||
                request.RowIdentity is not { } identity ||
                request.FieldName is null ||
                request.Value is not { } value)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.example.phase14.edit-invalid",
                        "A stable row identity, field name, and value are required."));
            }

            if (request.FieldName is "Id" or "InvoiceId" or "LineTotal")
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.example.phase14.field-read-only",
                        "Generated identity, parent identity, and computed fields are not directly editable."));
            }

            var line = Lines.SingleOrDefault(item =>
                item.Id.ToString() == identity.Value);

            if (line is null)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    new Error(
                        "hive.example.phase14.row-not-found",
                        ErrorCategory.NotFound,
                        "The requested invoice-line row was not found."));
            }

            switch (request.FieldName)
            {
                case "CategoryId" when value.TryGetInt64(out var category):
                    line.CategoryId = category;
                    break;
                case "ProductId" when value.TryGetInt64(out var product):
                    line.ProductId = product;
                    break;
                case "Quantity" when value.TryGetInt64(out var quantity) &&
                                   quantity > 0:
                    line.Quantity = quantity;
                    break;
                case "Price" when value.TryGetDecimal(out var price) &&
                                 price >= 0:
                    line.Price = price;
                    break;
                default:
                    return Result<HiveHostInteractionResult>.Failure(
                        Error.Validation(
                            "hive.example.phase14.value-invalid",
                            "The requested fixture field value is invalid."));
            }

            _hostVersion++;

            return Result<HiveHostInteractionResult>.Success(
                new HiveHostInteractionResult(
                    request.CorrelationId,
                    request.Kind,
                    row: ToRow(line),
                    hostVersion: _hostVersion.ToString()));
        }

        private Result<HiveHostInteractionResult> DeleteRow(
            HiveHostInteractionRequest request)
        {
            if (request.SurfaceId != "surface:invoiceLines" ||
                request.RowIdentity is not { } identity)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.example.phase14.delete-invalid",
                        "A stable invoice-line row identity is required."));
            }

            var line = Lines.SingleOrDefault(item =>
                item.Id.ToString() == identity.Value);

            if (line is null)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    new Error(
                        "hive.example.phase14.row-not-found",
                        ErrorCategory.NotFound,
                        "The requested invoice-line row was not found."));
            }

            Lines.Remove(line);
            _hostVersion++;

            return Result<HiveHostInteractionResult>.Success(
                new HiveHostInteractionResult(
                    request.CorrelationId,
                    request.Kind,
                    resultingRowIdentity: identity,
                    hostVersion: _hostVersion.ToString()));
        }

        private static Result<HiveHostInteractionResult> InvokeAction(
            HiveHostInteractionRequest request)
        {
            if (request.Action is HiveHostActionKind.Save)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Unsupported(
                        "hive.example.phase14.business-write-reserved",
                        "The consequential Save business operation remains outside Phase 1.14."));
            }

            return Result<HiveHostInteractionResult>.Success(
                new HiveHostInteractionResult(
                    request.CorrelationId,
                    request.Kind,
                    resultValue: request.Action is { } action
                        ? HiveHostValue.FromString(action.ToString())
                        : null));
        }

        private HiveHostRow ToRow(Phase14Line line)
        {
            var position = Lines.IndexOf(line);

            return new HiveHostRow(
                new HiveHostRowIdentity(line.Id.ToString()),
                position,
                new Dictionary<string, HiveHostValue>
                {
                    ["Id"] = HiveHostValue.FromInt64(line.Id),
                    ["InvoiceId"] = HiveHostValue.FromInt64(line.InvoiceId),
                    ["CategoryId"] = HiveHostValue.FromInt64(line.CategoryId),
                    ["ProductId"] = HiveHostValue.FromInt64(line.ProductId),
                    ["Quantity"] = HiveHostValue.FromInt64(line.Quantity),
                    ["Price"] = HiveHostValue.FromDecimal(line.Price),
                    ["LineTotal"] = HiveHostValue.FromDecimal(line.LineTotal)
                });
        }
    }

    private sealed class Phase14Authorizer :
        IHiveHostCapabilityAuthorizer
    {
        private readonly Guid _deniedCapabilityId;

        public Phase14Authorizer(Guid deniedCapabilityId)
        {
            if (deniedCapabilityId == Guid.Empty)
                throw new ArgumentException(
                    "A denied capability identity is required.",
                    nameof(deniedCapabilityId));

            _deniedCapabilityId = deniedCapabilityId;
        }

        public Result Authorize(
            HiveHostCapabilityRequest request,
            ResourceAccessContext accessContext)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(accessContext);

            if (accessContext.DeploymentId is null ||
                accessContext.PrincipalId is null)
            {
                return Result.Failure(
                    Error.Validation(
                        "hive.example.phase14.identity-required",
                        "Deployment and principal identity are required."));
            }

            return request.CapabilityId == _deniedCapabilityId
                ? Result.Failure(
                    new Error(
                        "hive.example.phase14.capability-forbidden",
                        ErrorCategory.Forbidden,
                        "The Hive authorization boundary denied this host capability."))
                : Result.Success();
        }
    }
}
