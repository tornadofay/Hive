using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderModelDiscoveryPanel : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ProviderId _providerId;
    private readonly ProviderAccountId _providerAccountId;
    private readonly Uri? _endpoint;
    private readonly string? _initialModelId;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveComboBox _modelSelector;
    private readonly Label _statusLabel;
    private readonly Label _metadataLabel;
    private readonly Label _capabilitiesLabel;
    private CancellationTokenSource? _discoveryCts;
    private IReadOnlyList<ProviderModelMetadata> _models = Array.Empty<ProviderModelMetadata>();
    private int _requestVersion;
    private bool _configurationChanged;
    private bool _customModelEntry;

    internal HiveProviderModelDiscoveryPanel(
        IHiveManagementFacade management,
        ProviderId providerId,
        ProviderAccountId providerAccountId,
        Uri? endpoint,
        string? initialModelId,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));

        if (providerId == default)
            throw new ArgumentException(
                "Provider identity is required.",
                nameof(providerId));

        if (providerAccountId == default)
            throw new ArgumentException(
                "Provider account identity is required.",
                nameof(providerAccountId));

        if (endpoint is not null &&
            (!endpoint.IsAbsoluteUri ||
             (endpoint.Scheme != Uri.UriSchemeHttp &&
              endpoint.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException(
                "Discovery endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }

        _providerId = providerId;
        _providerAccountId = providerAccountId;
        _endpoint = endpoint;
        _initialModelId = string.IsNullOrWhiteSpace(initialModelId)
            ? null
            : initialModelId.Trim();
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(0, 2, 0, 2);
        MinimumSize = new Size(0, 138);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Text = "Discovery has not started.",
            AccessibleName = "Provider model discovery status"
        };

        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        statusRow.Controls.Add(_statusLabel, 0, 0);

        _modelSelector = new HiveComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            Height = 32,
            Margin = new Padding(0, 4, 0, 4),
            AccessibleName = "Provider model",
            AccessibleDescription =
                "Select a discovered model or type a custom model identifier."
        };
        _modelSelector.SelectedIndexChanged += ModelSelectorOnSelectedIndexChanged;
        _modelSelector.SelectionChangeCommitted += ModelSelectorOnSelectionChangeCommitted;
        _modelSelector.TextChanged += ModelSelectorOnTextChanged;

        var selectionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        selectionRow.Controls.Add(_modelSelector, 0, 0);

        _metadataLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 34,
            Margin = new Padding(0, 4, 0, 0),
            Padding = Padding.Empty,
            Text = "Provider availability: Unknown • Health: Unknown",
            AccessibleName = "Provider model discovery operational metadata"
        };

        _capabilitiesLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = new Padding(0, 4, 0, 0),
            Padding = Padding.Empty,
            Text = _endpoint is null
                ? "Enter an endpoint to load provider model suggestions."
                : "Loading provider model suggestions...",
            AccessibleName = "Discovered model capabilities"
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 2)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(statusRow, 0, 0);
        root.Controls.Add(selectionRow, 0, 1);
        root.Controls.Add(_metadataLabel, 0, 2);
        root.Controls.Add(_capabilitiesLabel, 0, 3);

        Controls.Add(root);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme(_themeManager.Theme);
        SetNoDiscoveryState();
    }


    internal HiveComboBox ModelSelector => _modelSelector;

    internal event EventHandler? DiscoveryUpdated;

    internal ProviderDiscoverySnapshot? CurrentSnapshot { get; private set; }

    internal ProviderModelMetadata? SelectedModel { get; private set; }

    internal Label StatusLabel => _statusLabel;

    internal Label MetadataLabel => _metadataLabel;

    internal IReadOnlyList<ProviderModelMetadata> Models => _models;

    internal async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        await DiscoverAsync(forceRefresh: false, cancellationToken).ConfigureAwait(true);

    internal async Task RefreshAsync(CancellationToken cancellationToken = default) =>
        await DiscoverAsync(forceRefresh: true, cancellationToken).ConfigureAwait(true);

    internal void MarkEndpointConfigurationChanged()
    {
        if (_configurationChanged)
            return;

        _configurationChanged = true;
        Interlocked.Increment(ref _requestVersion);
        var discoveryCts = Interlocked.Exchange(ref _discoveryCts, null);
        discoveryCts?.Cancel();

        ClearModels();
        SetStatus(
            "The endpoint changed. Save and reopen the target to load model suggestions for the new endpoint.",
            HiveStatusTone.Warning);
        _metadataLabel.Text = "Model suggestions apply to the endpoint being edited.";
        _capabilitiesLabel.Text = "Save and reopen the target to load suggestions for the new endpoint.";
    }

    private async Task DiscoverAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (_configurationChanged)
        {
            SetStatus(
                "Save the target before refreshing model discovery.",
                HiveStatusTone.Warning);
            return;
        }

        var requestVersion = Interlocked.Increment(ref _requestVersion);
        var discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(ref _discoveryCts, discoveryCts);
        previous?.Cancel();

        SetBusyState(true);

        try
        {
            if (_endpoint is null)
            {
                SetStatus(
                    "Enter an endpoint to load provider model suggestions.",
                    HiveStatusTone.Information);
                return;
            }

            var result = await _management
                .GetProviderDiscoveryAsync(
                    _providerId,
                    _providerAccountId,
                    _endpoint,
                    _accessContext,
                    forceRefresh,
                    discoveryCts.Token)
                .ConfigureAwait(true);

            if (discoveryCts.IsCancellationRequested ||
                IsDisposed ||
                Disposing ||
                requestVersion != Volatile.Read(ref _requestVersion))
            {
                return;
            }

            if (result.IsFailure)
            {
                ClearModels();
                var error = result.Error!;
                SetStatus(
                    $"Discovery failed ({error.Code}). Manual model entry remains available.",
                    HiveStatusTone.Error);
                _metadataLabel.Text = "Operational metadata is unavailable because discovery failed.";
                _capabilitiesLabel.Text = "No discovered model metadata is currently available.";
                ReportFailure(error.Message);
                return;
            }

            var snapshot = result.Value!;
            if (!forceRefresh &&
                snapshot.IsStale(DateTimeOffset.UtcNow))
            {
                SetStatus(
                    "Discovery data is stale. Refreshing provider model metadata...",
                    HiveStatusTone.Warning);
                await DiscoverAsyncCore(
                    requestVersion,
                    discoveryCts).ConfigureAwait(true);
                return;
            }

            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
            when (discoveryCts.IsCancellationRequested ||
                  IsDisposed ||
                  Disposing)
        {
        }
        catch (Exception exception)
        {
            if (requestVersion != Volatile.Read(ref _requestVersion) ||
                IsDisposed ||
                Disposing)
            {
                return;
            }

            ClearModels();
            SetStatus(
                "Discovery failed. Manual model entry remains available.",
                HiveStatusTone.Error);
            _metadataLabel.Text = "Operational metadata is unavailable because discovery failed.";
            _capabilitiesLabel.Text = "No discovered model metadata is currently available.";
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Provider Model Discovery",
                "Provider model discovery could not be completed.",
                _output,
                _themeManager);
        }
        finally
        {
            if (ReferenceEquals(_discoveryCts, discoveryCts))
            {
                Interlocked.CompareExchange(
                    ref _discoveryCts,
                    null,
                    discoveryCts);
            }

            discoveryCts.Dispose();

            if (!IsDisposed && !Disposing &&
                requestVersion == Volatile.Read(ref _requestVersion))
            {
                    DiscoveryUpdated?.Invoke(this, EventArgs.Empty);
            }

            SetBusyState(false);
        }
    }

    private async Task DiscoverAsyncCore(
        int requestVersion,
        CancellationTokenSource discoveryCts)
    {
        if (_endpoint is null)
            return;

        var result = await _management
            .GetProviderDiscoveryAsync(
                _providerId,
                _providerAccountId,
                _endpoint,
                _accessContext,
                forceRefresh: true,
                discoveryCts.Token)
            .ConfigureAwait(true);

        if (discoveryCts.IsCancellationRequested ||
            IsDisposed ||
            Disposing ||
            requestVersion != Volatile.Read(ref _requestVersion))
        {
            return;
        }

        if (result.IsFailure)
        {
            var error = result.Error!;
            ClearModels();
            SetStatus(
                $"Discovery failed ({error.Code}). Manual model entry remains available.",
                HiveStatusTone.Error);
            _metadataLabel.Text = "Operational metadata is unavailable because discovery failed.";
            _capabilitiesLabel.Text = "No discovered model metadata is currently available.";
            ReportFailure(error.Message);
            return;
        }

        ApplySnapshot(result.Value!);
    }

    private void ApplySnapshot(ProviderDiscoverySnapshot snapshot)
    {
        _configurationChanged = false;

        if (snapshot.ModelEnumerationState == ProviderDiscoveryState.Unsupported)
        {
            ClearModels();
            SetStatus(
                "Model discovery is not supported by this provider. Enter a model manually.",
                HiveStatusTone.Warning);
            _metadataLabel.Text = BuildOperationalMetadata(snapshot.Operational);
            _capabilitiesLabel.Text = "No discovered model metadata is available.";
            return;
        }

        if (snapshot.ModelEnumerationState == ProviderDiscoveryState.Unknown)
        {
            ClearModels();
            SetStatus(
                "Provider model discovery is currently unknown. Enter a model manually or refresh.",
                HiveStatusTone.Warning);
            _metadataLabel.Text = BuildOperationalMetadata(snapshot.Operational);
            _capabilitiesLabel.Text = "No discovered model metadata is available.";
            return;
        }

        CurrentSnapshot = snapshot;
        SelectedModel = null;
        _models = snapshot.Models;
        _modelSelector.SuspendLayout();
        try
        {
            _modelSelector.Items.Clear();
            foreach (var model in _models)
                _modelSelector.Items.Add(new ModelChoice(model));

            var initialIndex = _initialModelId is null
                ? -1
                : _models
                    .Select((model, index) => (model, index))
                    .Where(item =>
                        string.Equals(
                            item.model.ModelId,
                            _initialModelId,
                            StringComparison.Ordinal))
                    .Select(item => item.index)
                    .DefaultIfEmpty(-1)
                    .First();

            _modelSelector.SelectedIndex = initialIndex;
            if (initialIndex < 0)
                _modelSelector.Text = _initialModelId ?? string.Empty;
        }
        finally
        {
            _modelSelector.ResumeLayout(true);
        }

        var stale = snapshot.IsStale(DateTimeOffset.UtcNow);
        SetStatus(
            stale
                ? $"Discovered {_models.Count} model(s). Metadata is stale; refresh recommended."
                : $"Discovered {_models.Count} model(s).",
            stale ? HiveStatusTone.Warning : HiveStatusTone.Success);
        _metadataLabel.Text = BuildOperationalMetadata(snapshot.Operational);
        _capabilitiesLabel.Text = _models.Count == 0
            ? "No models were returned. Manual model entry remains available."
            : "Select a discovered model or type a custom model identifier.";
        UpdateSelectionState();
    }

    private static string BuildOperationalMetadata(
        ProviderOperationalMetadata operational)
    {
        var rateLimit = operational.RateLimitRemaining is { } remaining
            ? $" • Rate limit remaining: {remaining}"
            : string.Empty;

        return
            $"Provider availability: {operational.Availability} • Health: {operational.Health} • " +
            $"Observed: {operational.ObservedAtUtc:yyyy-MM-dd HH:mm:ss} UTC • " +
            $"Stale after: {operational.StaleAfterUtc:yyyy-MM-dd HH:mm:ss} UTC{rateLimit}";
    }

    private void ModelSelectorOnSelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        UpdateSelectionState();

        if (_customModelEntry)
        {
            SelectedModel = null;
            return;
        }

        if (_modelSelector.SelectedItem is ModelChoice choice &&
            string.Equals(
                choice.Value.ModelId,
                _modelSelector.Text,
                StringComparison.Ordinal))
        {
            SelectedModel = choice.Value;
            _capabilitiesLabel.Text =
                $"Availability: {choice.Value.Availability} • Health: {choice.Value.Health} • " +
                $"Capabilities: {BuildCapabilities(choice.Value.DiscoveredCapabilities)}";

            ModelSelected?.Invoke(
                this,
                new ProviderModelSelectedEventArgs(choice.Value));
        }
        else
        {
            SelectedModel = null;

            if (_models.Count > 0)
            {
                _capabilitiesLabel.Text =
                    "Type a custom model identifier or select a discovered model.";
            }
        }
    }

    private void ModelSelectorOnSelectionChangeCommitted(
        object? sender,
        EventArgs e)
    {
        _customModelEntry = false;
        ModelSelectorOnSelectedIndexChanged(sender, e);
    }

    private void ModelSelectorOnTextChanged(
        object? sender,
        EventArgs e)
    {
        if (_modelSelector.SelectedItem is ModelChoice choice &&
            string.Equals(
                choice.Value.ModelId,
                _modelSelector.Text,
                StringComparison.Ordinal))
        {
            _customModelEntry = false;
            return;
        }

        _customModelEntry = true;
        SelectedModel = null;
    }

    private void SetNoDiscoveryState()
    {
        if (_models.Count != 0)
            return;

        _modelSelector.Items.Clear();
        _modelSelector.SelectedIndex = -1;
        _modelSelector.Enabled = false;
        _modelSelector.Text = _initialModelId ?? string.Empty;
        _capabilitiesLabel.Text = _endpoint is null
            ? "Enter an endpoint to load provider model suggestions."
            : "Loading provider model suggestions...";
    }

    private void ClearModels()
    {
        _models = Array.Empty<ProviderModelMetadata>();
        _modelSelector.SuspendLayout();
        try
        {
            _modelSelector.Items.Clear();
            _modelSelector.SelectedIndex = -1;
        }
        finally
        {
            _modelSelector.ResumeLayout(true);
        }

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        var available = !_configurationChanged &&
            (_discoveryCts is null || _discoveryCts.IsCancellationRequested);
        _modelSelector.Enabled = available;
    }

    private void SetBusyState(bool busy)
    {
        if (IsDisposed || Disposing)
            return;

        _modelSelector.Enabled = !busy && !_configurationChanged;

        if (busy)
        {
            SetStatus(
                "Discovering provider models...",
                HiveStatusTone.Information);
        }
        else
        {
            ApplyStatusTheme(_themeManager.Theme);
        }
    }

    private HiveStatusTone CurrentStatusTone =>
        _statusLabel.Tag is HiveStatusTone tone
            ? tone
            : HiveStatusTone.Neutral;

    private void SetStatus(
        string text,
        HiveStatusTone tone)
    {
        _statusLabel.Text = text;
        _statusLabel.Tag = tone;
        ApplyStatusTheme(_themeManager.Theme);
    }

    private void ApplyStatusTheme(HiveThemeDefinition theme)
    {
        _statusLabel.ForeColor = CurrentStatusTone switch
        {
            HiveStatusTone.Information => theme.VisualStates.Information,
            HiveStatusTone.Success => theme.VisualStates.Success,
            HiveStatusTone.Warning => theme.VisualStates.Warning,
            HiveStatusTone.Error => theme.VisualStates.Error,
            _ => theme.Palette.MutedText
        };
        _metadataLabel.ForeColor = theme.Palette.MutedText;
        _capabilitiesLabel.ForeColor = theme.Palette.MutedText;
    }

    private void ReportFailure(string message)
    {
        if (FindForm() is IWin32Window owner)
        {
            HiveUiErrorReporter.Report(
                owner,
                message,
                "Provider Model Discovery",
                _output,
                _themeManager);
            return;
        }

        _output?.Write("ERROR", $"Provider Model Discovery: {message}");
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        ApplyTheme(_themeManager.Theme);

    private void ApplyTheme(HiveThemeDefinition theme)
    {
        BackColor = theme.Palette.Surface;
        ApplyStatusTheme(theme);
    }

    private static string BuildCapabilities(
        IReadOnlyList<CapabilityStateEntry> capabilities) =>
        capabilities.Count == 0
            ? "none reported"
            : string.Join(
                ", ",
                capabilities.Select(
                    capability => $"{capability.Capability.Value}={capability.State}"));

    public event EventHandler<ProviderModelSelectedEventArgs>? ModelSelected;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _modelSelector.SelectedIndexChanged -= ModelSelectorOnSelectedIndexChanged;
            _modelSelector.SelectionChangeCommitted -= ModelSelectorOnSelectionChangeCommitted;
            _modelSelector.TextChanged -= ModelSelectorOnTextChanged;

            var discoveryCts = Interlocked.Exchange(ref _discoveryCts, null);
            discoveryCts?.Cancel();
        }

        base.Dispose(disposing);
    }

    private sealed record ModelChoice(ProviderModelMetadata Value)
    {
        public override string ToString() => Value.ModelId;
    }
}

internal sealed class ProviderModelSelectedEventArgs : EventArgs
{
    public ProviderModelSelectedEventArgs(ProviderModelMetadata model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ProviderModelMetadata Model { get; }
}
