using System.Drawing;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
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

    private sealed class AllowAllAuthorizer :
        IHiveHostCapabilityAuthorizer
    {
        public Result Authorize(
            HiveHostCapabilityRequest request,
            ResourceAccessContext accessContext) =>
            Result.Success();
    }
}
