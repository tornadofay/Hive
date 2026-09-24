using System.Threading.Tasks;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Xunit;

namespace Hive.Tests;

public sealed class HiveUiPolishTests
{
    [Fact]
    public void HiveExampleOutputView_NotifiesWhenOutputBecomesUnavailable()
    {
        using var output = new HiveExampleOutputView();
        var changeCount = 0;

        output.OutputAvailabilityChanged += (_, _) => changeCount++;

        output.OutputText = "output";
        output.OutputText = string.Empty;

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void HiveExampleOutputView_ClearNotifiesHostOfAvailabilityChange()
    {
        using var output = new HiveExampleOutputView();
        var changeCount = 0;

        output.OutputAvailabilityChanged += (_, _) => changeCount++;

        output.Write("TEST", "output");
        output.Clear();

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void HiveEditorLayout_KeepsSingleLineEditorsAtCompactHeight()
    {
        using var layout = new HiveEditorLayout();
        using var textBox = new TextBox();
        using var comboBox = new ComboBox();

        layout.AddField("Name", "Name.", textBox);
        layout.AddField("Type", "Type.", comboBox);

        var textHost = layout.FieldsPanel.GetControlFromPosition(1, 0);
        var comboHost = layout.FieldsPanel.GetControlFromPosition(1, 1);

        Assert.NotNull(textHost);
        Assert.NotNull(comboHost);
        Assert.IsType<TableLayoutPanel>(textHost);
        Assert.IsType<TableLayoutPanel>(comboHost);
        Assert.Equal(32, textBox.Height);
        Assert.Equal(32, comboBox.Height);
        Assert.Equal(DockStyle.Fill, textBox.Dock);
        Assert.Equal(DockStyle.Fill, comboBox.Dock);
    }

    [Fact]
    public void HiveEditorLayout_PreservesFullHeightForMultilineEditors()
    {
        using var layout = new HiveEditorLayout();
        using var textBox = new TextBox
        {
            Multiline = true
        };

        layout.AddField("Description", "Description.", textBox, 150);

        var host = layout.FieldsPanel.GetControlFromPosition(1, 0);

        Assert.NotNull(host);
        Assert.IsType<Panel>(host);
        Assert.Equal(DockStyle.Fill, textBox.Dock);
        Assert.True(textBox.Height > 32);
    }

    [Fact]
    public async Task HiveCrudPage_UsesFilterLanguageWhenFilteredResultIsEmpty()
    {
        using var page = new HiveCrudPage<TestItem>();

        page.SetColumns(
            new HiveCrudColumn<TestItem>(
                "Name",
                160,
                item => item.Name));

        page.LoadItemsAsync = _ =>
            Task.FromResult<IReadOnlyList<TestItem>>(
                new[]
                {
                    new TestItem("Alpha")
                });

        await page.RefreshAsync();

        page.SearchText = "missing";

        var emptyState = FindLabel(page, "No items match the current filters.");

        Assert.NotNull(emptyState);
        Assert.True(emptyState!.Visible);
    }

    private static Label? FindLabel(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Label label &&
                string.Equals(label.Text, text, StringComparison.Ordinal))
            {
                return label;
            }

            var nested = FindLabel(child, text);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private sealed record TestItem(string Name);
}
