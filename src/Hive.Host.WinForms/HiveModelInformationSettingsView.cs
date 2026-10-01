using System.Drawing;
using System.Text.Json;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveModelInformationSettingsView : UserControl, IHiveAdvancedConfigurationPage
{
    private sealed record ProviderChoice(Provider Value)
    {
        public override string ToString() => Value.DisplayName;
    }

    private sealed record AccountChoice(ProviderAccount Value)
    {
        public override string ToString() => Value.DisplayName;
    }

    private sealed record EndpointChoice(Uri Endpoint, string Source)
    {
        public override string ToString() => Endpoint.AbsoluteUri;
    }

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveComboBox _providerComboBox;
    private readonly HiveComboBox _accountComboBox;
    private readonly HiveComboBox _endpointComboBox;
    private readonly HiveButton _refreshButton;
    private readonly Label _statusLabel;
    private readonly ListView _modelsList;
    private readonly TextBox _detailsBox;
    private readonly Label _contextLabel;
    private CancellationTokenSource? _operationCts;
    private int _operationVersion;
    private bool _initializingContext;
    private Provider? _selectedProvider;
    private ProviderAccount? _selectedAccount;
    private Uri? _selectedEndpoint;
    private ProviderDiscoverySnapshot? _snapshot;

    public HiveModelInformationSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        var contextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 96,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 8)
        };
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));

        contextPanel.Controls.Add(CreateContextLabel("Provider"), 0, 0);
        contextPanel.Controls.Add(CreateContextLabel("Account / Credential"), 1, 0);
        contextPanel.Controls.Add(CreateContextLabel("Discovery endpoint"), 2, 0);
        contextPanel.Controls.Add(CreateContextLabel(string.Empty), 3, 0);

        _providerComboBox = CreateSelector("Provider");
        _accountComboBox = CreateSelector("Account / Credential");
        _endpointComboBox = CreateSelector("Discovery endpoint");
        _endpointComboBox.DropDownStyle = ComboBoxStyle.DropDown;

        _refreshButton = new HiveButton
        {
            Dock = DockStyle.Fill,
            Text = "Refresh",
            Style = HiveButtonStyle.Secondary,
            AccessibleName = "Refresh model information"
        };

        contextPanel.Controls.Add(_providerComboBox, 0, 1);
        contextPanel.Controls.Add(_accountComboBox, 1, 1);
        contextPanel.Controls.Add(_endpointComboBox, 2, 1);
        contextPanel.Controls.Add(_refreshButton, 3, 1);

        _contextLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 34,
            Text = "Select a Provider and Account, choose a saved endpoint, or enter an HTTP/HTTPS endpoint, then Refresh.",
            AutoEllipsis = true,
            Padding = new Padding(4, 6, 4, 4)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 30,
            Text = "Model discovery has not started.",
            AutoEllipsis = true,
            Padding = new Padding(4, 4, 4, 2)
        };

        _modelsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Discovered provider models"
        };
        _modelsList.Columns.Add("Model", 240);
        _modelsList.Columns.Add("Owner", 150);
        _modelsList.Columns.Add("Availability", 110);
        _modelsList.Columns.Add("Health", 100);

        _detailsBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Selected model information"
        };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 430,
            IsSplitterFixed = false
        };
        split.Panel1.Padding = new Padding(0, 4, 8, 0);
        split.Panel2.Padding = new Padding(8, 4, 0, 0);
        split.Panel1.Controls.Add(_modelsList);
        split.Panel2.Controls.Add(_detailsBox);

        Controls.Add(split);
        Controls.Add(_statusLabel);
        Controls.Add(_contextLabel);
        Controls.Add(contextPanel);

        _providerComboBox.SelectedIndexChanged += ProviderChanged;
        _accountComboBox.SelectedIndexChanged += AccountChanged;
        _endpointComboBox.SelectedIndexChanged += EndpointChanged;
        _endpointComboBox.TextChanged += EndpointTextChanged;
        _refreshButton.Click += RefreshButtonOnClick;

        _modelsList.SelectedIndexChanged += ModelsListSelected;

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme();
    }

    internal HiveComboBox ProviderSelector => _providerComboBox;

    internal HiveComboBox AccountSelector => _accountComboBox;

    internal HiveComboBox EndpointSelector => _endpointComboBox;

    internal ListView ModelsList => _modelsList;

    internal TextBox DetailsBox => _detailsBox;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _management
            .ListProvidersAsync(
                _accessContext,
                includeRetired: false,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        _initializingContext = true;
        try
        {
            SetItems(
                _providerComboBox,
                result.Value!.Select(item => new ProviderChoice(item)));
            _providerComboBox.SelectedIndex =
                _providerComboBox.Items.Count > 0 ? 0 : -1;
        }
        finally
        {
            _initializingContext = false;
        }

        await LoadAccountsAndEndpointsAsync(cancellationToken)
            .ConfigureAwait(true);
    }

    private async void ProviderChanged(object? sender, EventArgs e)
    {
        if (_initializingContext || IsDisposed || Disposing)
            return;

        try
        {
            await LoadAccountsAndEndpointsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_operationCts?.IsCancellationRequested == true || IsDisposed || Disposing)
        {
        }
        catch (Exception exception)
        {
            ReportError(
                exception,
                "The account context could not be loaded.");
        }
    }

    private async void AccountChanged(object? sender, EventArgs e)
    {
        if (_initializingContext || IsDisposed || Disposing)
            return;

        try
        {
            await LoadEndpointsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (_operationCts?.IsCancellationRequested == true || IsDisposed || Disposing)
        {
        }
        catch (Exception exception)
        {
            ReportError(
                exception,
                "The endpoint context could not be loaded.");
        }
    }

    private async void RefreshButtonOnClick(object? sender, EventArgs e)
    {
        try
        {
            await RefreshDiscoveryAsync(forceRefresh: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (IsDisposed || Disposing)
        {
        }
        catch (Exception exception)
        {
            ReportError(
                exception,
                "Provider model information could not be refreshed.");
        }
    }

    private void EndpointTextChanged(object? sender, EventArgs e)
    {
        if (_initializingContext || IsDisposed || Disposing)
            return;

        var parsed = TryParseEndpoint(_endpointComboBox.Text);
        if (parsed is not null &&
            _selectedEndpoint is not null &&
            EndpointsEqual(parsed, _selectedEndpoint))
        {
            return;
        }

        _selectedEndpoint = null;
        ClearObservation();
        UpdateContextLabel();
        SetStatus(
            "Endpoint changed. Refresh to inspect model information for the entered endpoint.",
            HiveStatusTone.Information);
    }

    private void EndpointChanged(object? sender, EventArgs e)
    {
        if (_initializingContext || IsDisposed || Disposing)
            return;

        _selectedEndpoint =
            (_endpointComboBox.SelectedItem as EndpointChoice)?.Endpoint;

        ClearObservation();
        UpdateContextLabel();

        if (_selectedEndpoint is null)
            return;

        _ = LoadCachedOrDiscoverAsync();
    }

    private async Task LoadAccountsAndEndpointsAsync(
        CancellationToken cancellationToken = default)
    {
        CancelOperation();
        ClearObservation();

        _selectedProvider =
            (_providerComboBox.SelectedItem as ProviderChoice)?.Value;

        _initializingContext = true;
        try
        {
            var accounts = _selectedProvider is null
                ? Result<IReadOnlyList<ProviderAccount>>.Success([])
                : await _management
                    .ListProviderAccountsAsync(
                        _selectedProvider.Id,
                        _accessContext,
                        includeRetired: false,
                        cancellationToken)
                    .ConfigureAwait(true);

            if (accounts.IsFailure)
                throw new InvalidOperationException(accounts.Error!.Message);

            SetItems(
                _accountComboBox,
                accounts.Value!.Select(item => new AccountChoice(item)));
            _accountComboBox.SelectedIndex =
                _accountComboBox.Items.Count > 0 ? 0 : -1;
        }
        finally
        {
            _initializingContext = false;
        }

        await LoadEndpointsAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadEndpointsAsync(
        CancellationToken cancellationToken = default)
    {
        ClearObservation();

        _selectedAccount =
            (_accountComboBox.SelectedItem as AccountChoice)?.Value;

        _initializingContext = true;
        try
        {
            var endpoints = new List<EndpointChoice>();

            if (_selectedAccount is not null)
            {
                var targets = await _management
                    .ListExecutionTargetsAsync(
                        _selectedAccount.Id,
                        _accessContext,
                        includeRetired: false,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (targets.IsFailure)
                    throw new InvalidOperationException(targets.Error!.Message);

                endpoints.AddRange(
                    targets.Value!
                        .Select(target => new EndpointChoice(
                            target.Endpoint,
                            "Execution Target")));
            }

            if (_selectedProvider is not null &&
                BuiltInProviderCatalog.Find(_selectedProvider.Key)?.DefaultEndpoint is { } defaultEndpoint)
            {
                endpoints.Add(
                    new EndpointChoice(
                        defaultEndpoint,
                        "Built-in provider default"));
            }

            var unique = endpoints
                .GroupBy(item => item.Endpoint.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Endpoint.AbsoluteUri, StringComparer.Ordinal)
                .ToArray();

            SetItems(_endpointComboBox, unique);
            _endpointComboBox.SelectedIndex =
                _endpointComboBox.Items.Count > 0 ? 0 : -1;
            _selectedEndpoint =
                (_endpointComboBox.SelectedItem as EndpointChoice)?.Endpoint;
        }
        finally
        {
            _initializingContext = false;
        }

        UpdateContextLabel();

        if (_selectedEndpoint is not null)
            await LoadCachedOrDiscoverAsync().ConfigureAwait(true);
    }

    private async Task LoadCachedOrDiscoverAsync()
    {
        if (_selectedProvider is null ||
            _selectedAccount is null ||
            _selectedEndpoint is null)
        {
            return;
        }

        await RefreshDiscoveryAsync(forceRefresh: false).ConfigureAwait(true);
    }

    private async Task RefreshDiscoveryAsync(bool forceRefresh)
    {
        _selectedEndpoint = TryParseEndpoint(_endpointComboBox.Text);

        if (_selectedProvider is null ||
            _selectedAccount is null ||
            _selectedEndpoint is null)
        {
            SetStatus(
                "Select a Provider, Account, and endpoint first.",
                HiveStatusTone.Warning);
            return;
        }

        var version = Interlocked.Increment(ref _operationVersion);
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _operationCts, cts);
        previous?.Cancel();

        _refreshButton.Enabled = false;
        SetStatus(
            forceRefresh
                ? "Refreshing provider model information..."
                : "Loading provider model information...",
            HiveStatusTone.Information);

        try
        {
            var result = await _management
                .GetProviderDiscoveryAsync(
                    _selectedProvider.Id,
                    _selectedAccount.Id,
                    _selectedEndpoint,
                    _accessContext,
                    forceRefresh,
                    cts.Token)
                .ConfigureAwait(true);

            if (cts.IsCancellationRequested ||
                version != Volatile.Read(ref _operationVersion) ||
                IsDisposed ||
                Disposing)
            {
                return;
            }

            if (result.IsFailure)
            {
                SetStatus(
                    $"Discovery did not complete ({result.Error!.Code}). " +
                    (_snapshot is null
                        ? "No successful model observation is available."
                        : "The last successful model observation is retained."),
                    _snapshot is null ? HiveStatusTone.Error : HiveStatusTone.Warning);
                ApplyOperationalContext();
                ReportError(
                    new InvalidOperationException(result.Error.Message),
                    "Provider model discovery could not be completed.");
                return;
            }

            _snapshot = result.Value!;
            ApplySnapshot();
        }
        catch (OperationCanceledException)
            when (cts.IsCancellationRequested || IsDisposed || Disposing)
        {
        }
        catch (Exception exception)
        {
            if (version != Volatile.Read(ref _operationVersion) ||
                IsDisposed ||
                Disposing)
            {
                return;
            }

            SetStatus(
                _snapshot is null
                    ? "Model information could not be loaded."
                    : "Refresh failed. The last successful model observation is retained.",
                _snapshot is null ? HiveStatusTone.Error : HiveStatusTone.Warning);
            ReportError(exception, "Provider model discovery could not be completed.");
        }
        finally
        {
            if (ReferenceEquals(_operationCts, cts))
                Interlocked.CompareExchange(ref _operationCts, null, cts);

            cts.Dispose();

            if (!IsDisposed && !Disposing)
                _refreshButton.Enabled = true;
        }
    }

    private void ApplySnapshot()
    {
        if (_snapshot is null)
            return;

        var stale = _snapshot.IsStale(DateTimeOffset.UtcNow);
        SetStatus(
            _snapshot.ModelEnumerationState switch
            {
                ProviderDiscoveryState.Supported when _snapshot.Models.Count > 0 =>
                    stale
                        ? $"Discovered {_snapshot.Models.Count} model(s). Observation is stale; refresh recommended."
                        : $"Discovered {_snapshot.Models.Count} model(s).",
                ProviderDiscoveryState.Supported =>
                    "The provider returned no models. No model was inferred.",
                ProviderDiscoveryState.Unsupported =>
                    "This provider does not expose supported model enumeration.",
                _ => "Provider model enumeration state is unknown."
            },
            stale
                ? HiveStatusTone.Warning
                : _snapshot.ModelEnumerationState == ProviderDiscoveryState.Supported
                    ? HiveStatusTone.Success
                    : HiveStatusTone.Warning);

        _modelsList.BeginUpdate();
        try
        {
            _modelsList.Items.Clear();

            foreach (var model in _snapshot.Models)
            {
                var item = new ListViewItem(model.ModelId);
                item.SubItems.Add(model.OwnedBy ?? "Not reported");
                item.SubItems.Add(model.Availability.ToString());
                item.SubItems.Add(model.Health.ToString());
                item.Tag = model;
                _modelsList.Items.Add(item);
            }
        }
        finally
        {
            _modelsList.EndUpdate();
        }

        if (_modelsList.Items.Count > 0)
        {
            var firstItem = _modelsList.Items[0];
            firstItem.Selected = true;

            if (firstItem.Tag is ProviderModelMetadata firstModel)
                _detailsBox.Text = FormatModel(firstModel);
        }
        else
        {
            _detailsBox.Clear();
        }

        ApplyOperationalContext();
    }

    private void ModelsListSelected(object? sender, EventArgs e)
    {
        if (_modelsList.SelectedItems.Count == 0 ||
            _modelsList.SelectedItems[0].Tag is not ProviderModelMetadata model)
        {
            return;
        }

        _detailsBox.Text = FormatModel(model);
    }

    private void ApplyOperationalContext()
    {
        if (_snapshot is null)
            return;

        var operational = _snapshot.Operational;
        var rateLimit = operational.RateLimitRemaining is { } remaining
            ? $"  •  Provider rate limit remaining: {remaining}"
            : string.Empty;

        _contextLabel.Text =
            $"Provider: {_selectedProvider?.DisplayName ?? "—"}  •  " +
            $"Account: {_selectedAccount?.DisplayName ?? "—"}  •  " +
            $"Endpoint: {_selectedEndpoint?.AbsoluteUri ?? "—"}{rateLimit}";

        if (_snapshot.Models.Count == 0)
        {
            _detailsBox.Text =
                $"Provider operational state{Environment.NewLine}" +
                $"Availability: {operational.Availability}{Environment.NewLine}" +
                $"Health: {operational.Health}{Environment.NewLine}" +
                $"Observed: {operational.ObservedAtUtc:O}{Environment.NewLine}" +
                $"Stale after: {operational.StaleAfterUtc:O}{rateLimit}{Environment.NewLine}{Environment.NewLine}" +
                "No model metadata is available for this observation.";
        }
    }

    private void ClearObservation()
    {
        CancelOperation();
        _snapshot = null;
        _modelsList.Items.Clear();
        _detailsBox.Clear();
        SetStatus(
            "Select a Provider, Account, and endpoint to inspect model information.",
            HiveStatusTone.Neutral);
    }

    private void UpdateContextLabel()
    {
        _selectedEndpoint = TryParseEndpoint(_endpointComboBox.Text);

        _contextLabel.Text =
            $"Provider: {_selectedProvider?.DisplayName ?? "—"}  •  " +
            $"Account: {_selectedAccount?.DisplayName ?? "—"}  •  " +
            $"Endpoint: {_selectedEndpoint?.AbsoluteUri ?? "—"}";
    }

    private static string FormatModel(ProviderModelMetadata model)
    {
        var lines = new List<string>();

        AddSection(lines, "Identity", [
            $"Model: {model.ModelId}",
            $"Owner / provider attribution: {model.OwnedBy ?? "Not reported"}",
            $"Display name: {model.DisplayName ?? "Not reported"}",
            $"Description: {model.Description ?? "Not reported"}",
            $"Family: {model.Family ?? "Not reported"}",
            $"Model type: {model.ModelType ?? "Not reported"}",
            $"Category: {model.Category ?? "Not reported"}",
            $"Version: {model.Version ?? "Not reported"}",
            $"Operational state: {model.OperationalState ?? "Not reported"}",
            $"Created: {(model.CreatedAtUtc is { } created ? created.ToString("O") : "Not reported")}"
        ]);

        AddSection(lines, "Inputs", [
            $"Modalities: {FormatList(model.InputModalities)}"
        ]);

        AddSection(lines, "Outputs", [
            $"Modalities: {FormatList(model.OutputModalities)}"
        ]);

        AddSection(
            lines,
            "Capabilities",
            model.DiscoveredCapabilities.Count == 0
                ? ["No normalized capability state was reported."]
                : model.DiscoveredCapabilities
                    .OrderBy(item => item.Capability.Value, StringComparer.Ordinal)
                    .Select(item => $"{item.Capability.Value}: {item.State}")
                    .ToArray());

        AddSection(lines, "Reasoning / Thinking", [
            $"Reasoning: {FindCapability(model, HiveCapabilityKeys.Reasoning)}",
            $"Thinking: {FindCapability(model, HiveCapabilityKeys.Thinking)}",
            $"Options: {FormatList(model.ThinkingOptions)}",
            $"Default: {model.DefaultThinkingLevel ?? "Not reported"}"
        ]);

        var limits = model.Limits;
        AddSection(
            lines,
            "Limits",
            limits is null
                ? ["No model-scoped limits were reported."]
                : [
                    $"Context window tokens: {limits.ContextWindowTokens?.ToString() ?? "Not reported"}",
                    $"Max input tokens: {limits.MaxInputTokens?.ToString() ?? "Not reported"}",
                    $"Max output tokens: {limits.MaxOutputTokens?.ToString() ?? "Not reported"}",
                    $"Additional constraints: {FormatJsonDictionary(limits.AdditionalConstraints)}"
                ]);

        var pricing = model.Pricing;
        AddSection(
            lines,
            "Pricing / Economics",
            pricing is null
                ? [
                    "No pricing was reported.",
                    "Missing pricing is not evidence that the model is free."
                ]
                : [
                    $"Explicit free evidence: {(pricing.ExplicitFreeEvidence ? "Reported" : "Not reported")}",
                    pricing.Prices.Count == 0
                        ? "No billable rate entries were reported."
                        : string.Join(
                            Environment.NewLine,
                            pricing.Prices.Select(
                                item =>
                                    $"{item.BillingUnit}: {item.Price} " +
                                    $"{item.Currency ?? "currency not reported"}" +
                                    $"{(item.UnitQuantity is { } quantity ? $" per {quantity}" : string.Empty)}"))
                ]);

        AddSection(lines, "Operational state", [
            $"Availability: {model.Availability}",
            $"Health: {model.Health}",
            $"Observed: {model.ObservedAtUtc?.ToString("O") ?? "Snapshot observation timestamp"}",
            $"Stale after: {model.StaleAfterUtc?.ToString("O") ?? "Snapshot freshness boundary"}"
        ]);

        AddSection(
            lines,
            "Additional provider information",
            model.ExtensionData.Count == 0
                ? ["No additional bounded provider-specific evidence was reported."]
                : model.ExtensionData
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}: {pair.Value.GetRawText()}")
                    .ToArray());

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            lines);
    }

    private static bool EndpointsEqual(Uri left, Uri right) =>
        Uri.Compare(
            left,
            right,
            UriComponents.SchemeAndServer,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) == 0
        &&
        Uri.Compare(
            left,
            right,
            UriComponents.PathAndQuery,
            UriFormat.SafeUnescaped,
            StringComparison.Ordinal) == 0;

    private static string FindCapability(
        ProviderModelMetadata model,
        CapabilityKey key) =>
        model.DiscoveredCapabilities.FirstOrDefault(item => item.Capability == key)
            ?.State.ToString() ?? "Not reported";

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0
            ? "Not reported"
            : string.Join(", ", values);

    private static void AddSection(
        ICollection<string> lines,
        string heading,
        IEnumerable<string> content)
    {
        lines.Add(heading);
        lines.Add(new string('-', heading.Length));
        foreach (var line in content)
            lines.Add(line);
    }

    private static string FormatJsonDictionary(
        IReadOnlyDictionary<string, JsonElement> values)
    {
        if (values.Count == 0)
            return "None";

        return string.Join(
            "; ",
            values.Select(pair => $"{pair.Key}={pair.Value.GetRawText()}"));
    }

    private static Uri? TryParseEndpoint(string? text)
    {
        if (!Uri.TryCreate(
                text?.Trim(),
                UriKind.Absolute,
                out var endpoint))
        {
            return null;
        }

        return endpoint.Scheme is "http" or "https" &&
               string.IsNullOrEmpty(endpoint.UserInfo)
            ? endpoint
            : null;
    }

    private static HiveComboBox CreateSelector(string name) =>
        new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
            Height = 32,
            AccessibleName = name
        };

    private static Label CreateContextLabel(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };

    private static void SetItems<T>(
        HiveComboBox comboBox,
        IEnumerable<T> values)
        where T : notnull
    {
        comboBox.BeginUpdate();
        try
        {
            comboBox.Items.Clear();
            foreach (var value in values)
                comboBox.Items.Add(value);
        }
        finally
        {
            comboBox.EndUpdate();
        }
    }

    private void SetStatus(string text, HiveStatusTone tone)
    {
        _statusLabel.Text = text;
        _statusLabel.Tag = tone;
        ApplyStatusTheme();
    }

    private void ApplyStatusTheme()
    {
        var tone = _statusLabel.Tag is HiveStatusTone value
            ? value
            : HiveStatusTone.Neutral;

        _statusLabel.ForeColor = tone switch
        {
            HiveStatusTone.Information => _themeManager.Theme.VisualStates.Information,
            HiveStatusTone.Success => _themeManager.Theme.VisualStates.Success,
            HiveStatusTone.Warning => _themeManager.Theme.VisualStates.Warning,
            HiveStatusTone.Error => _themeManager.Theme.VisualStates.Error,
            _ => _themeManager.Theme.Palette.MutedText
        };
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (IsDisposed || Disposing)
            return;

        _themeManager.Apply(this);
        ApplyStatusTheme();
    }

    private void ReportError(Exception exception, string message)
    {
        IWin32Window? owner = FindForm();
        owner ??= this;

        HiveUiErrorReporter.Report(
            owner,
            exception,
            "Model Information",
            message,
            _output,
            _themeManager);
    }

    private void CancelOperation()
    {
        Interlocked.Increment(ref _operationVersion);
        var cts = Interlocked.Exchange(ref _operationCts, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
            _providerComboBox.SelectedIndexChanged -= ProviderChanged;
            _accountComboBox.SelectedIndexChanged -= AccountChanged;
            _endpointComboBox.SelectedIndexChanged -= EndpointChanged;
            _endpointComboBox.TextChanged -= EndpointTextChanged;
            _refreshButton.Click -= RefreshButtonOnClick;
            _modelsList.SelectedIndexChanged -= ModelsListSelected;
            CancelOperation();
        }

        base.Dispose(disposing);
    }
}
