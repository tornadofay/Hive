using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Tests;

public sealed class HiveButtonTests
{
    [Fact]
    public void HiveButton_ImplementsWinFormsDialogActionContract()
    {
        using var button = new HiveButton();
        using var form = new Form();

        Assert.IsAssignableFrom<IButtonControl>(button);

        form.AcceptButton = button;
        form.CancelButton = button;

        Assert.Same(button, form.AcceptButton);
        Assert.Same(button, form.CancelButton);

        var control = (IButtonControl)button;

        Assert.Equal(DialogResult.None, control.DialogResult);

        control.DialogResult = DialogResult.OK;

        Assert.Equal(DialogResult.OK, control.DialogResult);
    }

    [Fact]
    public void PerformClick_RaisesClickWhenEnabled()
    {
        using var button = new HiveButton();
        var clickCount = 0;

        button.Click += (_, _) => clickCount++;

        button.PerformClick();

        Assert.Equal(1, clickCount);
    }

    [Fact]
    public void PerformClick_DoesNotRaiseClickWhenDisabled()
    {
        using var button = new HiveButton
        {
            Enabled = false
        };
        var clickCount = 0;

        button.Click += (_, _) => clickCount++;

        button.PerformClick();

        Assert.Equal(0, clickCount);
    }
}
