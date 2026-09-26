using System.Drawing;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Management;
using Hive.Host.WinForms.UI.Controls;
using Xunit;

namespace Hive.Tests;

public sealed class HiveWinFormsHostIntegrationTests
{
    [Fact]
    public async Task Capture_AdaptsStandardWinFormsControlsAndBoundDataSurface()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await adapter.CaptureAsync(accessContext);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var descriptor = result.Value!;

        var text = descriptor.Controls
            .Single(control => control.Name == "customer");
        Assert.NotNull(text.Field);
        Assert.Equal(
            typeof(string).FullName,
            text.Field!.ValueType);
        Assert.Contains(
            text.Capabilities,
            capability =>
                capability.Kind == HiveHostCapabilityKind.ReadControl);
        Assert.Contains(
            text.Capabilities,
            capability =>
                capability.Kind == HiveHostCapabilityKind.SetControlValue);

        var grid = descriptor.DataSurfaces.Single();
        Assert.Equal("orders", grid.Name);
        Assert.Equal(2, grid.RowCount);
        Assert.Equal(
            "Id",
            grid.Fields[0].Name);
        Assert.DoesNotContain(
            grid.Fields,
            field => field.IsPrimaryKey);
        Assert.DoesNotContain(
            descriptor.Controls,
            control => control.Field?.CurrentValue?.AsString() == "secret-value");
    }

    [Fact]
    public async Task Capture_UsesDeterministicNamesAndBaseControlMetadata()
    {
        using var form = CreateBaseFixtureForm();
        var accessContext = CreateAccessContext();

        using var first = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var firstResult = await first.CaptureAsync(accessContext);

        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);

        var firstControl = firstResult.Value!.Controls
            .Single(control => control.Name == "customer");

        Assert.Equal(
            "control:customer",
            firstControl.Id);

        var firstSetCapability = firstControl.Capabilities.Single(capability =>
            capability.Kind == HiveHostCapabilityKind.SetControlValue);

        first.Dispose();

        using var second = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var secondResult = await second.CaptureAsync(accessContext);

        Assert.True(secondResult.IsSuccess, secondResult.Error?.Message);

        var secondControl = secondResult.Value!.Controls
            .Single(control => control.Name == "customer");

        var secondSetCapability = secondControl.Capabilities.Single(capability =>
            capability.Kind == HiveHostCapabilityKind.SetControlValue);

        Assert.Equal(
            firstSetCapability.Id,
            secondSetCapability.Id);
    }

    [Fact]
    public async Task Capture_BaseDataSurfaceAppliesExplicitOverridesAndParentChildRelationship()
    {
        using var form = CreateBaseFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await adapter.CaptureAsync(accessContext);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var descriptor = result.Value!;
        var invoice = descriptor.DataSurfaces.Single(surface =>
            surface.Id == "surface:invoice");
        var lines = descriptor.DataSurfaces.Single(surface =>
            surface.Id == "surface:invoiceLines");

        var invoicePrimaryKey = invoice.Fields.Single(field =>
            field.Name == "Id");
        Assert.True(invoicePrimaryKey.IsPrimaryKey);
        Assert.True(invoicePrimaryKey.ReadOnly);
        Assert.True(invoice.Fields.Single(field =>
            field.Name == "InvoiceNumber").Generated);
        Assert.True(invoice.Fields.Single(field =>
            field.Name == "Total").Computed);

        var product = lines.Fields.Single(field =>
            field.Name == "ProductId");

        Assert.NotNull(product.Lookup);
        Assert.Single(invoice.Children);
        Assert.Equal(
            "surface:invoiceLines",
            invoice.Children[0].ChildSurfaceId);
        Assert.Equal(
            "InvoiceId",
            invoice.Children[0].ChildKeyField);
        Assert.Contains(
            lines.Capabilities,
            capability => capability.Kind == HiveHostCapabilityKind.EditRow);
    }

    [Fact]
    public async Task Capture_DuplicateExplicitControlIdentityFailsInsteadOfAliasing()
    {
        using var form = new Form();
        var first = new HiveTextBox { Name = "first" };
        var second = new HiveTextBox { Name = "second" };
        first.HiveIntegration.ControlId = "shared";
        second.HiveIntegration.ControlId = "shared";
        form.Controls.Add(first);
        form.Controls.Add(second);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await adapter.CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.control-identity-duplicate",
            result.Error!.Code);
    }

    [Fact]
    public async Task Capture_RejectsMissingExplicitPrimaryKeyField()
    {
        using var form = new Form();
        var grid = new HiveDataGridView
        {
            Name = "orders",
            AutoGenerateColumns = false
        };
        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "OrderNumber",
                DataPropertyName = "OrderNumber"
            });
        grid.HiveDataSurface.PrimaryKeyField = "Id";
        form.Controls.Add(grid);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await adapter.CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.primary-key-field-not-found",
            result.Error!.Code);
    }

    [Fact]
    public async Task Capture_ComputedBaseFieldDoesNotExposeWriteCapability()
    {
        using var form = new Form();
        var total = new HiveTextBox
        {
            Name = "total",
            Text = "100"
        };
        total.HiveField.Computed = true;
        form.Controls.Add(total);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await adapter.CaptureAsync(accessContext);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var descriptor = result.Value!.Controls
            .Single(control => control.Name == "total");
        Assert.DoesNotContain(
            descriptor.Capabilities,
            capability => capability.Kind == HiveHostCapabilityKind.SetControlValue);
    }

    [Fact]
    public async Task Dispose_BaseHostDoesNotDisposeHostForm()
    {
        var form = new TestHiveForm();
        var accessContext = CreateAccessContext();
        var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        adapter.Dispose();

        Assert.False(form.IsDisposed);
        form.Dispose();
    }

    [Fact]
    public async Task ExplicitPathLikeControlIdentityRemainsResolvable()
    {
        using var form = new Form();
        var control = new HiveTextBox
        {
            Name = "customer",
            Text = "Example"
        };
        control.HiveIntegration.ControlId = "0/0";
        form.Controls.Add(control);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await adapter.CaptureAsync(accessContext)).Value!;
        var captured = descriptor.Controls
            .Single(control => control.Name == "customer");

        var readCapability = captured.Capabilities.Single(capability =>
            capability.Kind == HiveHostCapabilityKind.ReadControl);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                readCapability.Id,
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: captured.Id),
            accessContext);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            "Example",
            result.Value!.ResultValue!.Value.AsString());
    }

    [Fact]
    public async Task Management_DeniesDefaultBaseControlCapabilityBeforeInteraction()
    {
        using var form = CreateBaseFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await adapter.CaptureAsync(accessContext)).Value!;
        var customer = descriptor.Controls.Single(control =>
            control.Name == "customer");
        var capability = customer.Capabilities.Single(item =>
            item.Kind == HiveHostCapabilityKind.SetControlValue);

        var service = new HiveHostIntegrationService(
            new DenyCapabilityAuthorizer(capability.Id));

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                capability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: customer.Id,
                value: HiveHostValue.FromString("blocked")),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorCategory.Forbidden,
            result.Error!.Category);
        Assert.Equal(
            "Example",
            ((HiveTextBox)FindControl(form, "customer")).Text);
    }

    [Fact]
    public async Task ExecuteInteraction_SetsAndReadsStandardTextControl()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await adapter.CaptureAsync(accessContext)).Value!;
        var control = descriptor.Controls
            .Single(item => item.Name == "customer");
        var capability = control.Capabilities.Single(item =>
            item.Kind == HiveHostCapabilityKind.SetControlValue);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var write = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                capability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: control.Id,
                value: HiveHostValue.FromString("Contoso")),
            accessContext);

        Assert.True(write.IsSuccess, write.Error?.Message);
        Assert.Equal(
            "Contoso",
            write.Value!.ResultValue!.Value.AsString());

        var readCapability = control.Capabilities.Single(item =>
            item.Kind == HiveHostCapabilityKind.ReadControl);

        var read = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                readCapability.Id,
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: control.Id),
            accessContext);

        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(
            "Contoso",
            read.Value!.ResultValue!.Value.AsString());
    }

    [Fact]
    public async Task ExecuteInteraction_RejectsDateOutsideHostRangeWithoutThrowing()
    {
        using var form = new Form();
        var picker = new DateTimePicker
        {
            Name = "date",
            MinDate = new DateTime(2026, 1, 1),
            MaxDate = new DateTime(2026, 12, 31),
            Value = new DateTime(2026, 6, 1)
        };
        form.Controls.Add(picker);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await adapter.CaptureAsync(accessContext)).Value!;
        var control = descriptor.Controls.Single(item => item.Name == "date");
        var capability = control.Capabilities.Single(item =>
            item.Kind == HiveHostCapabilityKind.SetControlValue);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                capability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: control.Id,
                value: HiveHostValue.FromDateTime(new DateTime(2027, 1, 1))),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.datetime-range-invalid",
            result.Error!.Code);
        Assert.Equal(
            new DateTime(2026, 6, 1),
            picker.Value);
    }

    [Fact]
    public async Task PasswordControl_IsNeverExposedAsValue()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await adapter.CaptureAsync(accessContext)).Value!;
        var password = descriptor.Controls
            .Single(item => item.Name == "password");

        Assert.Null(password.Field!.CurrentValue);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var read = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                password.Capabilities.Single(capability =>
                    capability.Kind == HiveHostCapabilityKind.ReadControl).Id,
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: password.Id),
            accessContext);

        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Null(read.Value!.ResultValue);
    }

    [Fact]
    public async Task AccessContextMismatch_IsRejected()
    {
        using var form = CreateFixtureForm();
        var registeredContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            registeredContext);

        var result = await adapter.CaptureAsync(CreateAccessContext());

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorCategory.Forbidden,
            result.Error!.Category);
        Assert.Equal(
            "hive.host.winforms.access-context-mismatch",
            result.Error.Code);
    }

    [Fact]
    public async Task DisposedHost_IsRejectedWithoutExposingControls()
    {
        var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        adapter.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => adapter.CaptureAsync(accessContext));

        form.Dispose();
    }

    [Fact]
    public void Dispose_IsIdempotentAndDoesNotDisposeHostRoot()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        adapter.Dispose();
        adapter.Dispose();

        Assert.False(form.IsDisposed);
    }

    [Fact]
    public async Task ExecuteInteraction_FromBackgroundThread_IsRejectedBeforeHostTraversal()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);
        _ = form.Handle;

        for (var index = 0; index < 600; index++)
        {
            form.Controls.Add(new Panel
            {
                Name = $"padding{index}"
            });
        }

        var result = await Task.Run(() =>
            adapter.ExecuteInteractionAsync(
                new HiveHostInteractionRequest(
                    Guid.NewGuid(),
                    HiveHostInteractionKind.ReadControl,
                    CorrelationId.New(),
                    controlId: "control:missing"),
                accessContext));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.ui-thread-required",
            result.Error!.Code);
    }

    [Fact]
    public async Task Cancellation_IsCheckedBeforeCapture()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => adapter.CaptureAsync(
                accessContext,
                cancellation.Token));
    }

    private static Form CreateBaseFixtureForm()
    {
        var form = new Form
        {
            Name = "baseFixture",
            Text = "Base Fixture"
        };

        var customer = new HiveTextBox
        {
            Name = "customer",
            Text = "Example"
        };

        var invoiceGrid = new HiveDataGridView
        {
            Name = "invoiceGrid",
            AutoGenerateColumns = false
        };

        invoiceGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id",
                Visible = false
            });
        invoiceGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "InvoiceNumber",
                DataPropertyName = "InvoiceNumber"
            });
        invoiceGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Total",
                DataPropertyName = "Total"
            });

        var lineGrid = new HiveDataGridView
        {
            Name = "invoiceLinesGrid",
            AutoGenerateColumns = false
        };

        lineGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id",
                Visible = false
            });
        lineGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "InvoiceId",
                DataPropertyName = "InvoiceId",
                Visible = false
            });
        lineGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId"
            });

        lineGrid.HiveDataSurface.SurfaceId = "invoiceLines";
        lineGrid.HiveDataSurface.ParentSurfaceId = "surface:invoice";
        lineGrid.HiveDataSurface.ParentKeyField = "Id";
        lineGrid.HiveDataSurface.ChildKeyField = "InvoiceId";
        lineGrid.HiveDataSurface.PrimaryKeyField = "Id";
        lineGrid.HiveDataSurface.ConfigureField("ProductId").Lookup =
            new HiveHostLookupDescriptor(
                "lookup:products",
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                "Name",
                "Id",
                new[] { "CategoryId" });
        lineGrid.HiveDataSurface.AddCapability(
            new HiveHostCapabilityDescriptor(
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                HiveHostCapabilityKind.EditRow,
                "Edit invoice line"));

        var invoiceSurface = invoiceGrid.HiveDataSurface;
        invoiceSurface.SurfaceId = "invoice";
        invoiceSurface.PrimaryKeyField = "Id";
        invoiceSurface.ConfigureField("InvoiceNumber").Generated = true;
        invoiceSurface.ConfigureField("Total").Computed = true;

        form.Controls.Add(customer);
        form.Controls.Add(invoiceGrid);
        form.Controls.Add(lineGrid);

        return form;
    }

    private static Control FindControl(Control root, string name)
    {
        if (root.Name == name)
            return root;

        foreach (Control child in root.Controls)
        {
            var result = FindControl(child, name);
            if (result is not null)
                return result;
        }

        throw new InvalidOperationException(
            $"Control '{name}' was not found in the test fixture.");
    }

    private static Form CreateFixtureForm()
    {
        var form = new Form
        {
            Name = "integrationFixture",
            Text = "Integration Fixture"
        };

        var customer = new TextBox
        {
            Name = "customer",
            Text = "Example"
        };

        var password = new TextBox
        {
            Name = "password",
            Text = "secret-value",
            UseSystemPasswordChar = true
        };

        var binding = new BindingSource
        {
            DataSource = new[]
            {
                new { Id = 1, Name = "One" },
                new { Id = 2, Name = "Two" }
            }
        };

        var grid = new DataGridView
        {
            Name = "orders",
            DataSource = binding,
            Location = new Point(0, 80)
        };

        form.Controls.Add(customer);
        form.Controls.Add(password);
        form.Controls.Add(grid);

        return form;
    }

    private static ResourceAccessContext CreateAccessContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private sealed class TestHiveForm :
        HiveForm
    {
        public TestHiveForm()
            : base("Test", "Test")
        {
        }
    }

    private sealed class DenyCapabilityAuthorizer :
        IHiveHostCapabilityAuthorizer
    {
        private readonly Guid _deniedCapabilityId;

        public DenyCapabilityAuthorizer(Guid deniedCapabilityId)
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
                        "The test authorizer denied the default base-control capability."))
                : Result.Success();
    }

    private sealed class AllowAllAuthorizer :
        IHiveHostCapabilityAuthorizer
    {
        public Result Authorize(
            HiveHostCapabilityRequest request,
            ResourceAccessContext accessContext) =>
            Result.Success();
    }
}
