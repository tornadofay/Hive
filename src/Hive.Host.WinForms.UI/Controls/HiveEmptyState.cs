using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

public sealed class HiveEmptyState : Panel
{
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private Font? _titleFont;
    private Font? _descriptionFont;

    public HiveEmptyState(string title, string? description = null)
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(24);
        BackColor = Color.Transparent;

        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _titleLabel = new Label
        {
            Text = title ?? string.Empty,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _descriptionLabel = new Label
        {
            Text = description ?? string.Empty,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
            Visible = !string.IsNullOrWhiteSpace(description)
        };

        var center = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        center.Controls.Add(_titleLabel);
        if (_descriptionLabel.Visible)
        {
            center.Controls.Add(_descriptionLabel);
        }

        host.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 0);
        host.Controls.Add(center, 0, 1);
        host.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 2);

        Controls.Add(host);
    }

    public void ApplyTheme(HiveThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var typography = theme.Typography;
        var palette = theme.Palette;

        ReplaceFont(ref _titleFont, _titleLabel, typography.FontFamily, typography.SectionSize, FontStyle.Bold);
        ReplaceFont(ref _descriptionFont, _descriptionLabel, typography.FontFamily, typography.SmallSize, FontStyle.Regular);

        _titleLabel.ForeColor = palette.MutedText;
        BackColor = Color.Transparent;
        _descriptionLabel.ForeColor = palette.MutedText;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _titleFont = null;
            _descriptionFont?.Dispose();
            _descriptionFont = null;
        }

        base.Dispose(disposing);
    }

    private static void ReplaceFont(
        ref Font? current,
        Label target,
        string family,
        float size,
        FontStyle style)
    {
        if (current is not null &&
            string.Equals(current.FontFamily.Name, family, StringComparison.Ordinal) &&
            Math.Abs(current.Size - size) <= 0.01f &&
            current.Style == style)
        {
            target.Font = current;
            return;
        }

        var previous = current;
        current = new Font(family, size, style);
        target.Font = current;
        previous?.Dispose();
    }
}
