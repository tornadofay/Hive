using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveCapabilityEditor : UserControl
{
    private static readonly (CapabilityKey Key, string Name)[] KnownCapabilities =
    [
        (HiveCapabilityKeys.TextGeneration, "Text generation"),
        (HiveCapabilityKeys.Vision, "Vision"),
        (HiveCapabilityKeys.ToolCalling, "Tool calling"),
        (HiveCapabilityKeys.StructuredOutput, "Structured output"),
        (HiveCapabilityKeys.Reasoning, "Reasoning"),
        (HiveCapabilityKeys.Thinking, "Thinking")
    ];

    private sealed record CapabilityRow(
        CapabilityKey Key,
        Label Discovered,
        ComboBox Configured,
        Label Effective);

    private readonly IHiveThemeManager _themeManager;
    private readonly TableLayoutPanel _table;
    private readonly Label _description;
    private readonly Label _additionalLabel;
    private readonly List<CapabilityRow> _rows = [];
    private IReadOnlyList<CapabilityStateEntry> _preservedUnknown = Array.Empty<CapabilityStateEntry>();
    private IReadOnlyList<CapabilityStateEntry> _automaticCapabilities = Array.Empty<CapabilityStateEntry>();
    private IReadOnlyList<CapabilityStateEntry> _discovered = Array.Empty<CapabilityStateEntry>();
    private bool _automatic;

    public HiveCapabilityEditor(
        IHiveThemeManager themeManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        MinimumSize = new Size(0, 206);

        _description = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 42,
            Text =
                "Known Hive capabilities use structured Supported / Unsupported / Unknown states. " +
                "Automatic targets are discovery-managed; Manual targets can define explicit overrides."
        };

        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            Padding = new Padding(0, 2, 0, 0)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));

        AddHeader("Capability", 0);
        AddHeader("Discovered", 1);
        AddHeader("Configured Override", 2);
        AddHeader("Effective", 3);

        foreach (var (key, name) in KnownCapabilities)
        {
            var discovered = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "Not reported",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            };

            var configured = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false,
                Height = 32,
                Tag = key
            };
            configured.Items.Add("Not configured");
            foreach (var state in Enum.GetValues<CapabilityState>())
                configured.Items.Add(state);
            configured.SelectedIndex = 0;
            configured.SelectedIndexChanged += ConfiguredOnChanged;

            var effective = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "Unknown",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            };

            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _table.Controls.Add(
                new Label
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    Text = name,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 4, 0),
                    AccessibleName = $"{name} capability"
                },
                0,
                _table.RowCount);
            _table.Controls.Add(discovered, 1, _table.RowCount);
            _table.Controls.Add(configured, 2, _table.RowCount);
            _table.Controls.Add(effective, 3, _table.RowCount);

            _rows.Add(new CapabilityRow(key, discovered, configured, effective));
            _table.RowCount++;
        }

        _additionalLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 38,
            Padding = new Padding(4, 8, 4, 4),
            Text = "No additional capability evidence."
        };

        Controls.Add(_additionalLabel);
        Controls.Add(_table);
        Controls.Add(_description);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme();
    }

    public void Configure(
        IReadOnlyList<CapabilityStateEntry> configured,
        ProviderModelMetadata? discovery,
        bool automatic)
    {
        ArgumentNullException.ThrowIfNull(configured);

        _automatic = automatic;
        _automaticCapabilities = configured.ToArray();
        _discovered = discovery?.DiscoveredCapabilities ?? Array.Empty<CapabilityStateEntry>();

        var knownKeys = KnownCapabilities
            .Select(item => item.Key)
            .ToHashSet();

        _preservedUnknown = configured
            .Where(item => !knownKeys.Contains(item.Capability))
            .ToArray();

        foreach (var row in _rows)
        {
            var discovered = FindState(_discovered, row.Key);
            var configuredState = FindState(configured, row.Key);

            row.Discovered.Text = FormatState(discovered);
            row.Configured.SelectedItem = configuredState is null
                ? "Not configured"
                : configuredState.Value;
            row.Configured.Enabled = !_automatic;

            var effective = _automatic
                ? configuredState?.State ?? discovered?.State ?? CapabilityState.Unknown
                : configuredState?.State ?? discovered?.State ?? CapabilityState.Unknown;

            row.Effective.Text = effective.ToString();
        }

        var unknownDiscovered = _discovered
            .Where(item => !knownKeys.Contains(item.Capability))
            .Select(item => $"{item.Capability}={item.State}")
            .ToArray();

        _additionalLabel.Text = unknownDiscovered.Length == 0
            ? _preservedUnknown.Count == 0
                ? "No additional provider-specific capability evidence."
                : $"Additional configured capability entries are preserved but not editable here: {string.Join(", ", _preservedUnknown.Select(item => item.Capability.Value))}"
            : $"Provider-specific capability evidence (read-only): {string.Join(", ", unknownDiscovered)}";

        _description.Text = _automatic
            ? "Automatic target: capability state is maintained by successful discovery. Change Management to Manual to define explicit overrides."
            : "Manual target: select a state or Not configured for each known capability. Not configured leaves discovery evidence available for the effective state.";

        ApplyTheme();
    }

    public IReadOnlyList<CapabilityStateEntry> GetConfiguredCapabilities()
    {
        if (_automatic)
            return _automaticCapabilities;

        var result = _preservedUnknown.ToList();

        foreach (var row in _rows)
        {
            if (row.Configured.SelectedItem is CapabilityState state)
            {
                result.Add(new CapabilityStateEntry(row.Key, state));
            }
        }

        return result
            .OrderBy(item => item.Capability.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public void SetDiscovery(ProviderModelMetadata? discovery)
    {
        _discovered = discovery?.DiscoveredCapabilities ?? Array.Empty<CapabilityStateEntry>();

        foreach (var row in _rows)
            row.Discovered.Text = FormatState(FindState(_discovered, row.Key));

        var knownKeys = KnownCapabilities.Select(item => item.Key).ToHashSet();
        var unknownDiscovered = _discovered
            .Where(item => !knownKeys.Contains(item.Capability))
            .Select(item => $"{item.Capability}={item.State}")
            .ToArray();

        _additionalLabel.Text = unknownDiscovered.Length == 0
            ? _preservedUnknown.Count == 0
                ? "No additional provider-specific capability evidence."
                : $"Additional configured capability entries are preserved but not editable here: {string.Join(", ", _preservedUnknown.Select(item => item.Capability.Value))}"
            : $"Provider-specific capability evidence (read-only): {string.Join(", ", unknownDiscovered)}";

        UpdateEffectiveStates();
    }

    private void ConfiguredOnChanged(object? sender, EventArgs e)
    {
        if (_automatic)
            return;

        UpdateEffectiveStates();
    }

    private void UpdateEffectiveStates()
    {
        foreach (var row in _rows)
        {
            var configured = row.Configured.SelectedItem is CapabilityState state
                ? state
                : (CapabilityState?)null;
            var discovered = FindState(_discovered, row.Key)?.State;
            row.Effective.Text = (configured ?? discovered ?? CapabilityState.Unknown).ToString();
        }
    }

    private static CapabilityStateEntry? FindState(
        IReadOnlyList<CapabilityStateEntry> entries,
        CapabilityKey key) =>
        entries.FirstOrDefault(item => item.Capability == key);

    private static string FormatState(CapabilityStateEntry? state) =>
        state is null ? "Not reported" : state.Value.State.ToString();

    private void AddHeader(string text, int column)
    {
        _table.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = text,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            },
            column,
            _table.RowCount);
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme() => _themeManager.Apply(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

        base.Dispose(disposing);
    }
}
