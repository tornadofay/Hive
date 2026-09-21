using Hive.Host.WinForms.UI.Theme;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HivePaginationBar : UserControl
{
    private readonly HiveButton _previousButton;
    private readonly HiveButton _nextButton;
    private readonly Label _pageLabel;
    private int _pageNumber = 1;
    private bool _canGoPrevious;
    private bool _canGoNext;

    public HivePaginationBar()
    {
        Height = 40;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(0, 3, 0, 3);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

        _previousButton = CreateButton("Previous");
        _previousButton.Click += (_, _) =>
        {
            if (!CanGoPrevious)
                return;

            PreviousRequested?.Invoke(this, EventArgs.Empty);
        };

        _nextButton = CreateButton("Next");
        _nextButton.Click += (_, _) =>
        {
            if (!CanGoNext)
                return;

            NextRequested?.Invoke(this, EventArgs.Empty);
        };

        _pageLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.8f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = Padding.Empty
        };

        layout.Controls.Add(_previousButton, 0, 0);
        layout.Controls.Add(_pageLabel, 1, 0);
        layout.Controls.Add(_nextButton, 2, 0);

        Controls.Add(layout);
        UpdateState();
    }

    public event EventHandler? PreviousRequested;

    public event EventHandler? NextRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageNumber
    {
        get => _pageNumber;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_pageNumber == value)
                return;

            _pageNumber = value;
            UpdateState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanGoPrevious
    {
        get => _canGoPrevious;
        set
        {
            if (_canGoPrevious == value)
                return;

            _canGoPrevious = value;
            UpdateState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanGoNext
    {
        get => _canGoNext;
        set
        {
            if (_canGoNext == value)
                return;

            _canGoNext = value;
            UpdateState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PageText
    {
        get => _pageLabel.Text;
        set => _pageLabel.Text = value ?? string.Empty;
    }

    private HiveButton CreateButton(string text) =>
        new()
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        BackColor = theme.Palette.Surface;
        _pageLabel.ForeColor = theme.Palette.MutedText;
        _pageLabel.BackColor = Color.Transparent;
    }

    private void UpdateState()
    {
        _previousButton.Enabled = CanGoPrevious;
        _nextButton.Enabled = CanGoNext;
        _pageLabel.Text = $"Page {_pageNumber}";
    }
}
