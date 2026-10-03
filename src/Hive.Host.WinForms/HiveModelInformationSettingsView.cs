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

    private const string DetailKeyTag = "detail-key";
    private const string DetailValueTag = "detail-value";

    private sealed record EndpointChoice(Uri Endpoint, string Source)
    {
        public override string ToString() => Endpoint.AbsoluteUri;
    }

    private sealed record ModelInformationRow(
        ProviderModelMetadata Model,
        ExecutionTarget? ExecutionTarget,
        bool IsFavorite);

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveComboBox _providerComboBox;
    private readonly HiveComboBox _accountComboBox;
    private readonly HiveComboBox _endpointComboBox;
    private readonly HiveCrudPage<ModelInformationRow> _page;
    private readonly Panel _detailsContent;
    private readonly HiveScrollHost _detailsScrollHost;
    private CancellationTokenSource? _operationCts;
    private int _operationVersion;
    private bool _initializingContext;
    private Provider? _selectedProvider;
    private ProviderAccount? _selectedAccount;
    private Uri? _selectedEndpoint;
    private IReadOnlyList<ExecutionTarget> _executionTargets = Array.Empty<ExecutionTarget>();
    private IReadOnlyList<ExecutionTargetId> _favoriteExecutionTargetIds = Array.Empty<ExecutionTargetId>();
    private HashSet<ExecutionTargetId> _favoriteTargetIdSet = [];
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

        var contextCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(8),
            Margin = Padding.Empty,
            BorderStyle = BorderStyle.FixedSingle
        };

        var contextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
        contextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
        contextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
        contextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));

        contextPanel.Controls.Add(CreateContextLabel("Provider"), 0, 0);
        contextPanel.Controls.Add(CreateContextLabel("Account / Credential"), 1, 0);
        contextPanel.Controls.Add(CreateContextLabel("Discovery endpoint"), 2, 0);

        _providerComboBox = CreateSelector("Provider");
        _accountComboBox = CreateSelector("Account / Credential");
        _endpointComboBox = CreateSelector("Discovery endpoint");
        _endpointComboBox.DropDownStyle = ComboBoxStyle.DropDown;

        contextPanel.Controls.Add(_providerComboBox, 0, 1);
        contextPanel.Controls.Add(_accountComboBox, 1, 1);
        contextPanel.Controls.Add(_endpointComboBox, 2, 1);
        contextCard.Controls.Add(contextPanel);

        _page = new HiveCrudPage<ModelInformationRow>
        {
            Dock = DockStyle.Fill,
            Title = "Model Information",
            Description = "Browse discovered provider models, inspect normalized metadata, and add the selected ExecutionTarget to Favorites.",
            PageSize = 25,
            AllowAdd = true,
            AddButtonText = "Add to Favorites",
            AllowEdit = false,
            AllowDelete = false,
            ShowRefresh = false,
            ShowSearch = true,
            SearchPlaceholder = "Search models..."
        };

        _page.SetColumns(
            new HiveCrudColumn<ModelInformationRow>(
                "Model",
                300,
                row => row.IsFavorite
                    ? $"★ {row.Model.DisplayName ?? row.Model.ModelId}"
                    : row.Model.DisplayName ?? row.Model.ModelId),
            new HiveCrudColumn<ModelInformationRow>(
                "Text",
                95,
                row => FindCapability(row.Model, HiveCapabilityKeys.TextGeneration)),
            new HiveCrudColumn<ModelInformationRow>(
                "Vision",
                95,
                row => FindCapability(row.Model, HiveCapabilityKeys.Vision)),
            new HiveCrudColumn<ModelInformationRow>(
                "Tools",
                95,
                row => FindCapability(row.Model, HiveCapabilityKeys.ToolCalling)),
            new HiveCrudColumn<ModelInformationRow>(
                "Structured",
                105,
                row => FindCapability(row.Model, HiveCapabilityKeys.StructuredOutput)),
            new HiveCrudColumn<ModelInformationRow>(
                "Reasoning",
                100,
                row => FindCapability(row.Model, HiveCapabilityKeys.Reasoning)),
            new HiveCrudColumn<ModelInformationRow>(
                "Thinking",
                100,
                row => FindCapability(row.Model, HiveCapabilityKeys.Thinking)));

        _page.LoadItemsAsync = LoadModelsAsync;
        _page.EditItemAsync = AddSelectedModelToFavoritesAsync;
        _page.OperationFailed += PageOperationFailed;
        _page.ListView.ItemSelectionChanged += ModelsListSelectionChanged;

        _detailsContent = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 8, 8),
            MinimumSize = new Size(320, 48),
            AccessibleName = "Selected model information"
        };

        _detailsScrollHost = new HiveScrollHost
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AccessibleName = "Selected model information scroll area",
            AccessibleDescription = "Browse structured model information using the Hive scrollbars."
        };
        _detailsScrollHost.Attach(_detailsContent);
        _detailsScrollHost.Resize += DetailsScrollHostOnResize;

        var detailsSurface = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            Margin = Padding.Empty,
            BorderStyle = BorderStyle.FixedSingle
        };
        detailsSurface.Controls.Add(_detailsScrollHost);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 610,
            IsSplitterFixed = false
        };
        split.Panel1.Padding = new Padding(0, 6, 8, 0);
        split.Panel2.Padding = new Padding(8, 6, 0, 0);
        split.Panel1.Controls.Add(_page);
        split.Panel2.Controls.Add(detailsSurface);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(contextCard, 0, 0);
        root.Controls.Add(split, 0, 1);
        Controls.Add(root);

        _providerComboBox.SelectedIndexChanged += ProviderChanged;
        _accountComboBox.SelectedIndexChanged += AccountChanged;
        _endpointComboBox.SelectedIndexChanged += EndpointChanged;
        _endpointComboBox.TextChanged += EndpointTextChanged;

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme();
    }

    internal HiveComboBox ProviderSelector => _providerComboBox;

    internal HiveComboBox AccountSelector => _accountComboBox;

    internal HiveComboBox EndpointSelector => _endpointComboBox;

    internal HiveCrudPage<ModelInformationRow> CrudPage => _page;

    internal HiveListView ModelsList => (HiveListView)_page.ListView;

    internal Panel DetailsContent => _detailsContent;

    internal HiveScrollHost DetailsScrollHost => _detailsScrollHost;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var favoriteResult = await _management
            .GetFavoriteExecutionTargetIdsAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (favoriteResult.IsFailure)
            throw new InvalidOperationException(favoriteResult.Error!.Message);

        _favoriteExecutionTargetIds = favoriteResult.Value!;
        _favoriteTargetIdSet = _favoriteExecutionTargetIds.ToHashSet();

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

    private void UpdateContextLabel()
    {
        _selectedEndpoint = TryParseEndpoint(_endpointComboBox.Text);

        _contextLabel.Text =
            $"Provider: {_selectedProvider?.DisplayName ?? "—"}  •  " +
            $"Account: {_selectedAccount?.DisplayName ?? "—"}  •  " +
            $"Endpoint: {_selectedEndpoint?.AbsoluteUri ?? "—"}";
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
                item.SubItems.Add(model.ModelType ?? "Not reported");
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
            firstItem.Focused = true;

            if (firstItem.Tag is ProviderModelMetadata model)
                RenderModelDetails(model);
        }
        else
        {
            RenderNoModelDetails();
        }

        ApplyOperationalContext();
        ResizeDetailCards();
    }

    private void ModelsListSelectionChanged(
        object? sender,
        ListViewItemSelectionChangedEventArgs e)
    {
        if (!e.IsSelected)
            return;

        if (e.Item is not ListViewItem item)
            return;

        if (item.Tag is ProviderModelMetadata model)
        {
            RenderModelDetails(model);
            return;
        }

        if (_snapshot?.Models.Count == 0)
            RenderNoModelDetails();
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
            RenderNoModelDetails();
    }

    private void ClearObservation()
    {
        CancelOperation();
        _snapshot = null;
        _modelsList.Items.Clear();
        ClearDetailsContent();
        SetStatus(
            "Select a Provider, Account, and endpoint to inspect model information.",
            HiveStatusTone.Neutral);
    }

    private void RenderNoModelDetails()
    {
        ClearDetailsContent();

        var operational = _snapshot?.Operational;
        var card = CreateDetailCard(
            "No model selected",
            emphasized: true);

        var table = GetCardTable(card);
        AddKeyValueRow(
            table,
            "Discovery state",
            _snapshot?.ModelEnumerationState.ToString() ?? "Not started");
        AddKeyValueRow(
            table,
            "Provider availability",
            operational?.Availability.ToString() ?? "Not reported");
        AddKeyValueRow(
            table,
            "Provider health",
            operational?.Health.ToString() ?? "Not reported");
        AddKeyValueRow(
            table,
            "Observed",
            operational?.ObservedAtUtc.ToString("O") ?? "Not reported");
        AddKeyValueRow(
            table,
            "Stale after",
            operational?.StaleAfterUtc.ToString("O") ?? "Not reported");

        var message = _snapshot is null
            ? "Choose a Provider, Account, and endpoint, then refresh."
            : _snapshot.Models.Count == 0
                ? "No model metadata is available for this observation. Hive does not infer a model when enumeration returns none."
                : "Select a model from the discovered catalog.";

        AddKeyValueRow(table, "Information", message);

        _detailsContent.Controls.Add(card);
        ApplyDetailsTheme();
        ResizeDetailCards();
    }

    private void RenderModelDetails(ProviderModelMetadata model)
    {
        _detailsContent.SuspendLayout();
        try
        {
            ClearDetailsContent();

            var summary = CreateDetailCard(
                model.DisplayName ?? model.ModelId,
                emphasized: true);
            var summaryTable = GetCardTable(summary);

            AddKeyValueRow(summaryTable, "Model ID", model.ModelId);
            AddKeyValueRow(summaryTable, "Owner / attribution", model.OwnedBy ?? "Not reported");
            AddKeyValueRow(summaryTable, "Family", model.Family ?? "Not reported");
            AddKeyValueRow(summaryTable, "Type", model.ModelType ?? "Not reported");
            AddKeyValueRow(summaryTable, "Category", model.Category ?? "Not reported");
            AddKeyValueRow(summaryTable, "Version", model.Version ?? "Not reported");
            AddKeyValueRow(summaryTable, "Operational state", model.OperationalState ?? "Not reported");
            AddKeyValueRow(
                summaryTable,
                "Created",
                model.CreatedAtUtc?.ToString("O") ?? "Not reported");
            AddKeyValueRow(
                summaryTable,
                "Description",
                model.Description ?? "Not reported");

            _detailsContent.Controls.Add(summary);

            var modalities = CreateDetailCard("Inputs & outputs");
            var modalitiesTable = GetCardTable(modalities);
            AddKeyValueRow(
                modalitiesTable,
                "Input modalities",
                FormatList(model.InputModalities));
            AddKeyValueRow(
                modalitiesTable,
                "Output modalities",
                FormatList(model.OutputModalities));
            _detailsContent.Controls.Add(modalities);

            var capabilities = CreateDetailCard("Capabilities");
            var capabilityTable = GetCardTable(capabilities);
            AddKeyValueRow(capabilityTable, "Capability", "Discovered state");
            if (model.DiscoveredCapabilities.Count == 0)
            {
                AddKeyValueRow(
                    capabilityTable,
                    "State",
                    "No normalized capability state was reported.");
            }
            else
            {
                foreach (var item in model.DiscoveredCapabilities
                             .OrderBy(item => item.Capability.Value, StringComparer.Ordinal))
                {
                    AddKeyValueRow(
                        capabilityTable,
                        item.Capability.Value,
                        $"{item.State}  •  discovered");
                }
            }

            _detailsContent.Controls.Add(capabilities);

            var reasoning = CreateDetailCard("Reasoning & thinking");
            var reasoningTable = GetCardTable(reasoning);
            AddKeyValueRow(
                reasoningTable,
                "Reasoning",
                FindCapability(model, HiveCapabilityKeys.Reasoning));
            AddKeyValueRow(
                reasoningTable,
                "Thinking",
                FindCapability(model, HiveCapabilityKeys.Thinking));
            AddKeyValueRow(
                reasoningTable,
                "Options",
                FormatList(model.ThinkingOptions));
            AddKeyValueRow(
                reasoningTable,
                "Default",
                model.DefaultThinkingLevel ?? "Not reported");
            _detailsContent.Controls.Add(reasoning);

            var limits = CreateDetailCard("Limits");
            var limitsTable = GetCardTable(limits);
            if (model.Limits is null)
            {
                AddKeyValueRow(
                    limitsTable,
                    "Status",
                    "No model-scoped limits were reported.");
            }
            else
            {
                AddKeyValueRow(
                    limitsTable,
                    "Context window tokens",
                    model.Limits.ContextWindowTokens?.ToString() ?? "Not reported");
                AddKeyValueRow(
                    limitsTable,
                    "Max input tokens",
                    model.Limits.MaxInputTokens?.ToString() ?? "Not reported");
                AddKeyValueRow(
                    limitsTable,
                    "Max output tokens",
                    model.Limits.MaxOutputTokens?.ToString() ?? "Not reported");
                AddKeyValueRow(
                    limitsTable,
                    "Additional constraints",
                    FormatJsonDictionary(model.Limits.AdditionalConstraints));
            }

            _detailsContent.Controls.Add(limits);

            var pricing = CreateDetailCard("Pricing & economics");
            var pricingTable = GetCardTable(pricing);
            if (model.Pricing is null)
            {
                AddKeyValueRow(pricingTable, "Status", "No pricing was reported.");
                AddKeyValueRow(
                    pricingTable,
                    "Interpretation",
                    "Missing pricing is not evidence that the model is free.");
            }
            else
            {
                AddKeyValueRow(
                    pricingTable,
                    "Explicit free evidence",
                    model.Pricing.ExplicitFreeEvidence ? "Reported" : "Not reported");

                if (model.Pricing.Prices.Count == 0)
                {
                    AddKeyValueRow(
                        pricingTable,
                        "Rates",
                        "No billable rate entries were reported.");
                }
                else
                {
                    foreach (var price in model.Pricing.Prices)
                    {
                        var quantity = price.UnitQuantity is { } value
                            ? $" per {value:0.####}"
                            : string.Empty;
                        AddKeyValueRow(
                            pricingTable,
                            price.BillingUnit,
                            $"{price.Price:0.##########} {price.Currency ?? "currency not reported"}{quantity}");
                    }
                }
            }

            _detailsContent.Controls.Add(pricing);

            var operational = CreateDetailCard("Operational state");
            var operationalTable = GetCardTable(operational);
            AddKeyValueRow(
                operationalTable,
                "Availability",
                model.Availability.ToString());
            AddKeyValueRow(
                operationalTable,
                "Health",
                model.Health.ToString());
            AddKeyValueRow(
                operationalTable,
                "Observed",
                model.ObservedAtUtc?.ToString("O") ?? "Snapshot observation timestamp");
            AddKeyValueRow(
                operationalTable,
                "Stale after",
                model.StaleAfterUtc?.ToString("O") ?? "Snapshot freshness boundary");
            _detailsContent.Controls.Add(operational);

            var providerInfo = CreateDetailCard("Additional provider information");
            var providerInfoTable = GetCardTable(providerInfo);
            if (model.ExtensionData.Count == 0)
            {
                AddKeyValueRow(
                    providerInfoTable,
                    "Status",
                    "No additional bounded provider-specific evidence was reported.");
            }
            else
            {
                foreach (var pair in model.ExtensionData
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    AddKeyValueRow(
                        providerInfoTable,
                        pair.Key,
                        FormatJsonValue(pair.Value));
                }
            }

            _detailsContent.Controls.Add(providerInfo);
            ApplyDetailsTheme();
        }
        finally
        {
            _detailsContent.ResumeLayout(true);
            ResizeDetailCards();
        }
    }

    private Panel CreateDetailCard(
        string title,
        bool emphasized = false)
    {
        var theme = _themeManager.Theme;
        var card = new Panel
        {
            AutoSize = false,
            Size = new Size(320, 48),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10),
            BackColor = emphasized
                ? theme.Palette.Surface
                : theme.Palette.ElevatedSurface,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = title,
            Tag = emphasized ? "emphasized" : null
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 28,
            Text = title,
            Padding = new Padding(0, 0, 0, 6),
            AccessibleRole = AccessibleRole.StaticText,
            Font = new Font(
                _themeManager.Theme.Typography.FontFamily,
                _themeManager.Theme.Typography.SectionSize + 0.75f,
                FontStyle.Bold),
            AccessibleName = title
        };

        table.Controls.Add(heading, 0, 0);
        table.SetColumnSpan(heading, 2);
        card.Controls.Add(table);
        return card;
    }

    private static TableLayoutPanel GetCardTable(Panel card) =>
        card.Controls.OfType<TableLayoutPanel>().Single();

    private static void AddKeyValueRow(
        TableLayoutPanel table,
        string key,
        string value)
    {
        var row = table.RowCount;
        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var keyLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = key,
            Padding = new Padding(0, 3, 12, 3),
            TextAlign = ContentAlignment.TopLeft,
            Tag = DetailKeyTag
        };

        var valueLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Text = value,
            Padding = new Padding(0, 3, 0, 3),
            Margin = Padding.Empty,
            Tag = DetailValueTag
        };

        table.Controls.Add(keyLabel, 0, row);
        table.Controls.Add(valueLabel, 1, row);
    }

    private void ClearDetailsContent()
    {
        while (_detailsContent.Controls.Count > 0)
        {
            var child = _detailsContent.Controls[0];
            _detailsContent.Controls.RemoveAt(0);
            child.Dispose();
        }
    }

    private void ApplyDetailsTheme()
    {
        if (IsDisposed || Disposing)
            return;

        _themeManager.Apply(_detailsContent);

        var theme = _themeManager.Theme;
        foreach (var card in _detailsContent.Controls.OfType<Panel>())
        {
            card.BackColor = card.Tag is string tag && tag == "emphasized"
                ? theme.Palette.Surface
                : theme.Palette.ElevatedSurface;
            card.ForeColor = theme.Palette.Text;

            var table = GetCardTable(card);

            foreach (var label in table.Controls.OfType<Label>())
            {
                label.ForeColor = label.Tag switch
                {
                    DetailKeyTag => theme.Palette.MutedText,
                    DetailValueTag => theme.Palette.Text,
                    _ => theme.Palette.Text
                };
            }
        }
    }

    private void DetailsScrollHostOnResize(object? sender, EventArgs e) =>
        ResizeDetailCards();

    private void ResizeDetailCards()
    {
        if (IsDisposed || Disposing)
            return;

        var availableWidth = Math.Max(
            320,
            _detailsScrollHost.ClientSize.Width - _detailsContent.Padding.Horizontal - 10);

        var top = 0;
        foreach (Control child in _detailsContent.Controls)
        {
            if (child is not Panel card)
                continue;

            card.Width = availableWidth;

            if (card.Controls.OfType<TableLayoutPanel>().SingleOrDefault() is { } table)
            {
                table.Width = Math.Max(
                    300,
                    card.ClientSize.Width - card.Padding.Horizontal);

                table.PerformLayout();

                var preferred = table.GetPreferredSize(
                    new Size(table.Width, 0));

                card.Height = Math.Max(
                    48,
                    preferred.Height + card.Padding.Vertical + 2);
            }

            card.Location = new Point(0, top);
            top += card.Height + card.Margin.Bottom;
        }

        _detailsContent.Size = new Size(
            Math.Max(320, availableWidth + _detailsContent.Padding.Horizontal),
            Math.Max(
                48,
                top + _detailsContent.Padding.Bottom));

        _detailsContent.PerformLayout();
        _detailsScrollHost.Synchronize();
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

    private static string FormatJsonDictionary(
        IReadOnlyDictionary<string, JsonElement> values)
    {
        if (values.Count == 0)
            return "None";

        return string.Join(
            Environment.NewLine,
            values.Select(pair => $"{pair.Key}: {FormatJsonValue(pair.Value)}"));
    }

    private static string FormatJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => "null",
            _ => value.GetRawText()
        };

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
            Height = 36,
            Margin = Padding.Empty,
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
        comboBox.SuspendLayout();
        try
        {
            comboBox.Items.Clear();
            foreach (var value in values)
                comboBox.Items.Add(value);
        }
        finally
        {
            comboBox.ResumeLayout(true);
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
        ApplyDetailsTheme();
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
            _modelsList.ItemSelectionChanged -= ModelsListSelectionChanged;
            _detailsScrollHost.Resize -= DetailsScrollHostOnResize;
            CancelOperation();
        }

        base.Dispose(disposing);
    }
}
