using System.Drawing;
using System.Reflection;
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

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

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

        var firstResult = await AsIntegrationAdapter(first).CaptureAsync(accessContext);

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

        var secondResult = await AsIntegrationAdapter(second).CaptureAsync(accessContext);

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

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

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
    public async Task Capture_AppliesFieldOverrideWhenConfiguredWithDifferentCase()
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
                Name = "CustomerId",
                DataPropertyName = "CustomerId"
            });
        grid.HiveDataSurface.ConfigureField("customerid").Required = true;

        form.Controls.Add(grid);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var field = result.Value!.DataSurfaces
            .Single(surface => surface.Id == "surface:orders")
            .Fields
            .Single(candidate => candidate.BindingMember == "CustomerId");

        Assert.True(field.Required);
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

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.control-identity-duplicate",
            result.Error!.Code);
    }

    [Fact]
    public async Task Capture_DuplicateExplicitSurfaceIdentityFailsInsteadOfAliasing()
    {
        using var form = new Form();
        var first = new HiveDataGridView
        {
            Name = "orders",
            AutoGenerateColumns = false
        };
        var second = new HiveDataGridView
        {
            Name = "orders2",
            AutoGenerateColumns = false
        };

        first.HiveDataSurface.SurfaceId = "shared";
        second.HiveDataSurface.SurfaceId = "shared";
        form.Controls.Add(first);
        form.Controls.Add(second);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.surface-identity-duplicate",
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

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.primary-key-field-not-found",
            result.Error!.Code);
    }

    [Fact]
    public async Task Capture_BindingInspectionFailureIsReportedInsteadOfReturningIncompleteSurface()
    {
        using var form = new Form();
        var binding = new BindingSource
        {
            DataSource = new[]
            {
                new { Id = 1, Name = "One" }
            }
        };

        var grid = new DataGridView
        {
            Name = "orders",
            DataSource = binding,
            DataMember = "missing"
        };
        form.Controls.Add(grid);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.binding-inspection-failed",
            result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
    }

    [Fact]
    public async Task Capture_BoundRowCountInspectionFailureIsReportedAsTypedFailure()
    {
        using var form = new Form();
        var binding = new BindingSource
        {
            DataSource = new[]
            {
                new { Id = 1, Name = "One" }
            }
        };

        var grid = new DataGridView
        {
            Name = "orders",
            DataSource = binding,
            DataMember = "missing"
        };
        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id"
            });
        form.Controls.Add(grid);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.binding-row-count-failed",
            result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
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

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

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

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
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
                controlId: captured.Id,
                captureId: descriptor.Provenance.CaptureId),
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

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var captureId = descriptor.Provenance.CaptureId;
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
                value: HiveHostValue.FromString("blocked"),
                captureId: captureId),
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

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var captureId = descriptor.Provenance.CaptureId;
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
                value: HiveHostValue.FromString("Contoso"),
                captureId: captureId),
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
                controlId: control.Id,
                captureId: descriptor.Provenance.CaptureId),
            accessContext);

        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(
            "Contoso",
            read.Value!.ResultValue!.Value.AsString());
    }

    [Fact]
    public async Task ExecuteInteraction_ReadControlRejectsMismatchedCapability()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var control = descriptor.Controls
            .Single(item => item.Name == "customer");
        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                Guid.NewGuid(),
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: control.Id,
                captureId: descriptor.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.capability-mismatch",
            result.Error!.Code);
    }

    [Fact]
    public async Task ReadControl_RejectsStaleCaptureWithoutReadingReplacement()
    {
        using var form = new Form();

        var original = new HiveTextBox
        {
            Name = "customer",
            Text = "Original"
        };
        form.Controls.Add(original);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var first = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var oldControl = first.Controls.Single(item => item.Name == "customer");
        var oldReadCapability = oldControl.Capabilities.Single(
            capability => capability.Kind == HiveHostCapabilityKind.ReadControl);

        form.Controls.Remove(original);
        original.Dispose();

        var replacement = new HiveTextBox
        {
            Name = "customer",
            Text = "Replacement"
        };
        form.Controls.Add(replacement);

        var second = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                oldReadCapability.Id,
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: oldControl.Id,
                captureId: first.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.capture-stale",
            result.Error!.Code);
        Assert.NotEqual(
            first.Provenance.CaptureId,
            second.Provenance.CaptureId);
        Assert.Equal("Replacement", replacement.Text);
    }

    [Fact]
    public async Task ReadControl_RejectsReplacedControlWithoutRecapture()
    {
        using var form = new Form();

        var original = new HiveTextBox
        {
            Name = "customer",
            Text = "Original"
        };
        form.Controls.Add(original);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var captured = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var control = captured.Controls.Single(item => item.Name == "customer");
        var readCapability = control.Capabilities.Single(
            capability => capability.Kind == HiveHostCapabilityKind.ReadControl);

        form.Controls.Remove(original);
        original.Dispose();

        var replacement = new HiveTextBox
        {
            Name = "customer",
            Text = "Replacement"
        };
        form.Controls.Add(replacement);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                readCapability.Id,
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: control.Id,
                captureId: captured.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.target-stale",
            result.Error!.Code);
        Assert.Equal("Replacement", replacement.Text);
    }

    [Fact]
    public void AdapterLowLevelOperationsAreExplicitInterfaceImplementations()
    {
        var publicMethodNames = typeof(HiveWinFormsHostIntegrationAdapter)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain(nameof(IHiveHostIntegrationAdapter.CaptureAsync), publicMethodNames);
        Assert.DoesNotContain(nameof(IHiveHostIntegrationAdapter.ExecuteInteractionAsync), publicMethodNames);
        Assert.DoesNotContain(nameof(IHiveHostIntegrationAdapter.ResolveLookupAsync), publicMethodNames);

        var interfaceMap = typeof(HiveWinFormsHostIntegrationAdapter)
            .GetInterfaceMap(typeof(IHiveHostIntegrationAdapter));

        var lowLevelMethodNames = new HashSet<string>(
            [
                nameof(IHiveHostIntegrationAdapter.CaptureAsync),
                nameof(IHiveHostIntegrationAdapter.ExecuteInteractionAsync),
                nameof(IHiveHostIntegrationAdapter.ResolveLookupAsync)
            ],
            StringComparer.Ordinal);

        var lowLevelTargets = interfaceMap.TargetMethods
            .Where(method => lowLevelMethodNames.Contains(method.Name))
            .ToArray();

        Assert.Equal(3, lowLevelTargets.Length);
        Assert.All(
            lowLevelTargets,
            method => Assert.True(method.IsPrivate));

        Assert.Contains(
            nameof(HiveWinFormsHostIntegrationAdapter.AdapterId),
            publicMethodNames);
    }

    [Fact]
    public async Task ExecuteInteraction_SetsAndReadsStandardBooleanComboAndNumericControls()
    {
        using var form = new Form();

        var checkBox = new CheckBox
        {
            Name = "active",
            Checked = false
        };

        var comboBox = new ComboBox
        {
            Name = "status",
            DropDownStyle = ComboBoxStyle.DropDown,
            Text = "Pending"
        };
        comboBox.Items.AddRange(["Pending", "Approved"]);

        var numeric = new NumericUpDown
        {
            Name = "amount",
            Minimum = 0,
            Maximum = 100,
            Value = 10
        };

        form.Controls.Add(checkBox);
        form.Controls.Add(comboBox);
        form.Controls.Add(numeric);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var captured = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var captureId = captured.Provenance.CaptureId;
        Assert.Equal(
            typeof(bool).FullName,
            captured.Controls.Single(item => item.Name == "active").Field!.ValueType);
        Assert.Equal(
            typeof(string).FullName,
            captured.Controls.Single(item => item.Name == "status").Field!.ValueType);
        Assert.Equal(
            typeof(decimal).FullName,
            captured.Controls.Single(item => item.Name == "amount").Field!.ValueType);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var checkBoxControl = captured.Controls.Single(item => item.Name == "active");
        var checkBoxWrite = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                checkBoxControl.Capabilities.Single(item =>
                    item.Kind == HiveHostCapabilityKind.SetControlValue).Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: checkBoxControl.Id,
                value: HiveHostValue.FromBoolean(true),
                captureId: captureId),
            accessContext);

        Assert.True(checkBoxWrite.IsSuccess, checkBoxWrite.Error?.Message);
        Assert.True(checkBox.Checked);

        var comboControl = captured.Controls.Single(item => item.Name == "status");
        var comboWrite = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                comboControl.Capabilities.Single(item =>
                    item.Kind == HiveHostCapabilityKind.SetControlValue).Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: comboControl.Id,
                value: HiveHostValue.FromString("Approved"),
                captureId: captureId),
            accessContext);

        Assert.True(comboWrite.IsSuccess, comboWrite.Error?.Message);
        Assert.Equal("Approved", comboBox.Text);

        var numericControl = captured.Controls.Single(item => item.Name == "amount");
        var numericWrite = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                numericControl.Capabilities.Single(item =>
                    item.Kind == HiveHostCapabilityKind.SetControlValue).Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: numericControl.Id,
                value: HiveHostValue.FromDecimal(42.5m),
                captureId: captureId),
            accessContext);

        Assert.True(numericWrite.IsSuccess, numericWrite.Error?.Message);
        Assert.Equal(42.5m, numeric.Value);
    }

    [Fact]
    public async Task Capture_DropDownListDoesNotExposeStandardSetCapability()
    {
        using var form = new Form();
        var comboBox = new ComboBox
        {
            Name = "status",
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        comboBox.Items.AddRange(["Pending", "Approved"]);
        comboBox.SelectedIndex = 0;
        form.Controls.Add(comboBox);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var control = result.Value!.Controls.Single(item => item.Name == "status");

        Assert.Contains(
            control.Capabilities,
            capability => capability.Kind == HiveHostCapabilityKind.ReadControl);
        Assert.DoesNotContain(
            control.Capabilities,
            capability => capability.Kind == HiveHostCapabilityKind.SetControlValue);
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

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var captureId = descriptor.Provenance.CaptureId;
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
                value: HiveHostValue.FromDateTime(new DateTime(2027, 1, 1)),
                captureId: captureId),
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
    public async Task ExecuteInteraction_RejectsNumericOutsideHostRangeWithoutChangingValue()
    {
        using var form = new Form();
        var numeric = new NumericUpDown
        {
            Name = "amount",
            Minimum = 0,
            Maximum = 100,
            Value = 25
        };
        form.Controls.Add(numeric);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var captureId = descriptor.Provenance.CaptureId;
        var control = descriptor.Controls.Single(item => item.Name == "amount");
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
                value: HiveHostValue.FromDecimal(101m),
                captureId: captureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.numeric-range-invalid",
            result.Error!.Code);
        Assert.Equal(25m, numeric.Value);
    }

    [Fact]
    public async Task PasswordControl_IsNeverExposedAsValue()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var password = descriptor.Controls
            .Single(item => item.Name == "password");

        Assert.Null(password.Field!.CurrentValue);
        Assert.DoesNotContain(
            password.Capabilities,
            capability => capability.Kind == HiveHostCapabilityKind.SetControlValue);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var read = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                password.Capabilities.Single(capability =>
                    capability.Kind == HiveHostCapabilityKind.ReadControl).Id,
                HiveHostInteractionKind.ReadControl,
                CorrelationId.New(),
                controlId: password.Id,
                captureId: descriptor.Provenance.CaptureId),
            accessContext);

        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Null(read.Value!.ResultValue);
    }

    [Fact]
    public async Task ConsequentialInteraction_FromStaleCapture_IsRejected()
    {
        using var form = new Form();
        var original = new HiveTextBox
        {
            Name = "customer",
            Text = "Original"
        };
        form.Controls.Add(original);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var first = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var oldControl = first.Controls.Single(item => item.Name == "customer");
        var oldCaptureId = first.Provenance.CaptureId;
        var oldSetCapability = oldControl.Capabilities.Single(
            capability => capability.Kind == HiveHostCapabilityKind.SetControlValue);

        form.Controls.Remove(original);
        original.Dispose();

        var replacement = new HiveTextBox
        {
            Name = "customer",
            Text = "Replacement"
        };
        form.Controls.Add(replacement);

        var second = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        Assert.NotEqual(oldCaptureId, second.Provenance.CaptureId);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                oldSetCapability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: oldControl.Id,
                value: HiveHostValue.FromString("Stale mutation"),
                captureId: oldCaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.capture-stale",
            result.Error!.Code);
        Assert.Equal("Replacement", replacement.Text);
    }

    [Fact]
    public async Task ConsequentialInteraction_RejectsReplacedDataSurfaceWithoutRecapture()
    {
        using var form = new Form();
        var original = new HiveDataGridView
        {
            Name = "orders",
            AutoGenerateColumns = false
        };
        original.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id"
            });
        original.HiveDataSurface.SurfaceId = "orders";
        original.HiveDataSurface.AddCapability(
            new HiveHostCapabilityDescriptor(
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                HiveHostCapabilityKind.EditRow,
                "Edit order"));

        form.Controls.Add(original);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext,
            new AllowingSemanticProvider());

        var captured = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var surface = captured.DataSurfaces.Single(
            item => item.Id == "surface:orders");

        form.Controls.Remove(original);
        original.Dispose();

        var replacement = new HiveDataGridView
        {
            Name = "orders",
            AutoGenerateColumns = false
        };
        replacement.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id"
            });
        replacement.HiveDataSurface.SurfaceId = "orders";
        replacement.HiveDataSurface.AddCapability(
            new HiveHostCapabilityDescriptor(
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                HiveHostCapabilityKind.EditRow,
                "Edit order"));
        form.Controls.Add(replacement);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                surface.Capabilities.Single(
                    capability => capability.Kind == HiveHostCapabilityKind.EditRow).Id,
                HiveHostInteractionKind.EditRow,
                CorrelationId.New(),
                surfaceId: surface.Id,
                rowIdentity: new HiveHostRowIdentity("1"),
                fieldName: "Quantity",
                value: HiveHostValue.FromInt64(2),
                captureId: captured.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.target-stale",
            result.Error!.Code);
    }

    [Fact]
    public async Task ConsequentialInteraction_RejectsReplacedControlWithoutRecapture()
    {
        using var form = new Form();
        var original = new HiveTextBox
        {
            Name = "customer",
            Text = "Original"
        };
        form.Controls.Add(original);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var captured = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var control = captured.Controls.Single(item => item.Name == "customer");
        var capability = control.Capabilities.Single(
            item => item.Kind == HiveHostCapabilityKind.SetControlValue);

        form.Controls.Remove(original);
        original.Dispose();

        var replacement = new HiveTextBox
        {
            Name = "customer",
            Text = "Replacement"
        };
        form.Controls.Add(replacement);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                capability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: control.Id,
                value: HiveHostValue.FromString("Invalid rebind"),
                captureId: captured.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.target-stale",
            result.Error!.Code);
        Assert.Equal("Replacement", replacement.Text);
    }

    [Fact]
    public async Task ConsequentialInteraction_RejectsReparentedControlWithoutRecapture()
    {
        using var form = new Form();

        var originalContainer = new Panel
        {
            Name = "originalContainer"
        };
        var customer = new HiveTextBox
        {
            Name = "customer",
            Text = "Original"
        };
        originalContainer.Controls.Add(customer);
        form.Controls.Add(originalContainer);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var captured = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var control = captured.Controls.Single(item => item.Name == "customer");
        var capability = control.Capabilities.Single(
            item => item.Kind == HiveHostCapabilityKind.SetControlValue);

        form.Controls.Remove(originalContainer);

        var replacementContainer = new Panel
        {
            Name = "replacementContainer"
        };
        replacementContainer.Controls.Add(customer);
        form.Controls.Add(replacementContainer);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                capability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: control.Id,
                value: HiveHostValue.FromString("Invalid reparent"),
                captureId: captured.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.target-stale",
            result.Error!.Code);
        Assert.Equal("Original", customer.Text);
    }

    [Fact]
    public async Task ConsequentialInteraction_RejectsReparentedDataSurfaceWithoutRecapture()
    {
        using var form = new Form();

        var originalContainer = new Panel
        {
            Name = "originalContainer"
        };
        var grid = new HiveDataGridView
        {
            Name = "orders",
            AutoGenerateColumns = false
        };
        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Id",
                DataPropertyName = "Id"
            });
        grid.HiveDataSurface.SurfaceId = "orders";
        grid.HiveDataSurface.AddCapability(
            new HiveHostCapabilityDescriptor(
                Guid.Parse("00000000-0000-0000-0000-000000000011"),
                HiveHostCapabilityKind.EditRow,
                "Edit order"));

        originalContainer.Controls.Add(grid);
        form.Controls.Add(originalContainer);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext,
            new AllowingSemanticProvider());

        var captured = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var surface = captured.DataSurfaces.Single(
            item => item.Id == "surface:orders");
        var capability = surface.Capabilities.Single(
            item => item.Kind == HiveHostCapabilityKind.EditRow);

        form.Controls.Remove(originalContainer);

        var replacementContainer = new Panel
        {
            Name = "replacementContainer"
        };
        replacementContainer.Controls.Add(grid);
        form.Controls.Add(replacementContainer);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                capability.Id,
                HiveHostInteractionKind.EditRow,
                CorrelationId.New(),
                surfaceId: surface.Id,
                rowIdentity: new HiveHostRowIdentity("1"),
                value: HiveHostValue.FromInt64(2),
                captureId: captured.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.target-stale",
            result.Error!.Code);
    }

    [Fact]
    public async Task ConsequentialInteraction_CannotRebindOldCapabilityToCurrentCapture()
    {
        using var form = new Form();
        var original = new HiveTextBox
        {
            Name = "customer",
            Text = "Original"
        };
        form.Controls.Add(original);

        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);

        var first = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var oldControl = first.Controls.Single(item => item.Name == "customer");
        var oldCapability = oldControl.Capabilities.Single(
            capability => capability.Kind == HiveHostCapabilityKind.SetControlValue);

        form.Controls.Remove(original);
        original.Dispose();

        var replacement = new HiveTextBox
        {
            Name = "customer",
            Text = "Replacement",
            ReadOnly = true
        };
        form.Controls.Add(replacement);

        var second = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        Assert.DoesNotContain(
            second.Controls.Single(item => item.Name == "customer").Capabilities,
            capability => capability.Kind == HiveHostCapabilityKind.SetControlValue);

        var service = new HiveHostIntegrationService(
            new AllowAllAuthorizer());

        var result = await service.ExecuteInteractionAsync(
            adapter,
            new HiveHostInteractionRequest(
                oldCapability.Id,
                HiveHostInteractionKind.SetControlValue,
                CorrelationId.New(),
                controlId: oldControl.Id,
                value: HiveHostValue.FromString("Invalid rebind"),
                captureId: second.Provenance.CaptureId),
            accessContext);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.capability-mismatch",
            result.Error!.Code);
        Assert.Equal("Replacement", replacement.Text);
    }

    [Fact]
    public async Task AccessContextMismatch_IsRejected()
    {
        using var form = CreateFixtureForm();
        var registeredContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            registeredContext);

        var result = await AsIntegrationAdapter(adapter).CaptureAsync(CreateAccessContext());

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
            () => AsIntegrationAdapter(adapter).CaptureAsync(accessContext));

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
    public async Task ConsequentialInteraction_FromBackgroundThread_IsRejectedBeforeFreshnessTraversal()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);
        _ = form.Handle;

        var customer = (TextBox)FindControl(form, "customer");
        var textChangedCount = 0;
        customer.TextChanged += (_, _) => Interlocked.Increment(ref textChangedCount);

        var descriptor = (await AsIntegrationAdapter(adapter).CaptureAsync(accessContext)).Value!;
        var control = descriptor.Controls.Single(item => item.Name == "customer");
        var capability = control.Capabilities.Single(
            item => item.Kind == HiveHostCapabilityKind.SetControlValue);
        var baselineTextChangedCount = Volatile.Read(ref textChangedCount);

        var result = await Task.Run(() =>
            AsIntegrationAdapter(adapter).ExecuteInteractionAsync(
                new HiveHostInteractionRequest(
                    capability.Id,
                    HiveHostInteractionKind.SetControlValue,
                    CorrelationId.New(),
                    controlId: control.Id,
                    value: HiveHostValue.FromString("blocked"),
                    captureId: descriptor.Provenance.CaptureId),
                accessContext));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.ui-thread-required",
            result.Error!.Code);
        Assert.Equal(
            baselineTextChangedCount,
            Volatile.Read(ref textChangedCount));
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

        var capturedResult = await AsIntegrationAdapter(adapter).CaptureAsync(accessContext);
        Assert.True(capturedResult.IsSuccess, capturedResult.Error?.Message);
        var captured = capturedResult.Value!;

        for (var index = 0; index < 600; index++)
        {
            form.Controls.Add(new Panel
            {
                Name = $"padding{index}"
            });
        }

        var result = await Task.Run(() =>
            AsIntegrationAdapter(adapter).ExecuteInteractionAsync(
                new HiveHostInteractionRequest(
                    Guid.NewGuid(),
                    HiveHostInteractionKind.ReadControl,
                    CorrelationId.New(),
                    controlId: "control:missing",
                    captureId: captured.Provenance.CaptureId),
                accessContext));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.winforms.ui-thread-required",
            result.Error!.Code);
    }

    [Fact]
    public async Task Capture_FromBackgroundThread_IsRejectedBeforeHostTraversal()
    {
        using var form = CreateFixtureForm();
        var accessContext = CreateAccessContext();
        using var adapter = new HiveWinFormsHostIntegrationAdapter(
            form,
            accessContext);
        _ = form.Handle;

        var result = await Task.Run(
            () => AsIntegrationAdapter(adapter).CaptureAsync(accessContext));

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
            () => AsIntegrationAdapter(adapter).CaptureAsync(
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

    private static IHiveHostIntegrationAdapter AsIntegrationAdapter(
        HiveWinFormsHostIntegrationAdapter adapter) =>
        adapter;

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

    private sealed class AllowingSemanticProvider :
        IHiveWinFormsSemanticProvider
    {
        public bool TryDescribeDataSurface(
            DataGridView grid,
            string surfaceId,
            out HiveHostDataSurfaceDescriptor descriptor)
        {
            descriptor = null!;
            return false;
        }

        public Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
            HiveHostInteractionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Result<HiveHostInteractionResult>.Success(
                    new HiveHostInteractionResult(
                        request.CorrelationId,
                        request.Kind)));
        }

        public Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
            HiveLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Result<IReadOnlyList<HiveLookupOption>>.Success(
                    Array.Empty<HiveLookupOption>()));
        }

        public IReadOnlyList<HiveHostBusinessOperationDescriptor>
            GetBusinessOperations() =>
            Array.Empty<HiveHostBusinessOperationDescriptor>();
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
