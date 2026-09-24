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
