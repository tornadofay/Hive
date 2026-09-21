using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class OverviewExampleView : UserControl
{
    private const string RepositoryUrl = "https://github.com/tornadofay/Hive";

    private readonly Panel _hero;
    private readonly TableLayoutPanel _cards;
    private readonly TableLayoutPanel _navigationSection;
    private readonly TableLayoutPanel _architectureSection;
    private readonly TableLayoutPanel _projectSection;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _intro;
    private readonly LinkLabel _repositoryLink;
    private readonly Label _projectText;
    private readonly IHiveThemeManager _themeManager;
    private readonly Label _hostText;
    private readonly Label _uiText;
    private readonly Label _navigationText;
    private readonly Label _architectureText;
    private readonly Font _eyebrowFont;
    private readonly Font _titleFont;
    private readonly Font _sectionFont;
    private readonly Font _bodyFont;
    private readonly Font _cardTitleFont;
    private readonly Font _linkFont;

    public OverviewExampleView(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        _themeManager = themeManager;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        AutoScroll = true;

        _eyebrowFont = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        _titleFont = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
        _sectionFont = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        _bodyFont = new Font("Segoe UI", 9.25f);
        _cardTitleFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _linkFont = new Font("Segoe UI Semibold", 9.25f, FontStyle.Underline);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = new Padding(2, 2, 2, 18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _hero = CreateHero();
        _cards = CreateCards();
        _navigationSection = CreateSection(
            "How to use this form",
            "The left navigation is the Example Host index. Expand a category and subcategory, then select an example. The selected example replaces the main content area without rebuilding the host shell. The output pane at the bottom is shared across examples.");
        _architectureSection = CreateSection(
            "What this application demonstrates",
            "Hive.Example.WinForms is a permanent developer-facing host for exercising the reusable Hive UI foundation. It demonstrates the controls, interaction patterns, CRUD composition, dialogs, themes, and the reusable example-test surface used by ordinary developer examples.");
        _projectSection = CreateSection(
            "Project direction",
            "Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems. Microsoft Agent Framework provides underlying execution and workflow infrastructure where it already owns the mechanism; Hive supplies the platform contracts, governance, management, persistence, host integration, and higher-level boundaries.");

        root.Controls.Add(_hero, 0, 0);
        root.Controls.Add(_cards, 0, 1);
        root.Controls.Add(_navigationSection, 0, 2);
        root.Controls.Add(_architectureSection, 0, 3);
        root.Controls.Add(_projectSection, 0, 4);

        Controls.Add(root);

        _projectText = FindCardBody(_cards, 0);
        _hostText = FindCardBody(_cards, 1);
        _uiText = FindCardBody(_cards, 2);

        _projectText.Text =
            "General-purpose .NET 10 multi-agent platform, with a concrete V1 forcing function.";

        _hostText.Text =
            "First-class developer host for exercising Hive features and reusable UI building blocks.";

        _uiText.Text =
            "Shared WinForms foundation for themes, controls, CRUD, dialogs, navigation, and example testing.";

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme(_themeManager.Theme);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _eyebrowFont.Dispose();
            _titleFont.Dispose();
            _sectionFont.Dispose();
            _bodyFont.Dispose();
            _cardTitleFont.Dispose();
            _linkFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private Panel CreateHero()
    {
        var hero = new Panel
        {
            Dock = DockStyle.Top,
            Height = 178,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(24, 20, 24, 18)
        };

        _eyebrow = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Font = _eyebrowFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "HIVE  ·  DEVELOPER EXAMPLE HOST",
            UseMnemonic = false
        };

        _title = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Font = _titleFont,
            Margin = new Padding(0, 2, 0, 0),
            Padding = Padding.Empty,
            Text = "Build, inspect, and exercise Hive",
            UseMnemonic = false
        };

        _intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 46,
            Font = _bodyFont,
            Margin = new Padding(0, 7, 0, 0),
            Padding = Padding.Empty,
            Text = "This application is the permanent developer-facing host for the Hive platform. It presents reusable UI building blocks and focused examples without turning every example into the same application layout.",
            UseMnemonic = false
        };

        _repositoryLink = new LinkLabel
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            Font = _linkFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "github.com/tornadofay/Hive",
            LinkBehavior = LinkBehavior.HoverUnderline,
            TabStop = true,
            TabIndex = 0,
            UseMnemonic = true
        };
        _repositoryLink.LinkClicked += (_, _) => OpenRepository();

        hero.Controls.Add(_repositoryLink);
        hero.Controls.Add(_intro);
        hero.Controls.Add(_title);
        hero.Controls.Add(_eyebrow);

        return hero;
    }

    private TableLayoutPanel CreateCards()
    {
        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 142,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 20),
            Padding = Padding.Empty
        };

        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));

        cards.Controls.Add(
            CreateCard(
                "Hive platform",
                string.Empty),
            0,
            0);

        cards.Controls.Add(
            CreateCard(
                "Example Host",
                string.Empty),
            1,
            0);

        cards.Controls.Add(
            CreateCard(
                "UI foundation",
                string.Empty),
            2,
            0);

        return cards;
    }

    private Panel CreateCard(string title, string text)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(16, 14, 16, 12)
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Font = _cardTitleFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = title,
            UseMnemonic = false
        };

        var body = new Label
        {
            Dock = DockStyle.Fill,
            Font = _bodyFont,
            Margin = new Padding(0, 6, 0, 0),
            Padding = Padding.Empty,
            Text = text,
            UseMnemonic = false
        };

        card.Controls.Add(body);
        card.Controls.Add(titleLabel);
        return card;
    }

    private TableLayoutPanel CreateSection(string title, string text)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 104,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0, 0, 18, 0)
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            Font = _sectionFont,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = title,
            UseMnemonic = false
        };

        var body = new Label
        {
            Dock = DockStyle.Fill,
            Font = _bodyFont,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0),
            Text = text,
            UseMnemonic = false
        };

        section.Controls.Add(heading, 0, 0);
        section.Controls.Add(body, 0, 1);
        return section;
    }

    private static Label FindCardBody(
        TableLayoutPanel cards,
        int column)
    {
        var card = (Panel)cards.GetControlFromPosition(column, 0);
        return (Label)card.Controls
            .OfType<Label>()
            .First(control => control.Dock == DockStyle.Fill);
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyTheme(_themeManager.Theme);
    }

    private void OpenRepository()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = RepositoryUrl,
                    UseShellExecute = true
                });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not open Hive repository: {exception}");
        }
    }

    internal void ApplyTheme(HiveThemeDefinition theme)
    {
        BackColor = theme.Palette.Surface;
        _hero.BackColor = theme.Palette.ElevatedSurface;
        _cards.BackColor = theme.Palette.Surface;
        _navigationSection.BackColor = theme.Palette.Surface;
        _architectureSection.BackColor = theme.Palette.Surface;
        _projectSection.BackColor = theme.Palette.Surface;
        _eyebrow.ForeColor = theme.Palette.Accent;
        _title.ForeColor = theme.Palette.Text;
        _intro.ForeColor = theme.Palette.MutedText;
        _repositoryLink.LinkColor = theme.Palette.Accent;
        _repositoryLink.ActiveLinkColor = theme.Palette.AccentHover;
        _projectText.ForeColor = theme.Palette.Text;
        _hostText.ForeColor = theme.Palette.Text;
        _uiText.ForeColor = theme.Palette.Text;

        foreach (Control card in _cards.Controls)
        {
            card.BackColor = theme.Palette.ElevatedSurface;
        }
    }
}
