using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ThemeFoundationExampleForm : Form
{
    private readonly HiveThemeManager _themeManager;
    private readonly Label _themeValue;
    private readonly TextBox _input;
    private readonly CheckBox _checkBox;
    private readonly HiveButton _messageButton;

    public ThemeFoundationExampleForm()
    {
        _themeManager = new HiveThemeManager();

        Text = "Hive — WinForms UI Foundation";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 480);
        Size = new Size(900, 600);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 4
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            Text = "Hive UI Foundation",
            Margin = new Padding(0, 0, 0, 16)
        };

        var description = new Label
        {
            AutoSize = true,
            Text = "Hive-owned Light, Dark, and System themes with a replaceable renderer boundary.",
            Margin = new Padding(0, 0, 0, 20)
        };

        var themeButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 16)
        };

        AddThemeButton(themeButtons, "Light", HiveThemeMode.Light);
        AddThemeButton(themeButtons, "Dark", HiveThemeMode.Dark);
        AddThemeButton(themeButtons, "System", HiveThemeMode.System);

        var form = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, 8, 0, 8)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));

        _themeValue = new Label { AutoSize = true };
        _input = new TextBox { Width = 260, Text = "Hive UI" };
        _checkBox = new CheckBox { AutoSize = true, Text = "Native WinForms control" };
        var disabledCheckBox = new CheckBox
        {
            AutoSize = true,
            Enabled = false,
            Text = "Disabled native control"
        };

        form.Controls.Add(new Label { AutoSize = true, Text = "Effective mode", Margin = new Padding(0, 8, 16, 0) }, 0, 0);
        form.Controls.Add(_themeValue, 1, 0);
        form.Controls.Add(new Label { AutoSize = true, Text = "Text input", Margin = new Padding(0, 8, 16, 0) }, 0, 1);
        form.Controls.Add(_input, 1, 1);
        form.Controls.Add(_checkBox, 0, 2);
        form.Controls.Add(disabledCheckBox, 1, 2);

        _messageButton = new HiveButton
        {
            Text = "Open HiveMessageBox",
            Width = 190,
            Margin = new Padding(0, 16, 0, 0)
        };
        _messageButton.Click += (_, _) =>
            HiveMessageBox.Show(
                this,
                "The message box is exposed through a Hive-owned API.",
                "Hive UI");

        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Top,
            WrapContents = false
        };
        content.Controls.Add(form);
        content.Controls.Add(_messageButton);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(description, 0, 1);
        root.Controls.Add(themeButtons, 0, 2);
        root.Controls.Add(content, 0, 3);

        Controls.Add(root);

        _themeManager.ThemeChanged += (_, _) => ApplyTheme();
        ApplyTheme();
    }

    private void AddThemeButton(Control parent, string text, HiveThemeMode mode)
    {
        var button = new HiveButton
        {
            Text = text,
            Width = 96,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };
        button.Click += (_, _) => _themeManager.SetMode(mode);
        parent.Controls.Add(button);
    }

    private void ApplyTheme()
    {
        _themeManager.Apply(this);
        _themeValue.Text = $"{_themeManager.Mode} → {_themeManager.Theme.Mode}";
        _themeValue.ForeColor = _themeManager.Theme.Palette.Accent;
    }
}
