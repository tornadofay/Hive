using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Xunit;

namespace Hive.Tests;

public sealed class HiveWinFormsBaseControlIntegrationTests
{
    [Fact]
    public void BaseControlsExposeExpectedNativeInheritance()
    {
        Assert.True(typeof(TextBox).IsAssignableFrom(typeof(HiveTextBox)));
        Assert.True(typeof(ComboBox).IsAssignableFrom(typeof(HiveComboBox)));
        Assert.True(typeof(CheckBox).IsAssignableFrom(typeof(HiveCheckBox)));
        Assert.True(typeof(DateTimePicker).IsAssignableFrom(typeof(HiveDateTimePicker)));
        Assert.True(typeof(NumericUpDown).IsAssignableFrom(typeof(HiveNumericUpDown)));
        Assert.True(typeof(DataGridView).IsAssignableFrom(typeof(HiveDataGridView)));
        Assert.True(typeof(Form).IsAssignableFrom(typeof(HiveForm)));
    }

    [Fact]
    public void ConfigureFieldRejectsBlankFieldKeys()
    {
        using var grid = new HiveDataGridView();

        Assert.Throws<ArgumentException>(
            () => grid.HiveDataSurface.ConfigureField(" "));
    }

    [Fact]
    public void DataSurfaceRejectsDuplicateCapabilityIdentity()
    {
        using var grid = new HiveDataGridView();
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
