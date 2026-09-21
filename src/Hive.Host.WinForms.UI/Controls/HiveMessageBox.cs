using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
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


    private static GraphicsPath CreateRoundedRectanglePath(
        RectangleF rectangle,
        float radius)
    {
        var diameter = Math.Min(
            radius * 2f,
            Math.Min(rectangle.Width, rectangle.Height));

        if (diameter <= 0f)
            return new GraphicsPath();

        var path = new GraphicsPath();
        var arc = new RectangleF(
            rectangle.Left,
            rectangle.Top,
            diameter,
            diameter);

        path.AddArc(arc, 180f, 90f);

        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270f, 90f);

        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0f, 90f);

        arc.X = rectangle.Left;
        path.AddArc(arc, 90f, 90f);

        path.CloseFigure();
        return path;
    }

    private sealed class HiveMessageDialog : Form
    {
        private const int DesignWidth = 560;
        private const int MinWidth = 460;
        private const int MinHeight = 260;
        private const int MaxHeight = 720;

        private const int OuterPadding = 24;
        private const int IconColumnWidth = 82;
        private const int IconSize = 56;
        private const int AccentBarHeight = 5;
        private const int FooterHeight = 68;
        private const int DetailsHeight = 156;
        private const int MaxMessageHeight = 260;

        private readonly HiveMessageOptions _options;
        private readonly IHiveThemeManager _themeManager;
        private readonly bool _subscribedToTheme;

        private readonly TableLayoutPanel _root;
        private readonly Panel _accentBar;
        private readonly Panel _contentPanel;
        private readonly TableLayoutPanel _contentLayout;
        private readonly TableLayoutPanel _messageLayout;
        private readonly TableLayoutPanel _textLayout;
        private readonly HiveMessageIcon _icon;
        private readonly Label _title;
        private readonly Panel _messageViewport;
        private readonly Label _message;
        private readonly LinkLabel _detailsLink;
        private readonly Panel _detailsContainer;
        private readonly TableLayoutPanel _detailsLayout;
        private readonly TextBox _details;
        private readonly HiveMessageButton _copyButton;
        private readonly FlowLayoutPanel _footer;
        private readonly HiveMessageButton _primaryButton;
        private readonly HiveMessageButton _secondaryButton;
        private readonly HiveMessageButton _tertiaryButton;

        private readonly Font _titleFont;
        private readonly Font _messageFont;
        private readonly Font _detailsFont;
        private readonly Font _buttonFont;

        private HiveThemeDefinition _theme;
        private GraphicsPath? _windowPath;
        private GraphicsPath? _borderPath;
        private Pen? _borderPen;
        private bool _updatingSize;

        public HiveMessageDialog(
            HiveMessageOptions options,
            IHiveThemeManager? themeManager)
        {
            _options = options;
            _themeManager = themeManager ?? new HiveThemeManager();
            _theme = _themeManager.Theme;

            if (themeManager is not null)
            {
                _themeManager.ThemeChanged += ThemeManagerOnChanged;
                _subscribedToTheme = true;
            }

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(MinWidth, MinHeight);
            ClientSize = new Size(DesignWidth, 320);
            BackColor = _theme.Palette.Surface;
            ForeColor = _theme.Palette.Text;
            AccessibleRole = AccessibleRole.Dialog;
            KeyPreview = true;

            _titleFont = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
            _messageFont = new Font("Segoe UI", 11.25f);
            _detailsFont = new Font("Consolas", 9.5f);
            _buttonFont = new Font("Segoe UI Semibold", 10.25f, FontStyle.Bold);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = _theme.Palette.Surface
            };
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, AccentBarHeight));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

            _accentBar = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Margin = Padding.Empty,
                Padding = new Padding(
                    OuterPadding,
                    OuterPadding,
                    OuterPadding,
                    8)
            };

            _contentLayout = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _contentLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            _messageLayout = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _messageLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, IconColumnWidth));
            _messageLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            _icon = new HiveMessageIcon
            {
                Size = new Size(IconSize, IconSize),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 18, 0),
                MessageType = options.Type
            };

            _textLayout = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _textLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            _title = new Label
            {
                AutoSize = true,
                Font = _titleFont,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
                Text = options.Title,
                UseMnemonic = false
            };

            _messageViewport = new Panel
            {
                Dock = DockStyle.Top,
                AutoScroll = true,
                Margin = new Padding(0, 10, 0, 0),
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };

            _message = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(420, MaxMessageHeight),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
                Text = options.Message,
                UseMnemonic = false
            };
            _messageViewport.Controls.Add(_message);

            _textLayout.Controls.Add(_title, 0, 0);
            _textLayout.Controls.Add(_messageViewport, 0, 1);

            _messageLayout.Controls.Add(_icon, 0, 0);
            _messageLayout.Controls.Add(_textLayout, 1, 0);

            _detailsLink = new LinkLabel
            {
                AutoSize = true,
                Visible = !string.IsNullOrWhiteSpace(options.Details),
                Text = options.DetailsExpanded
                    ? "Hide details  ▲"
                    : "Show details  ▼",
                Margin = new Padding(0, 14, 0, 8),
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                TabStop = true
            };
            _detailsLink.LinkClicked += (_, _) => ToggleDetails();

            _detailsContainer = new Panel
            {
                Visible = options.DetailsExpanded && !string.IsNullOrWhiteSpace(options.Details),
                Height = DetailsHeight,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(12)
            };

            _detailsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _detailsLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));
            _detailsLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 112));

            _details = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Text = options.Details ?? string.Empty,
                Font = _detailsFont,
                Margin = Padding.Empty
            };

            _copyButton = new HiveMessageButton
            {
                Text = "Copy details",
                Width = 108,
                Height = 36,
                Kind = HiveMessageButtonKind.Secondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(6, 0, 0, 0)
            };
            _copyButton.Click += (_, _) => CopyDetails();

            _detailsLayout.Controls.Add(_details, 0, 0);
            _detailsLayout.Controls.Add(_copyButton, 1, 0);
            _detailsContainer.Controls.Add(_detailsLayout);

            _footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = new Padding(12, 10, 24, 12)
            };

            _primaryButton = CreateActionButton();
            _secondaryButton = CreateActionButton();
            _tertiaryButton = CreateActionButton();

            _footer.Controls.Add(_primaryButton);
            _footer.Controls.Add(_secondaryButton);
            _footer.Controls.Add(_tertiaryButton);

            _root.Controls.Add(_accentBar, 0, 0);
            _root.Controls.Add(_contentPanel, 0, 1);
            _root.Controls.Add(_footer, 0, 2);

            _contentLayout.Controls.Add(_messageLayout, 0, 0);
            _contentLayout.Controls.Add(_detailsLink, 0, 1);
            _contentLayout.Controls.Add(_detailsContainer, 0, 2);

            _contentPanel.Controls.Add(_contentLayout);
            Controls.Add(_root);

            ApplyButtons();
            ApplyTheme();
            UpdateDialogSize();

            AcceptButton = _primaryButton;
            CancelButton = ResolveCancelButton();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_subscribedToTheme)
                    _themeManager.ThemeChanged -= ThemeManagerOnChanged;

                _windowPath?.Dispose();
                _borderPath?.Dispose();
                _borderPen?.Dispose();
                _titleFont.Dispose();
                _messageFont.Dispose();
                _detailsFont.Dispose();
                _buttonFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_borderPath is null || _borderPen is null)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawPath(_borderPen, _borderPath);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateWindowRegion();
        }

        private void ThemeManagerOnChanged(object? sender, EventArgs e)
        {
            _theme = _themeManager.Theme;
            ApplyTheme();
            UpdateDialogSize();
            Invalidate();
        }

        private void ApplyTheme()
        {
            BackColor = _theme.Palette.Surface;
            ForeColor = _theme.Palette.Text;

            _root.BackColor = _theme.Palette.Surface;
            _contentPanel.BackColor = _theme.Palette.Surface;
            _contentLayout.BackColor = _theme.Palette.Surface;
            _messageLayout.BackColor = _theme.Palette.Surface;
            _textLayout.BackColor = _theme.Palette.Surface;
            _accentBar.BackColor = GetAccentColor(_theme, _options.Type);

            _title.ForeColor = _theme.Palette.Text;
            _message.ForeColor = _theme.Palette.MutedText;
            _detailsLink.LinkColor = _theme.Palette.Accent;
            _detailsLink.ActiveLinkColor = _theme.Palette.AccentHover;

            _detailsContainer.BackColor = _theme.Palette.ElevatedSurface;
            _details.BackColor = _theme.Palette.InputBackground;
            _details.ForeColor = _theme.Palette.Text;
            _details.Font = _detailsFont;

            _borderPen?.Dispose();
            _borderPen = new Pen(_theme.Palette.Border, 1.2f);

            _icon.ApplyTheme(
                _theme,
                GetAccentColor(_theme, _options.Type));

            _primaryButton.ApplyTheme(_theme, HiveMessageButtonKind.Primary);
            _secondaryButton.ApplyTheme(_theme, HiveMessageButtonKind.Secondary);
            _tertiaryButton.ApplyTheme(_theme, HiveMessageButtonKind.Secondary);
            _copyButton.ApplyTheme(_theme, HiveMessageButtonKind.Secondary);
        }

        private void ApplyButtons()
        {
            _primaryButton.Visible = false;
            _secondaryButton.Visible = false;
            _tertiaryButton.Visible = false;

            switch (_options.Buttons)
            {
                case MessageBoxButtons.OK:
                    PrepareButton(
                        _primaryButton,
                        "OK",
                        DialogResult.OK);
                    break;

                case MessageBoxButtons.OKCancel:
                    PrepareButton(
                        _primaryButton,
                        "OK",
                        DialogResult.OK);
                    PrepareButton(
                        _secondaryButton,
                        "Cancel",
                        DialogResult.Cancel);
                    break;

                case MessageBoxButtons.YesNo:
                    PrepareButton(
                        _primaryButton,
                        "Yes",
                        DialogResult.Yes);
                    PrepareButton(
                        _secondaryButton,
                        "No",
                        DialogResult.No);
                    break;

                case MessageBoxButtons.YesNoCancel:
                    PrepareButton(
                        _primaryButton,
                        "Yes",
                        DialogResult.Yes);
                    PrepareButton(
                        _secondaryButton,
                        "No",
                        DialogResult.No);
                    PrepareButton(
                        _tertiaryButton,
                        "Cancel",
                        DialogResult.Cancel);
                    break;

                case MessageBoxButtons.RetryCancel:
                    PrepareButton(
                        _primaryButton,
                        "Retry",
                        DialogResult.Retry);
                    PrepareButton(
                        _secondaryButton,
                        "Cancel",
                        DialogResult.Cancel);
                    break;

                case MessageBoxButtons.AbortRetryIgnore:
                    PrepareButton(
                        _primaryButton,
                        "Retry",
                        DialogResult.Retry);
                    PrepareButton(
                        _secondaryButton,
                        "Ignore",
                        DialogResult.Ignore);
                    PrepareButton(
                        _tertiaryButton,
                        "Abort",
                        DialogResult.Abort);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(_options.Buttons),
                        _options.Buttons,
                        "Unsupported message-box button set.");
            }
        }

        private void PrepareButton(
            HiveMessageButton button,
            string text,
            DialogResult result)
        {
            button.Text = text;
            button.DialogResult = result;
            button.Visible = true;
            button.Width = MeasureButtonWidth(text);
        }

        private int MeasureButtonWidth(string text)
        {
            var measured = TextRenderer.MeasureText(
                text,
                _buttonFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            return Math.Max(112, Math.Min(190, measured.Width + 40));
        }

        private HiveMessageButton? ResolveCancelButton()
        {
            return _options.Buttons switch
            {
                MessageBoxButtons.OKCancel => _secondaryButton,
                MessageBoxButtons.YesNoCancel => _tertiaryButton,
                MessageBoxButtons.RetryCancel => _secondaryButton,
                MessageBoxButtons.YesNo => _secondaryButton,
                _ => null
            };
        }

        private void ToggleDetails()
        {
            _detailsContainer.Visible = !_detailsContainer.Visible;
            _detailsLink.Text = _detailsContainer.Visible
                ? "Hide details  ▲"
                : "Show details  ▼";

            UpdateDialogSize();
        }

        private void CopyDetails()
        {
            if (string.IsNullOrEmpty(_details.Text))
                return;

            try
            {
                Clipboard.SetText(_details.Text);
            }
            catch (ExternalException)
            {
                // Clipboard ownership can temporarily prevent writes.
            }
        }

        private void UpdateDialogSize()
        {
            if (_updatingSize)
                return;

            _updatingSize = true;
            try
            {
                UpdateDialogSizeCore();
            }
            finally
            {
                _updatingSize = false;
            }
        }

        private void UpdateDialogSizeCore()
        {
            var width = Math.Max(MinWidth, Math.Min(DesignWidth, Width));
            var contentWidth =
                width -
                (OuterPadding * 2) -
                IconColumnWidth;

            contentWidth = Math.Max(260, contentWidth);

            _message.MaximumSize = new Size(contentWidth, MaxMessageHeight);

            var messageMeasured = TextRenderer.MeasureText(
                _message.Text,
                _messageFont,
                new Size(contentWidth, MaxMessageHeight),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

            _messageViewport.Height = Math.Max(
                24,
                Math.Min(MaxMessageHeight, messageMeasured.Height));

            var messageHeight = Math.Max(
                IconSize,
                _title.PreferredHeight + 10 + _messageViewport.Height);

            var detailsHeight = _detailsContainer.Visible
                ? DetailsHeight
                : 0;

            var detailsLinkHeight = _detailsLink.Visible
                ? 34
                : 0;

            var requestedHeight =
                AccentBarHeight +
                OuterPadding +
                messageHeight +
                detailsLinkHeight +
                detailsHeight +
                8 +
                FooterHeight;

            var maxAllowed = Math.Min(
                MaxHeight,
                Screen.FromControl(this).WorkingArea.Height - 40);

            ClientSize = new Size(
                width,
                Math.Max(MinHeight, Math.Min(maxAllowed, requestedHeight)));
        }

        private void UpdateWindowRegion()
        {
            if (Width <= 1 || Height <= 1)
            {
                Region = null;
                return;
            }

            var newPath = CreateRoundedRectanglePath(
                new RectangleF(0, 0, Width, Height),
                12f);

            var newBorderPath = CreateRoundedRectanglePath(
                new RectangleF(0.6f, 0.6f, Width - 1.2f, Height - 1.2f),
                11.4f);

            _windowPath?.Dispose();
            _windowPath = newPath;

            _borderPath?.Dispose();
            _borderPath = newBorderPath;

            var oldRegion = Region;
            Region = new Region(newPath);
            oldRegion?.Dispose();

            Invalidate();
        }

        private HiveMessageButton CreateActionButton() =>
            new()
            {
                Height = 40,
                Font = _buttonFont,
                Margin = new Padding(8, 0, 0, 0)
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

    }

    private enum HiveMessageButtonKind
    {
        Primary,
        Secondary
    }

    private sealed class HiveMessageButton : Button
    {
        private bool _hovered;
        private bool _pressed;
        private HiveMessageButtonKind _kind = HiveMessageButtonKind.Secondary;

        private HiveThemeDefinition? _theme;
        private GraphicsPath? _path;
        private SolidBrush? _backgroundBrush;
        private SolidBrush? _hoverBrush;
        private SolidBrush? _pressedBrush;
        private SolidBrush? _textBrush;
        private SolidBrush? _disabledBrush;
        private Pen? _borderPen;
        private Pen? _focusPen;

        public HiveMessageButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = true;
            Cursor = Cursors.Hand;
            TextAlign = ContentAlignment.MiddleCenter;
            AccessibleRole = AccessibleRole.PushButton;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HiveMessageButtonKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public void ApplyTheme(
            HiveThemeDefinition theme,
            HiveMessageButtonKind kind)
        {
            _theme = theme;
            _kind = kind;

            DisposeResources();

            var palette = theme.Palette;

            if (kind == HiveMessageButtonKind.Primary)
            {
                _backgroundBrush = new SolidBrush(palette.Accent);
                _hoverBrush = new SolidBrush(palette.AccentHover);
                _pressedBrush = new SolidBrush(palette.Border);
                _textBrush = new SolidBrush(palette.AccentForeground);
                _borderPen = new Pen(palette.Accent, 1f);
            }
            else
            {
                _backgroundBrush = new SolidBrush(palette.ElevatedSurface);
                _hoverBrush = new SolidBrush(theme.VisualStates.HoverBackground);
                _pressedBrush = new SolidBrush(theme.VisualStates.PressedBackground);
                _textBrush = new SolidBrush(palette.Text);
                _borderPen = new Pen(palette.Border, 1f);
            }

            _disabledBrush = new SolidBrush(palette.DisabledBackground);
            _focusPen = new Pen(palette.Accent, 2f);

            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            _path?.Dispose();
            _path = Width > 1 && Height > 1
                ? CreateRoundedRectanglePath(
                    new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f),
                    8f)
                : null;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_path is null ||
                _backgroundBrush is null ||
                _hoverBrush is null ||
                _pressedBrush is null ||
                _textBrush is null ||
                _disabledBrush is null)
                return;

            var brush = !Enabled
                ? _disabledBrush
                : _pressed
                    ? _pressedBrush
                    : _hovered
                        ? _hoverBrush
                        : _backgroundBrush;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, _path);

            if (_borderPen is not null)
                e.Graphics.DrawPath(_borderPen, _path);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                Enabled ? _textBrush.Color : _theme?.Palette.DisabledText ?? SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            if (Focused && _focusPen is not null)
            {
                using var focusPath = CreateRoundedRectanglePath(
                    new RectangleF(3f, 3f, Width - 7f, Height - 7f),
                    6f);
                e.Graphics.DrawPath(_focusPen, focusPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _path?.Dispose();
                DisposeResources();
            }

            base.Dispose(disposing);
        }

        private void DisposeResources()
        {
            _backgroundBrush?.Dispose();
            _hoverBrush?.Dispose();
            _pressedBrush?.Dispose();
            _textBrush?.Dispose();
            _disabledBrush?.Dispose();
            _borderPen?.Dispose();
            _focusPen?.Dispose();

            _backgroundBrush = null;
            _hoverBrush = null;
            _pressedBrush = null;
            _textBrush = null;
            _disabledBrush = null;
            _borderPen = null;
            _focusPen = null;
        }
    }

    private sealed class HiveMessageIcon : Control
    {
        private HiveMessageType _messageType;
        private Color _accent;
        private Color _surface;

        private SolidBrush? _backgroundBrush;
        private SolidBrush? _accentBrush;
        private SolidBrush? _surfaceBrush;
        private Pen? _ringPen;
        private Pen? _glyphPen;
        private Font? _questionFont;
        private GraphicsPath? _warningPath;

        public HiveMessageIcon()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            BackColor = Color.Transparent;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HiveMessageType MessageType
        {
            get => _messageType;
            set
            {
                if (_messageType == value)
                    return;

                _messageType = value;
                Invalidate();
            }
        }

        public void ApplyTheme(
            HiveThemeDefinition theme,
            Color accent)
        {
            _surface = theme.Palette.Surface;
            _accent = accent;

            _backgroundBrush?.Dispose();
            _accentBrush?.Dispose();
            _surfaceBrush?.Dispose();
            _ringPen?.Dispose();
            _glyphPen?.Dispose();
            _questionFont?.Dispose();
            _warningPath?.Dispose();

            _backgroundBrush = new SolidBrush(
                Color.FromArgb(
                    34,
                    Blend(_surface, _accent, 0.72f)));

            _accentBrush = new SolidBrush(_accent);
            _surfaceBrush = new SolidBrush(_surface);
            _ringPen = new Pen(Color.FromArgb(88, _accent), 1.5f)
            {
                Alignment = PenAlignment.Inset
            };
            _glyphPen = new Pen(_accent, 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            _questionFont = new Font(
                "Segoe UI",
                25f,
                FontStyle.Bold);

            RebuildGeometry();
            Invalidate();
        }

        private void RebuildGeometry()
        {
            _warningPath?.Dispose();
            _warningPath = null;

            var size = Math.Min(ClientSize.Width, ClientSize.Height);
            if (size <= 2)
                return;

            var circleLeft = (ClientSize.Width - size) / 2f;
            var circleTop = (ClientSize.Height - size) / 2f;

            _warningPath = new GraphicsPath();
            _warningPath.AddPolygon(
                new[]
                {
                    new PointF(
                        circleLeft + size * 0.50f,
                        circleTop + size * 0.19f),
                    new PointF(
                        circleLeft + size * 0.77f,
                        circleTop + size * 0.78f),
                    new PointF(
                        circleLeft + size * 0.23f,
                        circleTop + size * 0.78f)
                });
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (_accent != Color.Empty && _surface != Color.Empty)
                RebuildGeometry();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_backgroundBrush is null ||
                _accentBrush is null ||
                _ringPen is null ||
                _glyphPen is null ||
                _surfaceBrush is null)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var size = Math.Min(ClientSize.Width, ClientSize.Height);
            if (size <= 2)
                return;

            var circle = new Rectangle(
                (ClientSize.Width - size) / 2,
                (ClientSize.Height - size) / 2,
                size - 1,
                size - 1);

            e.Graphics.FillEllipse(_backgroundBrush, circle);
            e.Graphics.DrawEllipse(
                _ringPen,
                circle.X + 1,
                circle.Y + 1,
                circle.Width - 2,
                circle.Height - 2);

            var centerX = circle.Left + circle.Width / 2f;
            switch (_messageType)
            {
                case HiveMessageType.Information:
                    e.Graphics.FillEllipse(
                        _accentBrush,
                        centerX - 3,
                        circle.Top + circle.Height * 0.20f,
                        6,
                        6);
                    e.Graphics.FillRectangle(
                        _accentBrush,
                        centerX - 3,
                        circle.Top + circle.Height * 0.36f,
                        6,
                        circle.Height * 0.40f);
                    break;

                case HiveMessageType.Success:
                    var p1 = new PointF(
                        circle.Left + circle.Width * 0.26f,
                        circle.Top + circle.Height * 0.53f);
                    var p2 = new PointF(
                        circle.Left + circle.Width * 0.44f,
                        circle.Top + circle.Height * 0.70f);
                    var p3 = new PointF(
                        circle.Left + circle.Width * 0.76f,
                        circle.Top + circle.Height * 0.32f);

                    e.Graphics.DrawLine(_glyphPen, p1, p2);
                    e.Graphics.DrawLine(_glyphPen, p2, p3);
                    break;

                case HiveMessageType.Warning:
                    if (_warningPath is not null)
                    {
                        e.Graphics.FillPath(_accentBrush, _warningPath);

                        e.Graphics.FillRectangle(
                            _surfaceBrush,
                            centerX - 2,
                            circle.Top + circle.Height * 0.38f,
                            4,
                            circle.Height * 0.22f);
                        e.Graphics.FillEllipse(
                            _surfaceBrush,
                            centerX - 2,
                            circle.Top + circle.Height * 0.66f,
                            4,
                            4);
                    }
                    break;

                case HiveMessageType.Error:
                    var left = circle.Left + circle.Width * 0.31f;
                    var right = circle.Left + circle.Width * 0.69f;
                    var top = circle.Top + circle.Height * 0.31f;
                    var bottom = circle.Top + circle.Height * 0.69f;
                    e.Graphics.DrawLine(_glyphPen, left, top, right, bottom);
                    e.Graphics.DrawLine(_glyphPen, right, top, left, bottom);
                    break;

                case HiveMessageType.Question:
                    TextRenderer.DrawText(
                        e.Graphics,
                        "?",
                        _questionFont ?? Font,
                        circle,
                        _accent,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPrefix);
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _backgroundBrush?.Dispose();
                _accentBrush?.Dispose();
                _ringPen?.Dispose();
                _glyphPen?.Dispose();
                _questionFont?.Dispose();
                _warningPath?.Dispose();
                _surfaceBrush?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Color Blend(Color first, Color second, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);

            return Color.FromArgb(
                (int)(first.R + ((second.R - first.R) * amount)),
                (int)(first.G + ((second.G - first.G) * amount)),
                (int)(first.B + ((second.B - first.B) * amount)));
        }
    }
}
