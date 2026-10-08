using System.Data;
using Microsoft.Data.Sql;
using Microsoft.Win32;
using System.ComponentModel;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

internal static class HiveSqlServerInstanceDiscovery
{
    public static Task<IReadOnlyList<string>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var network = DiscoverNetworkInstances();
                var local = DiscoverInstalledLocalInstances();

                cancellationToken.ThrowIfCancellationRequested();

                return MergeCandidates(
                    network,
                    local);
            },
            cancellationToken);
    }

    internal static IReadOnlyList<string> MergeCandidates(
        IEnumerable<string> networkInstances,
        IEnumerable<string> localInstances)
    {
        ArgumentNullException.ThrowIfNull(networkInstances);
        ArgumentNullException.ThrowIfNull(localInstances);

        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in networkInstances)
        {
            if (!string.IsNullOrWhiteSpace(instance))
                names.Add(instance.Trim());
        }

        foreach (var instance in localInstances)
        {
            if (string.IsNullOrWhiteSpace(instance))
                continue;

            names.Add(
                FormatInstalledInstanceName(instance));
        }

        return names.ToArray();
    }

    internal static string FormatInstalledInstanceName(
        string instanceName)
    {
        var normalized = instanceName.Trim();
        if (normalized.Length == 0)
            throw new ArgumentException(
                "SQL Server instance name is required.",
                nameof(instanceName));

        return string.Equals(
            normalized,
            "MSSQLSERVER",
            StringComparison.OrdinalIgnoreCase)
            ? "localhost"
            : $@"localhost\{normalized}";
    }

    private static IReadOnlyList<string> DiscoverNetworkInstances()
    {
        var table = SqlDataSourceEnumerator.Instance.GetDataSources();
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DataRow row in table.Rows)
        {
            var server = Convert.ToString(row["ServerName"])?.Trim();
            var instance = Convert.ToString(row["InstanceName"])?.Trim();

            if (string.IsNullOrWhiteSpace(server))
                continue;

            var name = string.IsNullOrWhiteSpace(instance)
                ? server
                : $@"{server}\{instance}";

            names.Add(name);
        }

        return names.ToArray();
    }

    private static IReadOnlyList<string> DiscoverInstalledLocalInstances()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    view);

                using var instanceNames = baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");

                if (instanceNames is null)
                    continue;

                foreach (var name in instanceNames.GetValueNames())
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name.Trim());
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        return names.ToArray();
    }
}

internal sealed class HiveSqlServerInstancePicker : UserControl
{
    private sealed record ServerChoice(string DisplayName, bool IsCustom)
    {
        public override string ToString() => DisplayName;
    }

    private readonly Hive.Host.WinForms.UI.Theme.IHiveThemeManager _themeManager;
    private readonly Hive.Host.WinForms.UI.Controls.HiveComboBox _serverComboBox;
    private readonly TextBox _customServerTextBox;
    private readonly TextBox _portTextBox;
    private readonly Hive.Host.WinForms.UI.Controls.HiveButton _refreshButton;
    private readonly TableLayoutPanel _layout;
    private readonly TableLayoutPanel _topRow;
    private readonly TableLayoutPanel _customRow;
    private bool _applyingValue;

    public event EventHandler? RefreshRequested;

