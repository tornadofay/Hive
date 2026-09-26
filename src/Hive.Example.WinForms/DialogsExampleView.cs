using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class DialogsExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly Label _description;
    private readonly FlowLayoutPanel _buttons;

    public DialogsExampleView(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        _themeManager = themeManager;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        AutoScroll = true;

        _description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(860, 72),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Semantic dialogs use consistent hierarchy, spacing, status accents, technical-detail presentation, and keyboard-safe action handling."
        };

        _buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 18, 0, 0),
            Padding = Padding.Empty
        };

        AddDialogButton(
            "Information",
            HiveMessageType.Information,
            "The information operation completed.");

        AddDialogButton(
            "Success",
            HiveMessageType.Success,
            "The operation completed successfully.");

        AddDialogButton(
            "Warning",
            HiveMessageType.Warning,
            "Review the current state before continuing.");

        AddDialogButton(
            "Error",
            HiveMessageType.Error,
            "The operation could not be completed.");

        AddDialogButton(
            "Question",
            HiveMessageType.Question,
            "Continue with this operation?",
            MessageBoxButtons.YesNo);

        var details = CreateButton(
            "Error with technical details",
            HiveButtonStyle.Secondary,
            240);

        details.Click += (_, _) =>
            HiveMessageBox.Show(
                this,
                new HiveMessageOptions(
                    "Operation failed",
                    "The operation could not be completed.",
                    HiveMessageType.Error,
                    MessageBoxButtons.OK,
                    "Example technical details\r\nCode: UI-0001\r\nPath: Hive.Example.WinForms",
                    DetailsExpanded: true),
                _themeManager);

        _buttons.Controls.Add(details);

        Controls.Add(_buttons);
        Controls.Add(_description);

        _themeManager.Apply(this);
    }

    private void AddDialogButton(
        string text,
        HiveMessageType type,
        string message,
        MessageBoxButtons buttons = MessageBoxButtons.OK)
    {
        var button = CreateButton(text, HiveButtonStyle.Secondary, 200);

        button.TabIndex = _buttons.Controls.Count;

        button.Click += (_, _) =>
            HiveMessageBox.Show(
                this,
                new HiveMessageOptions(
                    text,
                    message,
                    type,
                    buttons),
                _themeManager);

        _buttons.Controls.Add(button);
    }

    private static HiveButton CreateButton(
        string text,
        HiveButtonStyle style,
        int width) =>
        new()
        {
            Text = text,
            Style = style,
            Width = width,
            Height = 38,
            Margin = new Padding(0, 0, 10, 10)
        };
}
