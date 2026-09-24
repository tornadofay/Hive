using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
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
    public void HiveExampleTestSurface_DisablesCodeCopyWhenSnippetIsEmpty()
    {
        using var surface = new HiveExampleTestSurface();

        Assert.False(surface.CopyCodeButton.Enabled);

        surface.CodeSnippet = "// public API";

        Assert.True(surface.CopyCodeButton.Enabled);

        surface.CodeSnippet = string.Empty;

        Assert.False(surface.CopyCodeButton.Enabled);
    }

    [Fact]
    public void HiveExampleTestSurface_DisablesRunUntilConfiguredAndEnablesWhenConfigured()
    {
        using var surface = new HiveExampleTestSurface();

        Assert.False(surface.RunButton.Enabled);

        surface.ConfigureRun(_ => Task.CompletedTask);

        Assert.True(surface.RunButton.Enabled);
    }

    [Fact]
    public async Task HiveExampleTestSurface_ReportsSuccessfulRunState()
    {
        using var surface = new HiveExampleTestSurface();

        await surface.RunAsync(
            _ => Task.CompletedTask);

        Assert.Equal("Completed.", surface.StatusLabel.Text);
    }

    [Fact]
    public void HiveEditorLayout_KeepsSingleLineEditorsAtCompactHeight()
    {
        using var layout = new HiveEditorLayout();
        using var textBox = new TextBox();
        using var comboBox = new ComboBox();

        layout.Size = new Size(800, 400);
        layout.AddField("Name", "Name.", textBox);
        layout.AddField("Type", "Type.", comboBox);
        layout.CreateControl();
        layout.PerformLayout();
        layout.FieldsPanel.PerformLayout();

        var textHost = layout.FieldsPanel.GetControlFromPosition(1, 0);
        var comboHost = layout.FieldsPanel.GetControlFromPosition(1, 1);

        Assert.NotNull(textHost);
        Assert.NotNull(comboHost);
        Assert.IsType<TableLayoutPanel>(textHost);
        Assert.IsType<TableLayoutPanel>(comboHost);
        Assert.Equal(32, textBox.Height);
        Assert.True(comboBox.Height > 0);
        Assert.True(comboBox.Height <= 32);

        var textHostLayout = (TableLayoutPanel)textHost!;
        var comboHostLayout = (TableLayoutPanel)comboHost!;
        Assert.Equal(32, textHostLayout.GetRowHeights()[1]);
        Assert.Equal(32, comboHostLayout.GetRowHeights()[1]);
        Assert.Equal(DockStyle.Fill, textBox.Dock);
        Assert.Equal(DockStyle.None, comboBox.Dock);

        var expectedComboTop = (comboHost.Height - comboBox.Height) / 2;
        Assert.InRange(comboBox.Top, expectedComboTop - 1, expectedComboTop + 1);
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
        Assert.True(textBox.Height >= 32);
    }

    [Fact]
    public void HiveCrudPage_AppliesErrorStatusToneAndPreservesItAcrossThemeChanges()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var form = new TestHiveForm(themeManager);
        using var page = new HiveCrudPage<TestItem>();

        form.Body.Controls.Add(page);
        themeManager.Apply(form.Body);

        page.SetStatus("Operation failed.", HiveStatusTone.Error);

        Assert.Equal(
            themeManager.Theme.VisualStates.Error,
            page.StatusLabel.ForeColor);

        themeManager.SetMode(HiveThemeMode.Dark);

        Assert.Equal(
            themeManager.Theme.VisualStates.Error,
            page.StatusLabel.ForeColor);
    }

    [Fact]
    public void HiveCrudPage_UsesNonAutosizingSearchAndCenteredStatusFilter()
    {
        using var page = new HiveCrudPage<TestItem>();

        Assert.False(page.SearchBox.AutoSize);

        page.StatusSelector = item => item.Name;

        var statusFilter = page.ActionBarPanel
            .Controls
            .OfType<TableLayoutPanel>()
            .SelectMany(static layout => layout.Controls.Cast<Control>())
            .OfType<FlowLayoutPanel>()
            .SelectMany(static flow => flow.Controls.Cast<Control>())
            .OfType<ComboBox>()
            .FirstOrDefault();

        Assert.NotNull(statusFilter);
        Assert.Equal(new Padding(0, 4, 0, 4), statusFilter!.Margin);
        Assert.False(statusFilter.IntegralHeight);
    }

    [Fact]
    public void HiveCrudPage_ReturnsStatusToNeutralAfterListRebuild()
    {
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);
        using var form = new TestHiveForm(themeManager);
        using var page = new HiveCrudPage<TestItem>();

        form.Body.Controls.Add(page);
        themeManager.Apply(form.Body);

        page.SetStatus("Operation failed.", HiveStatusTone.Error);
        page.SetColumns(
            new HiveCrudColumn<TestItem>(
                "Name",
                160,
                item => item.Name));

        Assert.Equal(
            themeManager.Theme.Palette.MutedText,
            page.StatusLabel.ForeColor);
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

    private sealed class TestHiveForm : HiveForm
    {
        public TestHiveForm(IHiveThemeManager themeManager)
            : base(
                "Test",
                string.Empty,
                new Size(720, 480),
                new Size(640, 420),
                themeManager)
        {
        }

        public Control Body => BodyPanel;
    }
}
