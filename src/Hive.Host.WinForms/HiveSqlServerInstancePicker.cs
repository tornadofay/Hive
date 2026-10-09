using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

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
    private readonly TableLayoutPanel _statusRow;
    private readonly ProgressBar _discoverySpinner;
    private readonly Label _discoveryStatusLabel;
    private readonly SqlServerInstanceDiscoveryCoordinator _discovery;
    private CancellationTokenSource? _activeDiscoveryCts;
    private DiscoverySnapshot? _activeDiscoverySnapshot;
    private int _discoveryGeneration;
    private sealed record DiscoverySnapshot(
        int Generation,
        int UserEditVersion,
        string PreferredServer,
        SynchronizationContext? SynchronizationContext);

    private bool _applyingValue;
    private bool _settingPort;
    private bool _portWasAutomaticallyDefaulted;
    private int _userEditVersion;

    public event EventHandler? RefreshRequested;

    public HiveSqlServerInstancePicker(
        Hive.Host.WinForms.UI.Theme.IHiveThemeManager themeManager,
        SqlServerInstanceDiscoveryCoordinator? discovery = null)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _discovery = discovery ?? HiveSqlServerInstanceDiscovery.Shared;

        SuspendLayout();

        _serverComboBox = new Hive.Host.WinForms.UI.Controls.HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        _serverComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_applyingValue)
                return;

            _userEditVersion++;
            ApplyPortDefaultForSelection();
            UpdateCustomVisibility();
        };

        _customServerTextBox = CreateTextBox(autoSize: true);
        _customServerTextBox.PlaceholderText = "Custom server or instance";
        _customServerTextBox.Visible = false;

        _customServerTextBox.TextChanged += (_, _) =>
        {
            if (_applyingValue)
                return;

            _userEditVersion++;

            if (IsCustomSelected)
                ApplyPortDefaultForSelection();
        };

        _portTextBox = CreateTextBox();
        _portTextBox.PlaceholderText = "Port";
        _portTextBox.TextChanged += (_, _) =>
        {
            if (_settingPort)
                return;

            _portWasAutomaticallyDefaulted = false;
            if (!_applyingValue)
                _userEditVersion++;
        };

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
        _topRow.SuspendLayout();
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
        _topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));

        _topRow.Controls.Add(_serverComboBox, 0, 0);
        _topRow.Controls.Add(_portTextBox, 1, 0);
        _topRow.Controls.Add(_refreshButton, 2, 0);

        _customRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0),
            Padding = Padding.Empty,
            Visible = false
        };
        _customRow.SuspendLayout();
        _customRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _customRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _customRow.Controls.Add(_customServerTextBox, 0, 0);

        _statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 3, 0, 0),
            Padding = Padding.Empty,
            Visible = false
        };
        _statusRow.SuspendLayout();
        _statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20f));
        _statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _statusRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _discoverySpinner = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 25,
            Size = new Size(16, 16),
            Margin = new Padding(0, 1, 4, 1),
            Anchor = AnchorStyles.Left,
            Visible = false
        };
        _discoveryStatusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 18,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Visible = false
        };
        _statusRow.Controls.Add(_discoverySpinner, 0, 0);
        _statusRow.Controls.Add(_discoveryStatusLabel, 1, 0);
        _statusRow.ResumeLayout(false);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        _layout.SuspendLayout();
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(_topRow, 0, 0);
        _layout.Controls.Add(_statusRow, 0, 1);
        _layout.Controls.Add(_customRow, 0, 2);

        Controls.Add(_layout);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(0, 36);

        _topRow.ResumeLayout(false);
        _customRow.ResumeLayout(false);
        _statusRow.ResumeLayout(false);
        _layout.ResumeLayout(false);
        ResumeLayout(false);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _discovery.NetworkResultsCompleted += DiscoveryOnNetworkResultsCompleted;
        SetDiscoveredInstances(Array.Empty<string>(), null);
    }

    internal HiveComboBox ServerSelector => _serverComboBox;

    internal TextBox CustomServerInput => _customServerTextBox;

    internal TableLayoutPanel CustomRow => _customRow;

    internal TextBox PortInput => _portTextBox;

    internal ProgressBar DiscoverySpinner => _discoverySpinner;

    internal Label DiscoveryStatusLabel => _discoveryStatusLabel;

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
        set => SetPortValue(value, automaticallyDefaulted: false);
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
            try
            {
                ApplyPortDefaultForSelection();
                UpdateCustomVisibility();
            }
            finally
            {
                _applyingValue = false;
            }
        }
    }

    public void SetDiscoveredInstances(
        IReadOnlyList<string> instances,
        string? preferredServer,
        bool preserveCustomSelection = false)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var preferred = preferredServer?.Trim() ?? string.Empty;
        var currentCustomServer = _customServerTextBox.Text;
        _applyingValue = true;
        try
        {
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

            if (preserveCustomSelection)
            {
                _serverComboBox.SelectedIndex = _serverComboBox.Items.Count - 1;
                _customServerTextBox.Text = currentCustomServer;
            }
            else if (!string.IsNullOrWhiteSpace(preferred))
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
                else if (string.Equals(
                             preferred,
                             "localhost",
                             StringComparison.OrdinalIgnoreCase))
                {
                    // Recover from older configurations that represented an installed
                    // named local instance as bare "localhost".
                    var localInstance = _serverComboBox.Items
                        .OfType<ServerChoice>()
                        .FirstOrDefault(
                            static choice =>
                                !choice.IsCustom &&
                                (string.Equals(
                                     choice.DisplayName,
                                     "localhost",
                                     StringComparison.OrdinalIgnoreCase) ||
                                 choice.DisplayName.StartsWith(
                                     @"localhost\",
                                     StringComparison.OrdinalIgnoreCase)));

                    if (localInstance is not null)
                    {
                        _serverComboBox.SelectedItem = localInstance;
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
                    _serverComboBox.SelectedIndex = _serverComboBox.Items.Count - 1;
                    _customServerTextBox.Text = preferred;
                }
            }
            else
            {
                var localInstance = _serverComboBox.Items
                    .OfType<ServerChoice>()
                    .FirstOrDefault(
                        static choice =>
                            !choice.IsCustom &&
                            (string.Equals(
                                 choice.DisplayName,
                                 "localhost",
                                 StringComparison.OrdinalIgnoreCase) ||
                             choice.DisplayName.StartsWith(
                                 @"localhost\",
                                 StringComparison.OrdinalIgnoreCase)));

                _serverComboBox.SelectedItem =
                    localInstance ??
                    _serverComboBox.Items
                        .OfType<ServerChoice>()
                        .FirstOrDefault(static choice => !choice.IsCustom);
            }
        }
        finally
        {
            try
            {
                ApplyPortDefaultForSelection();
                UpdateCustomVisibility();
            }
            finally
            {
                _applyingValue = false;
            }
        }
    }

    private void ApplyPortDefaultForSelection()
    {
        var server = IsCustomSelected
            ? _customServerTextBox.Text.Trim()
            : ServerName;

        if (server.Contains('\\', StringComparison.Ordinal))
        {
            // Named instances use instance resolution unless the user explicitly
            // entered a port. Remove only the picker's automatic 1433 default.
            if (_portWasAutomaticallyDefaulted && Port == 1433)
                SetPortValue(null, automaticallyDefaulted: true);

            return;
        }

        if (Port is null && string.IsNullOrWhiteSpace(_portTextBox.Text))
            SetPortValue(1433, automaticallyDefaulted: true);
    }

    private void SetPortValue(int? value, bool automaticallyDefaulted)
    {
        _settingPort = true;
        try
        {
            _portTextBox.Text = value is > 0
                ? value.Value.ToString()
                : string.Empty;
        }
        finally
        {
            _settingPort = false;
        }

        _portWasAutomaticallyDefaulted = automaticallyDefaulted;
    }

    public async Task<SqlServerInstanceDiscoveryResult> RefreshAsync(
        string? preferredServer = null,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        if (IsDisposed || Disposing)
        {
            return new SqlServerInstanceDiscoveryResult(
                Array.Empty<string>(),
                IsComplete: false,
                IsTimedOut: false,
                IsFromCache: false,
                NetworkScanStillRunning: false,
                StatusMessage: "Discovery was cancelled; Custom... remains available.");
        }

        var generation = Interlocked.Increment(ref _discoveryGeneration);
        var snapshot = new DiscoverySnapshot(
            generation,
            _userEditVersion,
            preferredServer?.Trim() ?? ServerName,
            SynchronizationContext.Current);
        Volatile.Write(ref _activeDiscoverySnapshot, snapshot);

        var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(ref _activeDiscoveryCts, requestCts);
        CancelSafely(previous);
        SetDiscoveryPresentation(true, HiveSqlServerInstanceDiscovery.SearchingStatusMessage);

        var progress = new DiscoveryProgress(update => PostDiscoveryUpdate(update, snapshot));
        try
        {
            var result = await _discovery
                .DiscoverAsync(progress, forceRefresh, requestCts.Token)
                .ConfigureAwait(true);
            if (!result.NetworkScanStillRunning)
                Interlocked.CompareExchange(ref _activeDiscoverySnapshot, null, snapshot);
            return result;
        }
        catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
        {
            if (generation == Volatile.Read(ref _discoveryGeneration) && !IsDisposed && !Disposing)
            {
                Interlocked.Increment(ref _discoveryGeneration);
                Interlocked.CompareExchange(ref _activeDiscoverySnapshot, null, snapshot);
                SetDiscoveryPresentation(false, "Discovery cancelled; available instances remain selectable.");
            }
            throw;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("SQL Server discovery request failed ({0}).", exception.GetType().Name);
            var failure = new SqlServerInstanceDiscoveryResult(
                Array.Empty<string>(),
                IsComplete: false,
                IsTimedOut: false,
                IsFromCache: false,
                NetworkScanStillRunning: false,
                StatusMessage: "Discovery failed; refresh or enter Custom...");
            if (generation == Volatile.Read(ref _discoveryGeneration) && !IsDisposed && !Disposing)
            {
                Interlocked.CompareExchange(ref _activeDiscoverySnapshot, null, snapshot);
                SetDiscoveryPresentation(false, failure.StatusMessage);
            }
            return failure;
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeDiscoveryCts, null, requestCts);
            requestCts.Dispose();
        }
    }

    private void DiscoveryOnNetworkResultsCompleted(SqlServerInstanceDiscoveryResult result)
    {
        var snapshot = Volatile.Read(ref _activeDiscoverySnapshot);
        if (snapshot is null)
            return;
        PostDiscoveryUpdate(
            new SqlServerInstanceDiscoveryUpdate(
                SqlServerInstanceDiscoveryUpdateKind.Completed,
                result,
                IsSearching: false),
            snapshot);
    }

    private void PostDiscoveryUpdate(
        SqlServerInstanceDiscoveryUpdate update,
        DiscoverySnapshot snapshot)
    {
        void ApplyUpdate()
        {
            if (IsDisposed || Disposing ||
                snapshot.Generation != Volatile.Read(ref _discoveryGeneration))
                return;

            var editedDuringDiscovery = _userEditVersion != snapshot.UserEditVersion;
            var preferred = editedDuringDiscovery ? ServerName : snapshot.PreferredServer;
            var preserveCustom = editedDuringDiscovery && IsCustomSelected;
            SetDiscoveredInstances(update.Result.Instances, preferred, preserveCustom);
            SetDiscoveryPresentation(update.IsSearching, update.Result.StatusMessage);
        }

        try
        {
            if (snapshot.SynchronizationContext is not null)
            {
                snapshot.SynchronizationContext.Post(
                    static state => ((Action)state!).Invoke(),
                    (Action)ApplyUpdate);
                return;
            }
            if (!IsHandleCreated || IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                BeginInvoke((Action)ApplyUpdate);
            else
                ApplyUpdate();
        }
        catch (ObjectDisposedException)
        {
            Trace.TraceInformation("Late SQL Server discovery update ignored after picker disposal.");
        }
        catch (InvalidAsynchronousStateException)
        {
            Trace.TraceInformation("Late SQL Server discovery update ignored after UI-context shutdown.");
        }
    }

    internal void SetDiscoveryPresentation(bool isSearching, string statusMessage)
    {
        if (IsDisposed || Disposing)
            return;
        var hasStatus = !string.IsNullOrWhiteSpace(statusMessage);
        var changed = _statusRow.Visible != hasStatus ||
                      _discoverySpinner.Visible != isSearching ||
                      _discoveryStatusLabel.Visible != hasStatus;
        _discoveryStatusLabel.Text = statusMessage;
        _discoveryStatusLabel.Visible = hasStatus;
        _discoverySpinner.Visible = isSearching;
        _statusRow.Visible = hasStatus;
        if (changed)
        {
            _statusRow.PerformLayout();
            _layout.PerformLayout();
            PerformLayout();
        }
    }

    internal void SetDiscoveryFailureStatus() =>
        SetDiscoveryPresentation(false, "Discovery failed; refresh or enter Custom...");

    internal void CancelPendingDiscovery()
    {
        Interlocked.Increment(ref _discoveryGeneration);
        Interlocked.Exchange(ref _activeDiscoverySnapshot, null);
        CancelSafely(Interlocked.Exchange(ref _activeDiscoveryCts, null));
        if (!IsDisposed && !Disposing)
            SetDiscoveryPresentation(false, string.Empty);
    }

    private static void CancelSafely(CancellationTokenSource? source)
    {
        if (source is null)
            return;
        try { source.Cancel(); }
        catch (ObjectDisposedException)
        {
            Trace.TraceInformation("SQL Server discovery wait completed before cancellation.");
        }
    }

    private sealed class DiscoveryProgress(
        Action<SqlServerInstanceDiscoveryUpdate> report) :
        IProgress<SqlServerInstanceDiscoveryUpdate>
    {
        public void Report(SqlServerInstanceDiscoveryUpdate value) => report(value);
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

        if (!custom && _customServerTextBox.TextLength > 0)
            _customServerTextBox.Clear();

        if (_customRow.Visible == custom &&
            _customServerTextBox.Visible == custom)
        {
            return;
        }

        SuspendLayout();
        _layout.SuspendLayout();
        _customRow.SuspendLayout();
        try
        {
            _customRow.Visible = custom;
            _customServerTextBox.Visible = custom;
            _layout.RowStyles[2].SizeType = SizeType.AutoSize;
        }
        finally
        {
            _customRow.ResumeLayout(false);
            _layout.ResumeLayout(true);
            ResumeLayout(true);
        }
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        _themeManager.Apply(this);

    private static TextBox CreateTextBox(bool autoSize = false) =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            AutoSize = autoSize,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty
        };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _discovery.NetworkResultsCompleted -= DiscoveryOnNetworkResultsCompleted;
            Interlocked.Increment(ref _discoveryGeneration);
            Interlocked.Exchange(ref _activeDiscoverySnapshot, null);
            CancelSafely(Interlocked.Exchange(ref _activeDiscoveryCts, null));
        }

        base.Dispose(disposing);
    }
}
