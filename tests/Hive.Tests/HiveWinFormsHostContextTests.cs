using System.Drawing;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Xunit;

namespace Hive.Tests;

public sealed class HiveWinFormsHostContextTests
{
    [Fact]
    public async Task Capture_DiscoversNestedWinFormsControlsDeterministically()
    {
        using var form = CreateFixtureForm();
        using var context = new HiveWinFormsHostContext(CreateAccessContext());
        using var registration = context.Register(form);

        var result = await context.CaptureAsync(registration);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = result.Value!;

        Assert.Equal(7, snapshot.ControlCount);
        Assert.Equal(typeof(Form).FullName, snapshot.RootRuntimeType);
        Assert.Equal("fixtureForm", snapshot.RootName);

        Assert.Equal(
            [
                "0",
                "0/0",
                "0/0/0",
                "0/0/1",
                "0/1",
                "0/1/0",
                "0/1/1"
            ],
            snapshot.Controls.Select(static control => control.Path).ToArray());

        var password = snapshot.Controls.Single(control => control.Name == "passwordBox");
        Assert.Equal("[redacted]", password.Text);
        Assert.False(password.ReadOnly);

        var dataGrid = snapshot.Controls.Single(control => control.Name == "ordersGrid");
        Assert.Equal(typeof(BindingSource).FullName, dataGrid.DataSourceType);
    }

    [Fact]
    public async Task Capture_StopsAtConfiguredDepthInsteadOfSilentlyTruncating()
    {
        using var form = CreateFixtureForm();
        using var context = new HiveWinFormsHostContext(
            CreateAccessContext(),
            new HiveWinFormsHostContextOptions(maxDepth: 1, maxNodes: 32));

        using var registration = context.Register(form);

        var result = await context.CaptureAsync(registration);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.context.depth-limit-exceeded",
            result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
    }

    [Fact]
    public async Task Capture_StopsAtConfiguredNodeLimitInsteadOfSilentlyTruncating()
    {
        using var form = CreateFixtureForm();
        using var context = new HiveWinFormsHostContext(
            CreateAccessContext(),
            new HiveWinFormsHostContextOptions(maxDepth: 8, maxNodes: 3));

        using var registration = context.Register(form);

        var result = await context.CaptureAsync(registration);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.context.node-limit-exceeded",
            result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
    }

    [Fact]
    public async Task Capture_CancellationIsCooperative()
    {
        using var form = CreateFixtureForm();
        using var context = new HiveWinFormsHostContext(CreateAccessContext());
        using var registration = context.Register(form);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => context.CaptureAsync(registration, cancellation.Token));
    }

    [Fact]
    public async Task Capture_RejectsDisposedRegistration()
    {
        using var form = CreateFixtureForm();
        using var context = new HiveWinFormsHostContext(CreateAccessContext());
        var registration = context.Register(form);

        registration.Dispose();

        var result = await context.CaptureAsync(registration);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.context.registration-disposed",
            result.Error!.Code);
    }

    [Fact]
    public async Task Capture_PreservesRegistrationProvenance()
    {
        using var form = CreateFixtureForm();
        var access = CreateAccessContext();
        using var context = new HiveWinFormsHostContext(access);
        using var registration = context.Register(form);

        var result = await context.CaptureAsync(registration);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(registration.RegistrationId, result.Value!.Provenance.RegistrationId);
        Assert.Equal(access.DeploymentId, result.Value.Provenance.DeploymentId);
        Assert.Equal(access.PrincipalId, result.Value.Provenance.PrincipalId);
        Assert.NotEqual(Guid.Empty, result.Value.Provenance.CaptureId);
    }

    [Fact]
    public void Phase13ImageFixture_CreatesValidImageSubmission()
    {
        var content = ReadFixture();

        var submission = new WorkItemImageSubmission(
            "Phase13Sample.svg",
            "image/svg+xml",
            content);

        Assert.True(submission.Content.Length > 0);
        Assert.Equal("image/svg+xml", submission.MediaType);
        Assert.Equal(content.Length, submission.Content.Length);
    }

    [Fact]
    public void Register_RequiresDeploymentAndPrincipal()
    {
        Assert.Throws<ArgumentException>(() =>
            new HiveWinFormsHostContext(
                new ResourceAccessContext()));
    }

    private static Form CreateFixtureForm()
    {
        var form = new Form
        {
            Name = "fixtureForm",
            Text = "Fixture"
        };

        var firstPanel = new Panel
        {
            Name = "firstPanel"
        };

        var password = new TextBox
        {
            Name = "passwordBox",
            UseSystemPasswordChar = true,
            Text = "not-secret-output"
        };

        var input = new TextBox
        {
            Name = "inputBox",
            Text = "Invoice"
        };

        var secondPanel = new Panel
        {
            Name = "secondPanel"
        };

        var bindingSource = new BindingSource
        {
            DataSource = new[]
            {
                new
                {
                    Id = 1,
                    Name = "Invoice"
                }
            }
        };

        var grid = new DataGridView
        {
            Name = "ordersGrid",
            DataSource = bindingSource
        };

        var button = new Button
        {
            Name = "submitButton",
            Text = "Submit"
        };

        firstPanel.Controls.Add(password);
        firstPanel.Controls.Add(input);
        secondPanel.Controls.Add(grid);
        secondPanel.Controls.Add(button);
        form.Controls.Add(firstPanel);
        form.Controls.Add(secondPanel);

        return form;
    }

    private static byte[] ReadFixture()
    {
        var assembly = typeof(HiveWinFormsHostContextTests).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .First(name => name.EndsWith(
                "Fixtures.Phase13Sample.svg",
                StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                "The Phase 1.13 image fixture could not be loaded.");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static ResourceAccessContext CreateAccessContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
}
