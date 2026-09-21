using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HivePaginationBar : UserControl
{
    private readonly HiveButton _previousButton;
    private readonly HiveButton _nextButton;
    private readonly Label _pageLabel;
    private int _pageNumber = 1;

    public HivePaginationBar()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(HiveDpi.DesignDpi, HiveDpi.DesignDpi);
        Height = HiveDpi.Scale(this, 44);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = HiveDpi.Scale(this, new Padding(0, 4, 0, 4));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HiveDpi.Scale(this, 96)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HiveDpi.Scale(this, 96)));

        _previousButton = CreateButton("Previous");
        _previousButton.Click += (_, _) =>
        {
            if (!CanGoPrevious)
                return;

            PageNumber--;
            PreviousRequested?.Invoke(this, EventArgs.Empty);
        };

        _nextButton = CreateButton("Next");
        _nextButton.Click += (_, _) =>
        {
            if (!CanGoNext)
                return;

            PageNumber++;
            NextRequested?.Invoke(this, EventArgs.Empty);
        };

        _pageLabel = new Label
        {
            Dock = DockStyle.Fill,
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

    public bool CanGoPrevious
    {
        get;
        set
        {
            field = value;
            UpdateState();
        }
    }

    public bool CanGoNext
    {
        get;
        set
        {
            field = value;
            UpdateState();
        }
    }

    public string PageText
    {
        get => _pageLabel.Text;
        set => _pageLabel.Text = value ?? string.Empty;
    }

    public void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        BackColor = theme.Palette.Surface;
        _pageLabel.ForeColor = theme.Palette.MutedText;
        _previousButton.ApplyTheme(theme, HiveButtonStyle.Secondary);
        _nextButton.ApplyTheme(theme, HiveButtonStyle.Secondary);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Height = HiveDpi.Scale(this, 44);
        Padding = HiveDpi.Scale(this, new Padding(0, 4, 0, 4));

        if (Controls.Count == 1 && Controls[0] is TableLayoutPanel layout)
        {
            layout.ColumnStyles[0].Width = HiveDpi.Scale(this, 96);
            layout.ColumnStyles[2].Width = HiveDpi.Scale(this, 96);
        }
    }

    private HiveButton CreateButton(string text) =>
        new()
        {
            Text = text,
            Style = HiveButtonStyle.Secondary,
            Dock = DockStyle.Fill,
            Margin = HiveDpi.Scale(this, new Padding(0, 0, 8, 0))
        };

    private void UpdateState()
    {
        _previousButton.Enabled = CanGoPrevious;
        _nextButton.Enabled = CanGoNext;
        _pageLabel.Text = $"Page {_pageNumber}";
    }
}
