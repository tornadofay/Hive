using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public static class HiveMessageBox
{
    public static DialogResult Show(
        IWin32Window? owner,
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        IHiveThemeManager? themeManager = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        var manager = themeManager ?? new HiveThemeManager();
        using var dialog = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = owner is null
                ? FormStartPosition.CenterScreen
                : FormStartPosition.CenterParent,
            ClientSize = new Size(520, 210),
            Padding = new Padding(manager.Theme.Spacing.Lg)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var messagePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0),
            AutoScroll = true
        };

        if (icon != MessageBoxIcon.None)
        {
            var iconBox = new PictureBox
            {
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = GetIcon(icon).ToBitmap(),
                Margin = new Padding(0, 2, manager.Theme.Spacing.Md, 0)
            };
            messagePanel.Controls.Add(iconBox);
        }

        var messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 120),
            Text = message,
            Margin = new Padding(0, 2, 0, 0)
        };
        messagePanel.Controls.Add(messageLabel);

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Dock = DockStyle.Right
        };

        foreach (var option in CreateButtons(buttons))
        {
            var button = new HiveButton
            {
                Text = option.Text,
                Width = 96,
                Height = 36,
                Margin = new Padding(manager.Theme.Spacing.Sm, manager.Theme.Spacing.Sm, 0, 0)
            };

            button.Click += (_, _) =>
            {
                dialog.DialogResult = option.Result;
                dialog.Close();
            };

            buttonsPanel.Controls.Add(button);
        }

        root.Controls.Add(messagePanel, 0, 0);
        root.Controls.Add(buttonsPanel, 0, 1);
        dialog.Controls.Add(root);

        manager.Apply(dialog);

        return owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);
    }

    public static DialogResult Show(
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        IHiveThemeManager? themeManager = null) =>
        Show(null, message, title, buttons, icon, themeManager);

    private static Icon GetIcon(MessageBoxIcon icon) =>
        icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Question => SystemIcons.Question,
            MessageBoxIcon.Information => SystemIcons.Information,
            _ => SystemIcons.Information
        };

    private static IReadOnlyList<MessageButtonOption> CreateButtons(
        MessageBoxButtons buttons) =>
        buttons switch
        {
            MessageBoxButtons.OK => [new("OK", DialogResult.OK)],
            MessageBoxButtons.OKCancel =>
            [
                new("Cancel", DialogResult.Cancel),
                new("OK", DialogResult.OK)
            ],
            MessageBoxButtons.YesNo =>
            [
                new("No", DialogResult.No),
                new("Yes", DialogResult.Yes)
            ],
            MessageBoxButtons.YesNoCancel =>
            [
                new("Cancel", DialogResult.Cancel),
                new("No", DialogResult.No),
                new("Yes", DialogResult.Yes)
            ],
            MessageBoxButtons.RetryCancel =>
            [
                new("Cancel", DialogResult.Cancel),
                new("Retry", DialogResult.Retry)
            ],
            MessageBoxButtons.AbortRetryIgnore =>
            [
                new("Ignore", DialogResult.Ignore),
                new("Retry", DialogResult.Retry),
                new("Abort", DialogResult.Abort)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(buttons))
        };

    private sealed record MessageButtonOption(string Text, DialogResult Result);
}
