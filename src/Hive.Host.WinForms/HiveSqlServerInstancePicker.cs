using System.Data;
using Microsoft.Data.Sql;

namespace Hive.Host.WinForms;

internal static class HiveSqlServerInstanceDiscovery
{
    public static Task<IReadOnlyList<string>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            static () =>
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

                return (IReadOnlyList<string>)names.ToArray();
            },
            cancellationToken);
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
        _customServerTextBox.Visible = false;

        _portTextBox = CreateTextBox();

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
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104f));

        _topRow.Controls.Add(_serverComboBox, 0, 0);
        _topRow.Controls.Add(_refreshButton, 1, 0);
        _topRow.Controls.Add(_portTextBox, 2, 0);

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
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
        _layout.Controls.Add(_topRow, 0, 0);
        _layout.Controls.Add(_customRow, 0, 1);

        Controls.Add(_layout);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        MinimumSize = new Size(260, 38);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);
        SetDiscoveredInstances(Array.Empty<string>(), null);
    }

    public string ServerName
    {
        get
        {
            if (IsCustomSelected)
                return _customServerTextBox.Text.Trim();

            return (_serverComboBox.SelectedItem as ServerChoice)?.DisplayName?.Trim() ?? string.Empty;
        }
    }

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
            _serverComboBox.SelectedIndex = Math.Max(0, _serverComboBox.Items.Count - 1);
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
        _layout.RowStyles[1].Height = custom ? 38f : 0f;
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
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

        base.Dispose(disposing);
    }
}
