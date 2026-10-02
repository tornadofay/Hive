using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Xunit;

namespace Hive.Tests;

public sealed class HiveWinFormsBaseControlIntegrationTests
{
    [Fact]
    public void HostControlsPreserveNativeInheritanceWhileHiveUiControlsRemainIndependent()
    {
        Assert.True(typeof(TextBox).IsAssignableFrom(typeof(HostTextBox)));
        Assert.True(typeof(ComboBox).IsAssignableFrom(typeof(HostComboBox)));
        Assert.False(typeof(ComboBox).IsAssignableFrom(typeof(HiveComboBox)));
        Assert.True(typeof(Control).IsAssignableFrom(typeof(HiveComboBox)));
        Assert.True(typeof(CheckBox).IsAssignableFrom(typeof(HostCheckBox)));
        Assert.True(typeof(DateTimePicker).IsAssignableFrom(typeof(HostDateTimePicker)));
        Assert.True(typeof(NumericUpDown).IsAssignableFrom(typeof(HostNumericUpDown)));
        Assert.True(typeof(DataGridView).IsAssignableFrom(typeof(HostDataGridView)));
        Assert.True(typeof(Form).IsAssignableFrom(typeof(HiveForm)));

        using var combo = new HiveComboBox();
        Assert.NotNull(combo.HiveIntegration);
        Assert.NotNull(combo.HiveField);
        Assert.Single(combo.Controls);
        Assert.IsType<TextBox>(combo.Controls[0]);
    }

    [Fact]
    public void ConfigureFieldRejectsBlankFieldKeys()
    {
        using var grid = new HostDataGridView();

        Assert.Throws<ArgumentException>(
            () => grid.HiveDataSurface.ConfigureField(" "));
    }

    [Fact]
    public void DataSurfaceFieldOverridesUseCaseInsensitiveKeys()
    {
        using var grid = new HostDataGridView();

        var metadata = grid.HiveDataSurface.ConfigureField("CustomerId");
        metadata.Required = true;

        var sameMetadata = grid.HiveDataSurface.ConfigureField("customerid");

        Assert.Same(metadata, sameMetadata);
        Assert.True(
            grid.HiveDataSurface.TryGetFieldOverride(
                "CUSTOMERID",
                out var resolved));
        Assert.Same(metadata, resolved);
    }

    [Fact]
    public void DataSurfaceRejectsDuplicateCapabilityIdentity()
    {
        using var grid = new HostDataGridView();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var capability = new HiveHostCapabilityDescriptor(
            id,
            HiveHostCapabilityKind.ReadRow,
            "Read row");

        grid.HiveDataSurface.AddCapability(capability);

        Assert.Throws<ArgumentException>(
            () => grid.HiveDataSurface.AddCapability(capability));
    }

    [Fact]
    public void FormExposesHostMetadataWithoutReplacingWinFormsLifecycle()
    {
        using var form = new TestHiveForm();

        form.HiveHostIntegration.HostName = "Reference Application";

        Assert.Equal(
            "Reference Application",
            form.HiveHostIntegration.HostName);
        Assert.False(form.IsDisposed);
    }

    private sealed class TestHiveForm :
        HiveForm
    {
        public TestHiveForm()
            : base("Test", "Test")
        {
        }
    }
}
