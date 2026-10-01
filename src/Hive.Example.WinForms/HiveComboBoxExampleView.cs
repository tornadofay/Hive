using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class HiveComboBoxExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveComboBox _combo;
    private readonly HiveComboBox _statusCombo;
    private readonly HiveButton _openButton;
    private readonly Label _stateLabel;
    private readonly Label _themeLabel;
    private readonly Font _titleFont;
    private readonly Font _sectionFont;

    public HiveComboBoxExampleView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _themeManager = services.GetThemeManager();

        var typography = _themeManager.Theme.Typography;
        _titleFont = new Font(
            typography.FontFamily,
            typography.TitleSize,
            FontStyle.Bold);
        _sectionFont = new Font(
            typography.FontFamily,
            typography.SectionSize,
            FontStyle.Bold);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoScroll = false
        };
        root.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        var title = new Label
        {
            AutoSize = true,
            Font = _titleFont,
            Text = "HiveComboBox",
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(920, 72),
            Text =
                "This example uses the Hive-owned ComboBox field and popup. The main list is data-bound, supports deterministic case-insensitive filtering, uses the Hive scrollbar for overflow, and keeps selection identity separate from display text.",
            Margin = new Padding(0, 8, 0, 14)
        };

        var section = new Label
        {
            AutoSize = true,
            Font = _sectionFont,
            Text = "Long filtered selection",
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _combo = new HiveComboBox
        {
            Name = "longFilteredCombo",
            Dock = DockStyle.Fill,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(Choice.Name),
            ValueMember = nameof(Choice.Id),
            DropDownWidth = 360,
            MaxDropDownItems = 8,
            ItemHeight = 34,
            AccessibleName = "Long filtered selection"
        };

        var choices = new BindingList<Choice>();
        for (var index = 1; index <= 80; index++)
        {
            choices.Add(
                new Choice(
                    index,
                    $"Option {index:00} — Deterministic example item"));
        }

        _combo.DataSource = choices;
        _combo.SelectedIndex = 0;
        _combo.SelectedIndexChanged += ComboOnSelectedIndexChanged;
        _combo.DropDownOpened += ComboOnDropDownOpened;
        _combo.DropDownClosed += ComboOnDropDownClosed;

        _openButton = new HiveButton
        {
            Text = "Open popup",
            Style = HiveButtonStyle.Secondary,
            Width = 120,
            Height = 36,
            Margin = new Padding(0, 8, 0, 0),
            AccessibleName = "Open HiveComboBox popup"
        };
        _openButton.Click += (_, _) => _combo.ShowDropDown();

        var secondSection = new Label
        {
            AutoSize = true,
            Font = _sectionFont,
            Text = "Unbound Items contract",
            Margin = new Padding(0, 18, 0, 0),
            Padding = Padding.Empty
        };

        _statusCombo = new HiveComboBox
        {
            Name = "statusCombo",
            Dock = DockStyle.Fill,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            AccessibleName = "Unbound status selection"
        };
        _statusCombo.Items.AddRange(
        [
            "Draft",
            "Queued",
            "Approved",
            "Rejected",
            "Needs review"
        ]);

        _stateLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(920, 80),
            Margin = new Padding(0, 12, 0, 0),
            Padding = Padding.Empty
        };

        _themeLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty,
            Text = GetThemeText()
        };

        root.Controls.Add(title, 0, root.RowCount++);
        root.Controls.Add(description, 0, root.RowCount++);
        root.Controls.Add(section, 0, root.RowCount++);
        root.Controls.Add(_combo, 0, root.RowCount++);
        root.Controls.Add(_openButton, 0, root.RowCount++);
        root.Controls.Add(secondSection, 0, root.RowCount++);
        root.Controls.Add(_statusCombo, 0, root.RowCount++);
        root.Controls.Add(_stateLabel, 0, root.RowCount++);
        root.Controls.Add(_themeLabel, 0, root.RowCount++);

        Controls.Add(root);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        UpdateStateText();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _combo.SelectedIndexChanged -= ComboOnSelectedIndexChanged;
            _combo.DropDownOpened -= ComboOnDropDownOpened;
            _combo.DropDownClosed -= ComboOnDropDownClosed;
            _titleFont.Dispose();
            _sectionFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ComboOnSelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        UpdateStateText();
    }

    private void ComboOnDropDownOpened(
        object? sender,
        EventArgs e)
    {
        UpdateStateText("Popup open. Type in the filter field, use Up/Down, or press Enter/Escape.");
    }

    private void ComboOnDropDownClosed(
        object? sender,
        EventArgs e)
    {
        UpdateStateText();
    }

    private void ThemeManagerOnChanged(
        object? sender,
        EventArgs e)
    {
        _themeLabel.Text = GetThemeText();
    }

    private void UpdateStateText(string? interaction = null)
    {
        var selected = _combo.SelectedItem as Choice;

        _stateLabel.Text =
            $"SelectedIndex: {_combo.SelectedIndex}    " +
            $"SelectedValue: {_combo.SelectedValue ?? "<null>"}    " +
            $"Text: {_combo.Text}" +
            (selected is null
                ? string.Empty
                : Environment.NewLine + $"Selected item: {selected.Name}") +
            (string.IsNullOrWhiteSpace(interaction)
                ? string.Empty
                : Environment.NewLine + interaction);
    }

    private string GetThemeText() =>
        $"Active theme mode: {_themeManager.Mode}. Use the Example Host theme control to verify Light, Dark, and System rendering.";

    private sealed record Choice(int Id, string Name);
}