    public HiveSqlServerInstancePicker(
        Hive.Host.WinForms.UI.Theme.IHiveThemeManager themeManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        _serverComboBox = new Hive.Host.WinForms.UI.Controls.HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        _serverComboBox.SelectedIndexChanged += (_, _) => UpdateCustomVisibility();

        _customServerTextBox = CreateTextBox();
        _customServerTextBox.PlaceholderText = "Custom server or instance";
        _customServerTextBox.Visible = false;

        _portTextBox = CreateTextBox();
        _portTextBox.PlaceholderText = "Port";

        _refreshButton = new Hive.Host.WinForms.UI.Controls.HiveButton
        {
            Text = "Refresh",
            Style = Hive.Host.WinForms.UI.Controls.HiveButtonStyle.Secondary,
            Width = 82,
            Height = 32,
            Margin = new Padding(6, 0, 0, 0)
        };
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _topRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));

        _topRow.Controls.Add(_serverComboBox, 0, 0);
        _topRow.Controls.Add(_portTextBox, 1, 0);
        _topRow.Controls.Add(_refreshButton, 2, 0);

        _customRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0),
            Padding = Padding.Empty,
            Visible = false
        };
        _customRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _customRow.Controls.Add(_customServerTextBox, 0, 0);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
        _layout.Controls.Add(_topRow, 0, 0);
        _layout.Controls.Add(_customRow, 0, 1);

        Controls.Add(_layout);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        MinimumSize = new Size(0, 36);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);
        SetDiscoveredInstances(Array.Empty<string>(), null);
    }

    internal HiveComboBox ServerSelector => _serverComboBox;

    internal TextBox CustomServerInput => _customServerTextBox;

    public string ServerName
    {
        get
        {
            if (IsCustomSelected)
                return _customServerTextBox.Text.Trim();

            return (_serverComboBox.SelectedItem as ServerChoice)?.DisplayName?.Trim() ?? string.Empty;
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? Port
    {
        get => int.TryParse(_portTextBox.Text.Trim(), out var port) && port > 0
            ? port
            : null;
        set => _portTextBox.Text = value is > 0
            ? value.Value.ToString()
            : string.Empty;
    }

    public bool IsCustomSelected =>
        (_serverComboBox.SelectedItem as ServerChoice)?.IsCustom == true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsReadOnly
    {
        get => !_serverComboBox.Enabled;
        set
        {
            _serverComboBox.Enabled = !value;
            _refreshButton.Enabled = !value;
            _customServerTextBox.ReadOnly = value;
            _portTextBox.ReadOnly = value;
        }
    }

    public void SetValue(string serverName, int? port)
    {
        _applyingValue = true;
        try
        {
            Port = port;

            var server = serverName?.Trim() ?? string.Empty;
            var match = _serverComboBox.Items
                .OfType<ServerChoice>()
                .FirstOrDefault(
                    choice => !choice.IsCustom &&
                        string.Equals(
                            choice.DisplayName,
                            server,
                            StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                _serverComboBox.SelectedItem = match;
                _customServerTextBox.Clear();
            }
            else
            {
                var custom = _serverComboBox.Items
                    .OfType<ServerChoice>()
                    .LastOrDefault(static choice => choice.IsCustom);

                _serverComboBox.SelectedItem = custom;
                _customServerTextBox.Text = server;
            }
        }
        finally
        {
            _applyingValue = false;
            UpdateCustomVisibility();
        }
    }

    public void SetDiscoveredInstances(
        IReadOnlyList<string> instances,
        string? preferredServer)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var preferred = preferredServer?.Trim() ?? ServerName;
        _serverComboBox.Items.Clear();

        foreach (var instance in instances
                     .Where(static value => !string.IsNullOrWhiteSpace(value))
                     .Select(static value => value.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        {
            _serverComboBox.Items.Add(new ServerChoice(instance, false));
        }

        _serverComboBox.Items.Add(new ServerChoice("Custom...", true));

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var discovered = _serverComboBox.Items
                .OfType<ServerChoice>()
                .FirstOrDefault(
                    choice => !choice.IsCustom &&
                        string.Equals(
                            choice.DisplayName,
                            preferred,
                            StringComparison.OrdinalIgnoreCase));

            if (discovered is not null)
            {
                _serverComboBox.SelectedItem = discovered;
                _customServerTextBox.Clear();
            }
            else
            {
                _serverComboBox.SelectedIndex = _serverComboBox.Items.Count - 1;
                _customServerTextBox.Text = preferred;
            }
        }
        else
        {
            _serverComboBox.SelectedIndex =
                _serverComboBox.Items.Count > 1
                    ? 0
                    : _serverComboBox.Items.Count - 1;
        }

        UpdateCustomVisibility();
    }

    public async Task RefreshAsync(
        string? preferredServer = null,
        CancellationToken cancellationToken = default)
    {
        var preferred = preferredServer?.Trim() ?? ServerName;

        var instances = await HiveSqlServerInstanceDiscovery
            .DiscoverAsync(cancellationToken)
            .ConfigureAwait(true);

        SetDiscoveredInstances(instances, preferred);
    }

    public void SetEnabled(bool enabled)
    {
        _serverComboBox.Enabled = enabled;
        _refreshButton.Enabled = enabled;
        _customServerTextBox.Enabled = enabled;
        _portTextBox.Enabled = enabled;
    }

    private void UpdateCustomVisibility()
    {
        var custom = IsCustomSelected;
        _customRow.Visible = custom;
        _layout.RowStyles[1].Height = custom ? 42f : 0f;
        Height = custom ? 78 : 36;

        if (!custom)
            _customServerTextBox.Clear();

        if (!_applyingValue)
            PerformLayout();
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        _themeManager.Apply(this);

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty
        };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

        }

        base.Dispose(disposing);
    }
}
