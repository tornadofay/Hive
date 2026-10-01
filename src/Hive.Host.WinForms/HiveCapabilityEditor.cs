using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveCapabilityEditor : UserControl
{
    private const string NotConfiguredText = "Not configured";
    private const string ManagedByDiscoveryText = "Managed by discovery";

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
        HiveComboBox Configured,
        Label Current);

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
        MinimumSize = new Size(0, 220);

        _description = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 42,
            Padding = new Padding(4, 2, 4, 4),
            Text =
                "Set the capability state on the right. Current shows the effective state and its source."
        };

        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(0, 2, 0, 0)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _table.RowCount = 1;
        _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        AddHeader("Capability", 0);
        AddHeader("Set state", 1);
        AddHeader("Current", 2);

        foreach (var (key, name) in KnownCapabilities)
        {
            var configured = new HiveComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Height = 32,
                Tag = key,
                AccessibleName = $"{name} capability state",
                AccessibleDescription =
                    "Choose Not configured, Supported, Unsupported, or Unknown for this capability."
            };
            configured.Items.Add(NotConfiguredText);
            foreach (var state in Enum.GetValues<CapabilityState>())
                configured.Items.Add(state);
            configured.Items.Add(ManagedByDiscoveryText);
            configured.SelectedIndex = 0;
            configured.SelectedIndexChanged += ConfiguredOnChanged;

            var current = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "Unknown • not reported",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Padding = new Padding(4, 0, 4, 0),
                AccessibleName = $"{name} current capability state"
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
            _table.Controls.Add(configured, 1, _table.RowCount);
            _table.Controls.Add(current, 2, _table.RowCount);

            _rows.Add(new CapabilityRow(key, configured, current));
            _table.RowCount++;
        }

        _additionalLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 42,
            Padding = new Padding(4, 6, 4, 4),
            AutoEllipsis = true,
            Text = "No additional provider-specific capability evidence."
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

            if (_automatic)
            {
                if (!row.Configured.Items.Contains(ManagedByDiscoveryText))
                    row.Configured.Items.Add(ManagedByDiscoveryText);

                row.Configured.SelectedItem = ManagedByDiscoveryText;
                row.Configured.Enabled = false;
            }
            else
            {
                var managedIndex =
                    row.Configured.Items.IndexOf(ManagedByDiscoveryText);
                if (managedIndex >= 0)
                    row.Configured.Items.RemoveAt(managedIndex);

                row.Configured.Enabled = true;
                row.Configured.SelectedItem = configuredState is null
                    ? NotConfiguredText
                    : configuredState.State;
            }

            var currentState = _automatic
                ? discovered?.State ?? configuredState?.State ?? CapabilityState.Unknown
                : configuredState?.State ?? discovered?.State ?? CapabilityState.Unknown;

            row.Current.Text = FormatCurrentState(
                currentState,
                discoveredSource: discovered is not null,
                overrideSource: !_automatic && configuredState is not null);
        }

        UpdateAdditionalEvidence();

        _description.Text = _automatic
            ? "Automatic target: provider discovery controls the capability state. Change Management to Manual to set an override."
            : "Manual target: choose a state to override discovery, or Not configured to use discovery when available.";

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
                result.Add(new CapabilityStateEntry(row.Key, state));
        }

        return result
            .OrderBy(item => item.Capability.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public void SetDiscovery(ProviderModelMetadata? discovery)
    {
        if (_automatic)
            _automaticCapabilities = Array.Empty<CapabilityStateEntry>();

        _discovered = discovery?.DiscoveredCapabilities ?? Array.Empty<CapabilityStateEntry>();

        UpdateAdditionalEvidence();
        UpdateCurrentStates();
    }

    private void ConfiguredOnChanged(object? sender, EventArgs e)
    {
        if (_automatic)
            return;

        UpdateCurrentStates();
    }

    private void UpdateCurrentStates()
    {
        foreach (var row in _rows)
        {
            var configured = row.Configured.SelectedItem is CapabilityState state
                ? state
                : (CapabilityState?)null;
            var discovered = FindState(_discovered, row.Key);

            var current = configured ?? discovered?.State ?? CapabilityState.Unknown;
            var discoveredIsSource = configured is null && discovered is not null;

            row.Current.Text = FormatCurrentState(
                current,
                discoveredSource: discoveredIsSource,
                overrideSource: configured is not null);
        }
    }

    private void UpdateAdditionalEvidence()
    {
        var knownKeys = KnownCapabilities
            .Select(item => item.Key)
            .ToHashSet();

        var unknownDiscovered = _discovered
            .Where(item => !knownKeys.Contains(item.Capability))
            .Select(item => $"{item.Capability}={item.State}")
            .ToArray();

        _additionalLabel.Text = unknownDiscovered.Length == 0
            ? _preservedUnknown.Count == 0
                ? "No additional provider-specific capability evidence."
                : $"Additional configured capability entries are preserved but not editable here: {string.Join(", ", _preservedUnknown.Select(item => item.Capability.Value))}"
            : $"Provider-specific capability evidence (read-only): {string.Join(", ", unknownDiscovered)}";
    }

    private static CapabilityStateEntry? FindState(
        IReadOnlyList<CapabilityStateEntry> entries,
        CapabilityKey key) =>
        entries.FirstOrDefault(item => item.Capability == key);

    private static string FormatCurrentState(
        CapabilityState state,
        bool discoveredSource,
        bool overrideSource) =>
        overrideSource
            ? $"{state} • override"
            : discoveredSource
                ? $"{state} • discovered"
                : state == CapabilityState.Unknown
                    ? "Unknown • not reported"
                    : $"{state} • current";

    private void AddHeader(string text, int column)
    {
        var fallbackFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        _table.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = text,
                Font = new Font(fallbackFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            },
            column,
            0);
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
