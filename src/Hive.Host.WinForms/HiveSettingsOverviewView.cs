using System.Drawing;
using System.Windows.Forms;

namespace Hive.Host.WinForms;

internal sealed class HiveSettingsOverviewView : UserControl
{
    private readonly FlowLayoutPanel _cards;
    private readonly Font _headingFont;
    private readonly Font _sectionFont;
    private readonly List<Font> _cardTitleFonts = new();

    public HiveSettingsOverviewView()
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(4, 0, 4, 4);
        AutoScroll = true;

        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        _headingFont = new Font(
            fallbackFont.FontFamily,
            Math.Max(13f, fallbackFont.Size + 3f),
            FontStyle.Bold);
        _sectionFont = new Font(
            fallbackFont.FontFamily,
            fallbackFont.Size + 1f,
            FontStyle.Bold);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = new Padding(4, 0, 12, 12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var heading = new Label
        {
            AutoSize = true,
            Font = _headingFont,
            Text = "Overview",
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var introduction = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Text =
                "Hive Settings is the central place for the Hive configuration owned by your application. " +
                "Start with Providers, then configure Accounts / Credentials and Execution Targets. " +
                "Agents reference those configured targets, while Persistence controls the Hive package database.",
            Margin = new Padding(0, 8, 0, 20),
            Padding = Padding.Empty
        };

        _cards = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        AddCard(
            "Providers",
            "Define provider identity and transport configuration.",
            "Start here when adding a new provider.");

        AddCard(
            "Accounts / Credentials",
            "Connect providers to durable account records and secure credential references.",
            "Credentials are managed through Hive's Secret Store boundary.");

        AddCard(
            "Execution Targets",
            "Define concrete endpoint, model, or deployment targets and their declared capabilities.",
            "Agents use these targets for execution.");

        AddCard(
            "Agents",
            "Define the persisted AgentDefinitions used by configured-host execution.",
            "Agents reference configured Execution Targets.");

        AddCard(
            "Persistence",
            "Configure the SQL Server / LocalDB location used by Hive's package database.",
            "Initialization is always an explicit action.");

        var flowHeading = new Label
        {
            AutoSize = true,
            Font = _sectionFont,
            Text = "Configuration flow",
            Margin = new Padding(0, 12, 0, 6),
            Padding = Padding.Empty
        };

        var flow = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Text = "Providers  →  Accounts / Credentials  →  Execution Targets  →  Agents",
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(introduction, 0, 1);
        root.Controls.Add(_cards, 0, 2);
        root.Controls.Add(flowHeading, 0, 3);
        root.Controls.Add(flow, 0, 4);

        Controls.Add(root);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headingFont.Dispose();
            _sectionFont.Dispose();

            foreach (var font in _cardTitleFonts)
                font.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AddCard(
        string title,
        string description,
        string note)
    {
        var card = new Panel
        {
            Size = new Size(260, 124),
            Margin = new Padding(0, 0, 12, 12),
            Padding = new Padding(14),
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = $"{title} settings overview"
        };

        var titleFont = new Font(
            _headingFont.FontFamily,
            Math.Max(9.25f, _headingFont.Size - 2.5f),
            FontStyle.Bold);
        _cardTitleFonts.Add(titleFont);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = titleFont,
            Text = title,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(228, 0),
            Text = description,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty
        };

        var noteLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(228, 0),
            Text = note,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty
        };

        card.Controls.Add(noteLabel);
        card.Controls.Add(descriptionLabel);
        card.Controls.Add(titleLabel);
        _cards.Controls.Add(card);
    }
}
