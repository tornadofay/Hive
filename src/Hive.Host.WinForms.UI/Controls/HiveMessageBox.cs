using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public enum HiveMessageType
{
    Information,
    Success,
    Warning,
    Error,
    Question
}

public sealed record HiveMessageOptions(
    string Title,
    string Message,
    HiveMessageType Type = HiveMessageType.Information,
    MessageBoxButtons Buttons = MessageBoxButtons.OK,
    string? Details = null,
    bool DetailsExpanded = false);

public static class HiveMessageBox
{
    public static DialogResult Show(
        IWin32Window? owner,
        HiveMessageOptions options,
        IHiveThemeManager? themeManager = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Title))
            throw new ArgumentException("Message title is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.Message))
            throw new ArgumentException("Message text is required.", nameof(options));

        using var dialog = new HiveMessageDialog(options, themeManager);
        return owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);
    }

    public static DialogResult Show(
        string message,
        string title,
        HiveMessageType type = HiveMessageType.Information,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        IHiveThemeManager? themeManager = null) =>
        Show(
            null,
            new HiveMessageOptions(title, message, type, buttons),
            themeManager);

    public static DialogResult Show(
        IWin32Window? owner,
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        IHiveThemeManager? themeManager = null) =>
        Show(
            owner,
            new HiveMessageOptions(
                title,
                message,
                ToMessageType(icon),
                buttons),
            themeManager);

    public static DialogResult Show(
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        IHiveThemeManager? themeManager = null) =>
        Show(null, message, title, buttons, icon, themeManager);

    public static DialogResult ShowInformation(
        IWin32Window? owner,
        string message,
        string title = "Information",
        IHiveThemeManager? themeManager = null) =>
        Show(
            owner,
            new HiveMessageOptions(title, message, HiveMessageType.Information),
            themeManager);

    public static DialogResult ShowSuccess(
        IWin32Window? owner,
        string message,
        string title = "Success",
        IHiveThemeManager? themeManager = null) =>
        Show(
            owner,
            new HiveMessageOptions(title, message, HiveMessageType.Success),
            themeManager);

    public static DialogResult ShowWarning(
        IWin32Window? owner,
        string message,
        string title = "Warning",
        IHiveThemeManager? themeManager = null) =>
        Show(
            owner,
            new HiveMessageOptions(title, message, HiveMessageType.Warning),
            themeManager);

    public static DialogResult ShowError(
        IWin32Window? owner,
        string message,
        string title = "Error",
        IHiveThemeManager? themeManager = null) =>
        Show(
            owner,
            new HiveMessageOptions(title, message, HiveMessageType.Error),
            themeManager);

    public static DialogResult ShowQuestion(
        IWin32Window? owner,
        string message,
        string title = "Question",
        MessageBoxButtons buttons = MessageBoxButtons.YesNo,
        IHiveThemeManager? themeManager = null) =>
        Show(
            owner,
            new HiveMessageOptions(title, message, HiveMessageType.Question, buttons),
            themeManager);

    private static HiveMessageType ToMessageType(MessageBoxIcon icon) =>
        icon switch
        {
            MessageBoxIcon.Error => HiveMessageType.Error,
            MessageBoxIcon.Warning => HiveMessageType.Warning,
            MessageBoxIcon.Question => HiveMessageType.Question,
            _ => HiveMessageType.Information
        };

    private sealed class HiveMessageDialog : HiveForm
    {
        private readonly HiveMessageOptions _options;
        private readonly HiveThemeDefinition _theme;
        private readonly Label _icon;
        private readonly Label _message;
        private readonly Font _iconFont;
        private readonly LinkLabel _detailsLink;
        private readonly TextBox _details;
        private readonly Label _copyStatus;
        private readonly FlowLayoutPanel _buttons;

        public HiveMessageDialog(
            HiveMessageOptions options,
            IHiveThemeManager? themeManager)
            : base(
                options.Title,
                GetSubtitle(options.Type),
                new Size(560, 360),
                new Size(460, 260),
                themeManager)
        {
            _options = options;
            _theme = Theme;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            SetBodyPadding(new Padding(_theme.Spacing.Lg));

            ConfigureHeader(
                allowMove: true,
                allowClose: true,
                allowMinimize: false,
                allowHelp: false);

            _iconFont = new Font(
                _theme.Typography.FontFamily,
                16f,
                FontStyle.Bold);
            _icon = CreateIcon();
            _message = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(420, 180),
                Font = new Font(
                    _theme.Typography.FontFamily,
                    10f),
                ForeColor = _theme.Palette.Text,
                BackColor = Color.Transparent,
                Text = options.Message,
                Margin = new Padding(0, 4, 0, 0)
            };

            _detailsLink = new LinkLabel
            {
                AutoSize = true,
                Text = string.IsNullOrWhiteSpace(options.Details)
                    ? string.Empty
                    : "Show details",
                Visible = !string.IsNullOrWhiteSpace(options.Details),
                LinkColor = _theme.VisualStates.Information,
                ActiveLinkColor = _theme.VisualStates.Information,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 10, 0, 0)
            };
            _detailsLink.LinkClicked += (_, _) => ToggleDetails();

            _details = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = options.Details ?? string.Empty,
                Dock = DockStyle.Top,
                Height = 120,
                Visible = options.DetailsExpanded,
                Font = new Font("Consolas", 8.5f),
                BorderStyle = BorderStyle.FixedSingle
            };

            _copyStatus = new Label
            {
                AutoSize = true,
                Text = string.Empty,
                ForeColor = _theme.Palette.MutedText,
                BackColor = Color.Transparent,
                Margin = new Padding(_theme.Spacing.Sm, 8, 0, 0)
            };

            if (options.DetailsExpanded && !string.IsNullOrWhiteSpace(options.Details))
                _detailsLink.Text = "Hide details";

            _buttons = CreateButtons(options.Buttons);
            BuildBody();
            ApplyThemeToDialog();

            KeyPreview = true;
            KeyDown += DialogOnKeyDown;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _message.Font.Dispose();
                _details.Font.Dispose();
                _iconFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void BuildBody()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var messageRow = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            messageRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64f));
            messageRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            messageRow.Controls.Add(_icon, 0, 0);
            messageRow.Controls.Add(_message, 1, 0);

            root.Controls.Add(messageRow, 0, 0);
            root.Controls.Add(_detailsLink, 0, 1);
            root.Controls.Add(_details, 0, 2);
            root.Controls.Add(_copyStatus, 0, 3);

            var footer = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Bottom,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, _theme.Spacing.Lg, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var copyButton = new HiveButton
            {
                Text = "Copy",
                Style = HiveButtonStyle.Secondary,
                Width = 88,
                Height = 36,
                Visible = !string.IsNullOrWhiteSpace(_options.Details),
                Margin = new Padding(0, 0, _theme.Spacing.Sm, 0)
            };
            copyButton.Click += (_, _) => CopyDetails();

            footer.Controls.Add(copyButton, 0, 0);
            footer.Controls.Add(_buttons, 1, 0);

            BodyPanel.Controls.Add(root);
            BodyPanel.Controls.Add(footer);
        }

        private Label CreateIcon()
        {
            var accent = GetAccentColor(Theme, _options.Type);
            var icon = new Label
            {
                AutoSize = false,
                Size = new Size(48, 48),
                Text = GetIconGlyph(_options.Type),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = _iconFont,
                ForeColor = Color.White,
                BackColor = accent,
                Margin = new Padding(0, 2, Theme.Spacing.Md, 0)
            };

            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(new Rectangle(0, 0, 48, 48));
            icon.Region = new Region(path);
            return icon;
        }

        private FlowLayoutPanel CreateButtons(MessageBoxButtons buttons)
        {
            var specifications = buttons switch
            {
                MessageBoxButtons.OK =>
                    new[] { new MessageButtonSpec("OK", DialogResult.OK, true) },

                MessageBoxButtons.OKCancel =>
                    new[]
                    {
                        new MessageButtonSpec("Cancel", DialogResult.Cancel, false),
                        new MessageButtonSpec("OK", DialogResult.OK, true)
                    },

                MessageBoxButtons.YesNo =>
                    new[]
                    {
                        new MessageButtonSpec("No", DialogResult.No, false),
                        new MessageButtonSpec("Yes", DialogResult.Yes, true)
                    },

                MessageBoxButtons.YesNoCancel =>
                    new[]
                    {
                        new MessageButtonSpec("Cancel", DialogResult.Cancel, false),
                        new MessageButtonSpec("No", DialogResult.No, false),
                        new MessageButtonSpec("Yes", DialogResult.Yes, true)
                    },

                MessageBoxButtons.RetryCancel =>
                    new[]
                    {
                        new MessageButtonSpec("Cancel", DialogResult.Cancel, false),
                        new MessageButtonSpec("Retry", DialogResult.Retry, true)
                    },

                MessageBoxButtons.AbortRetryIgnore =>
                    new[]
                    {
                        new MessageButtonSpec("Ignore", DialogResult.Ignore, false),
                        new MessageButtonSpec("Retry", DialogResult.Retry, true),
                        new MessageButtonSpec("Abort", DialogResult.Abort, false)
                    },

                _ => throw new ArgumentOutOfRangeException(nameof(buttons), buttons, "Unsupported message-box button set.")
            };

            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            foreach (var specification in specifications)
            {
                var button = new HiveButton
                {
                    Text = specification.Text,
                    Style = specification.Primary
                        ? HiveButtonStyle.Primary
                        : HiveButtonStyle.Secondary,
                    Width = 96,
                    Height = 36,
                    Margin = new Padding(_theme.Spacing.Sm, 0, 0, 0)
                };

                button.Click += (_, _) =>
                {
                    DialogResult = specification.Result;
                    Close();
                };

                panel.Controls.Add(button);
            }

            return panel;
        }

        private void ToggleDetails()
        {
            _details.Visible = !_details.Visible;
            _detailsLink.Text = _details.Visible
                ? "Hide details"
                : "Show details";
        }

        private void CopyDetails()
        {
            if (string.IsNullOrEmpty(_details.Text))
                return;

            try
            {
                Clipboard.SetText(_details.Text);
                _copyStatus.Text = "Details copied.";
            }
            catch (ExternalException)
            {
                _copyStatus.Text = "Clipboard is unavailable.";
            }
        }

        private void DialogOnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = HasCancelButton(_options.Buttons)
                    ? DialogResult.Cancel
                    : DialogResult.None;
                e.Handled = true;
                Close();
                return;
            }

            if (e.KeyCode != Keys.Enter)
                return;

            var defaultResult = GetDefaultResult(_options.Buttons);
            DialogResult = defaultResult;
            e.Handled = true;
            Close();
        }

        private void ApplyThemeToDialog()
        {
            var theme = Theme;
            _message.ForeColor = theme.Palette.Text;
            _details.BackColor = theme.Palette.InputBackground;
            _details.ForeColor = theme.Palette.Text;
            _detailsLink.LinkColor = theme.Palette.Accent;
            _detailsLink.ActiveLinkColor = theme.Palette.AccentHover;
            _copyStatus.ForeColor = theme.Palette.MutedText;
        }

        private static DialogResult GetDefaultResult(MessageBoxButtons buttons) =>
            buttons switch
            {
                MessageBoxButtons.YesNo or
                MessageBoxButtons.YesNoCancel => DialogResult.Yes,

                _ => DialogResult.OK
            };

        private static bool HasCancelButton(MessageBoxButtons buttons) =>
            buttons is MessageBoxButtons.OKCancel
                or MessageBoxButtons.YesNoCancel
                or MessageBoxButtons.RetryCancel;

        private static string GetSubtitle(HiveMessageType type) =>
            type switch
            {
                HiveMessageType.Information => "Information",
                HiveMessageType.Success => "Success",
                HiveMessageType.Warning => "Warning",
                HiveMessageType.Error => "Error",
                HiveMessageType.Question => "Question",
                _ => "Message"
            };

        private static string GetIconGlyph(HiveMessageType type) =>
            type switch
            {
                HiveMessageType.Information => "i",
                HiveMessageType.Success => "✓",
                HiveMessageType.Warning => "!",
                HiveMessageType.Error => "×",
                HiveMessageType.Question => "?",
                _ => "i"
            };

        private static Color GetAccentColor(
            HiveThemeDefinition theme,
            HiveMessageType type) =>
            type switch
            {
                HiveMessageType.Information => theme.VisualStates.Information,
                HiveMessageType.Success => theme.VisualStates.Success,
                HiveMessageType.Warning => theme.VisualStates.Warning,
                HiveMessageType.Error => theme.VisualStates.Error,
                HiveMessageType.Question => theme.VisualStates.Question,
                _ => theme.Palette.Accent
            };

        private sealed record MessageButtonSpec(
            string Text,
            DialogResult Result,
            bool Primary);
    }
}
