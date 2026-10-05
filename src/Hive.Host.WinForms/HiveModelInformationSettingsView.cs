using System.Drawing;
using System.Globalization;
using System.Text.Json;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using System.ComponentModel;

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

    private sealed record CapabilityFilterChoice(
        CapabilityKey? Key,
        string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record StateFilterChoice(
        ModelCapabilityFilterState State,
        string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record EndpointChoice(Uri Endpoint, string Source)
    {
        public override string ToString() => Endpoint.AbsoluteUri;
    }

    private sealed class DetailSection
    {
        public DetailSection(
            Panel surface,
            Label title,
            FlowLayoutPanel body)
        {
            Surface = surface;
            Title = title;
            Body = body;
        }

        public Panel Surface { get; }

        public Label Title { get; }

        public FlowLayoutPanel Body { get; }
    }

    internal sealed class ModelInformationRow
    {
        public ModelInformationRow(
            ProviderModelMetadata model,
            ExecutionTarget? executionTarget,
            bool isFavorite,
            ModelCapabilitySummary capabilities,
            decimal? comparablePricePerMillion)
        {
            Model = model;
            ExecutionTarget = executionTarget;
            IsFavorite = isFavorite;
            Capabilities = capabilities;
            ComparablePricePerMillion = comparablePricePerMillion;
        }

        public ProviderModelMetadata Model { get; }

        public ExecutionTarget? ExecutionTarget { get; }

        public bool IsFavorite { get; set; }

        public ModelCapabilitySummary Capabilities { get; }

        public decimal? ComparablePricePerMillion { get; }
    }

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveComboBox _providerComboBox;
    private readonly HiveComboBox _accountComboBox;
    private readonly HiveComboBox _endpointComboBox;
    private readonly HiveComboBox _capabilityFilter;
    private readonly HiveComboBox _capabilityStateFilter;
    private readonly HiveCrudPage<ModelInformationRow> _page;
    private readonly SplitContainer _mainSplit;
    private readonly Panel _detailsContent;
    private readonly Label _detailsTitle;
    private readonly Label _detailsSummary;
    private readonly TableLayoutPanel _detailsSections;
    private readonly DetailSection _identityDetailsSection;
    private readonly DetailSection _inputsDetailsSection;
    private readonly DetailSection _outputsDetailsSection;
    private readonly DetailSection _capabilitiesDetailsSection;
    private readonly DetailSection _reasoningDetailsSection;
    private readonly DetailSection _limitsDetailsSection;
    private readonly DetailSection _pricingDetailsSection;
    private readonly DetailSection _operationalDetailsSection;
    private readonly DetailSection _additionalDetailsSection;
    private const int PriceSliderScale = 100;
    private const int InitialPriceSliderValue =
        checked((int)(1m * PriceSliderScale));
    private readonly TrackBar _minPriceFilter;
    private readonly TrackBar _maxPriceFilter;
    private readonly CheckBox _showUnpricedModels;
    private readonly Label _filterNotice;
    private readonly CheckBox _showAboveRangeModels;
    private decimal _highestComparablePrice;
    private ModelCatalogIndex _index = ModelCatalogIndex.Build([], [], null);
    private readonly Label _minPriceValueLabel;
    private readonly Label _maxPriceValueLabel;
    private bool _updatingPriceFilters;
    private readonly HiveScrollHost _detailsScrollHost;
    private bool _updatingModelList;
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

        // Fixed widths include the header renderer's 10px-per-side text padding
        // plus headroom for bold 9.25pt text under DPI scaling. The final column
        // is fill-stretched by HiveListView to consume remaining viewport width.
        _page.SetColumns(
            new HiveCrudColumn<ModelInformationRow>("Model", 240, FormatModelName),
            new HiveCrudColumn<ModelInformationRow>(
                "Price / 1M",
                100,
                FormatRowPrice,
                GetPriceColor),
            new HiveCrudColumn<ModelInformationRow>(
                "Context",
                84,
                row => FormatRowContext(row.Model)),
            new HiveCrudColumn<ModelInformationRow>(
                "Text",
                56,
                row => FormatCapabilityState(
                    row.Capabilities.Text),
                row => GetCapabilityStateColor(row.Capabilities.Text)),
            new HiveCrudColumn<ModelInformationRow>(
                "Vision",
                72,
                row => FormatCapabilityState(row.Capabilities.Vision),
                row => GetCapabilityStateColor(row.Capabilities.Vision)),
            new HiveCrudColumn<ModelInformationRow>(
                "Tools",
                64,
                row => FormatCapabilityState(row.Capabilities.Tools),
                row => GetCapabilityStateColor(row.Capabilities.Tools)),
            new HiveCrudColumn<ModelInformationRow>(
                "Reasoning",
                88,
                row => FormatCapabilityState(row.Capabilities.Reasoning),
                row => GetCapabilityStateColor(row.Capabilities.Reasoning)));

        _page.LoadItemsAsync = LoadModelsAsync;
        _page.EditItemAsync = AddSelectedModelToFavoritesAsync;
        _page.OperationFailed += PageOperationFailed;
        _page.ListView.ItemSelectionChanged += ModelsListSelectionChanged;

        _capabilityFilter = new HiveComboBox
        {
            Width = 150,
            Height = 32,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = Padding.Empty,
            AccessibleName = "Capability filter"
        };
        foreach (var choice in GetCapabilityFilterChoices())
            _capabilityFilter.Items.Add(choice);
        _capabilityFilter.SelectedIndex = 0;


        _capabilityStateFilter = new HiveComboBox
        {
            Width = 88,
            Height = 32,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = Padding.Empty,
            AccessibleName = "Capability state filter"
        };
        _capabilityStateFilter.Items.Add(
            new StateFilterChoice(ModelCapabilityFilterState.Any, "Any"));
        _capabilityStateFilter.Items.Add(
            new StateFilterChoice(ModelCapabilityFilterState.Supported, "Supported"));
        _capabilityStateFilter.Items.Add(
            new StateFilterChoice(ModelCapabilityFilterState.Unsupported, "Unsupported"));
        _capabilityStateFilter.Items.Add(
            new StateFilterChoice(
                ModelCapabilityFilterState.Unknown,
                "Unknown / unreported"));
        _capabilityStateFilter.SelectedIndex = 0;

        var filterBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 0, 6),
            AccessibleName = "Model Information filters"
        };
        filterBar.Controls.Add(CreateFilterLabel("Min"));
        _minPriceValueLabel = CreatePriceValueLabel();
        _maxPriceValueLabel = CreatePriceValueLabel();

        _minPriceFilter = CreatePriceSlider(0);
        _maxPriceFilter = CreatePriceSlider(
            InitialPriceSliderValue);

        filterBar.Controls.Add(_minPriceFilter);
        filterBar.Controls.Add(_minPriceValueLabel);
        filterBar.Controls.Add(CreateFilterLabel("Max"));
        filterBar.Controls.Add(_maxPriceFilter);
        filterBar.Controls.Add(_maxPriceValueLabel);

        UpdatePriceFilterLabels();
        filterBar.Controls.Add(CreateFilterLabel("Capability"));
        filterBar.Controls.Add(_capabilityFilter);
        filterBar.Controls.Add(CreateFilterLabel("State"));
        filterBar.Controls.Add(_capabilityStateFilter);

        // A model can report pricing Hive cannot compare. Under a bounded price
        // range such models are excluded, which is the established behaviour;
        // the notice below makes that visible and reversible instead of silent.
        _showUnpricedModels = new CheckBox
        {
            Text = "Show models without comparable pricing",
            AutoSize = true,
            Checked = false,
            Margin = new Padding(12, 9, 0, 0),
            AccessibleName = "Show models without comparable pricing"
        };

        filterBar.Controls.Add(_showUnpricedModels);

        // The ceiling is a high percentile so the slider stays operable; models
        // beyond it must stay reachable through an explicit control.
        _showAboveRangeModels = new CheckBox
        {
            Text = "Show models above the price range",
            AutoSize = true,
            Checked = false,
            Margin = new Padding(12, 9, 0, 0),
            AccessibleName = "Show models above the price range"
        };

        filterBar.Controls.Add(_showAboveRangeModels);

        _filterNotice = new Label
        {
            AutoSize = true,
            Visible = false,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 4),
            AccessibleName = "Model filter summary"
        };

        _minPriceFilter.ValueChanged += PriceFilterValueChanged;
        _maxPriceFilter.ValueChanged += PriceFilterValueChanged;
        _capabilityFilter.SelectedIndexChanged += FilterChanged;
        _capabilityStateFilter.SelectedIndexChanged += FilterChanged;
        _showUnpricedModels.CheckedChanged += FilterChanged;
        _showAboveRangeModels.CheckedChanged += FilterChanged;

        _page.LoadItemsAsync = LoadModelsAsync;
        _page.EditItemAsync = AddSelectedModelToFavoritesAsync;
        _page.OperationFailed += PageOperationFailed;
        _page.ListView.ItemSelectionChanged += ModelsListSelectionChanged;

        _detailsContent = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = new Padding(12),
            MinimumSize = new Size(300, 48),
            AccessibleName = "Selected model information"
        };

        _detailsTitle = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Font = new Font(
                _themeManager.Theme.Typography.FontFamily,
                _themeManager.Theme.Typography.SectionSize + 0.75f,
                FontStyle.Bold),
            AccessibleRole = AccessibleRole.StaticText
        };

        _detailsSummary = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 6, 0, 12),
            Padding = Padding.Empty,
            AccessibleRole = AccessibleRole.StaticText
        };

        _detailsSections = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _detailsSections.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        _identityDetailsSection = CreateDetailSection("Identity");
        _inputsDetailsSection = CreateDetailSection("Inputs");
        _outputsDetailsSection = CreateDetailSection("Outputs");
        _capabilitiesDetailsSection = CreateDetailSection("Capabilities");
        _reasoningDetailsSection = CreateDetailSection("Reasoning / Thinking");
        _limitsDetailsSection = CreateDetailSection("Limits");
        _pricingDetailsSection = CreateDetailSection("Pricing");
        _operationalDetailsSection = CreateDetailSection("Operational state");
        _additionalDetailsSection =
            CreateDetailSection("Additional provider information");

        foreach (var section in new[]
                 {
                     _identityDetailsSection,
                     _inputsDetailsSection,
                     _outputsDetailsSection,
                     _capabilitiesDetailsSection,
                     _reasoningDetailsSection,
                     _limitsDetailsSection,
                     _pricingDetailsSection,
                     _operationalDetailsSection,
                     _additionalDetailsSection
                 })
        {
            var rowIndex = _detailsSections.RowCount;
            _detailsSections.RowStyles.Add(
                new RowStyle(SizeType.AutoSize));
            _detailsSections.Controls.Add(section.Surface, 0, rowIndex);
            _detailsSections.RowCount = rowIndex + 1;
        }

        _detailsContent.Controls.Add(_detailsSections);
        _detailsContent.Controls.Add(_detailsSummary);
        _detailsContent.Controls.Add(_detailsTitle);
        var detailsSurface = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            Margin = Padding.Empty,
            BorderStyle = BorderStyle.FixedSingle
        };
        detailsSurface.Controls.Add(_detailsScrollHost);

        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = false,
            FixedPanel = FixedPanel.Panel2,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };
        _mainSplit.SizeChanged += MainSplitSizeChanged;
        _mainSplit.Panel1.Padding = new Padding(0, 6, 8, 0);
        _mainSplit.Panel2.Padding = new Padding(8, 6, 0, 0);
        _mainSplit.Panel1.Controls.Add(_page);
        _mainSplit.Panel2.Controls.Add(detailsSurface);

        var pageHeader = _page.HeaderPanel;
        _page.PageLayout.HeaderHeight = 0;
        pageHeader.Dock = DockStyle.Fill;
        pageHeader.Margin = Padding.Empty;

        var contextAndFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        contextAndFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        contextAndFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        contextAndFilters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contextAndFilters.Controls.Add(contextCard, 0, 0);
        contextAndFilters.Controls.Add(filterBar, 0, 1);
        contextAndFilters.Controls.Add(_filterNotice, 0, 2);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(pageHeader, 0, 0);
        root.Controls.Add(contextAndFilters, 0, 1);
        root.Controls.Add(_mainSplit, 0, 2);
        Controls.Add(root);

        _providerComboBox.SelectedIndexChanged += ProviderChanged;
        _accountComboBox.SelectedIndexChanged += AccountChanged;
        _endpointComboBox.SelectedIndexChanged += EndpointChanged;
        _endpointComboBox.TextChanged += EndpointTextChanged;

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        ApplyTheme();
    }

    private const int MainSplitPanel1MinimumWidth = 320;
    private const int MainSplitPanel2MinimumWidth = 400;
    private const int MainSplitPreferredPanel2Width = 400;
    private bool _mainSplitConstraintsApplied;

    private void MainSplitSizeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        ApplyMainSplitConstraints();
    }

    private void ApplyMainSplitConstraints()
    {
        var width = _mainSplit.ClientSize.Width;
        var minimumWidth =
            MainSplitPanel1MinimumWidth +
            MainSplitPanel2MinimumWidth +
            _mainSplit.SplitterWidth;

        if (width < minimumWidth)
            return;

        if (!_mainSplitConstraintsApplied)
        {
            var preferredSplitterDistance =
                width -
                MainSplitPreferredPanel2Width -
                _mainSplit.SplitterWidth;

            var initialSplitterDistance = Math.Clamp(
                preferredSplitterDistance,
                MainSplitPanel1MinimumWidth,
                width -
                MainSplitPanel2MinimumWidth -
                _mainSplit.SplitterWidth);

            _mainSplit.SplitterDistance = initialSplitterDistance;
            _mainSplit.Panel1MinSize = MainSplitPanel1MinimumWidth;
            _mainSplit.Panel2MinSize = MainSplitPanel2MinimumWidth;
            _mainSplitConstraintsApplied = true;
        }
    }

    internal HiveComboBox ProviderSelector => _providerComboBox;

    internal HiveComboBox AccountSelector => _accountComboBox;

    internal HiveComboBox EndpointSelector => _endpointComboBox;

    internal HiveCrudPage<ModelInformationRow> CrudPage => _page;

    internal HiveListView ModelsList => (HiveListView)_page.ListView;

    internal TrackBar MinPriceFilter => _minPriceFilter;

    internal TrackBar MaxPriceFilter => _maxPriceFilter;

    internal HiveComboBox CapabilityFilter => _capabilityFilter;

    internal HiveComboBox CapabilityStateFilter => _capabilityStateFilter;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<string, bool>? FavoriteConfirmationOverride { get; set; }

    internal Panel DetailsContent => _detailsContent;

    internal int DetailsPanelWidth => _mainSplit.Panel2.Width;

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
    }

    private void EndpointChanged(object? sender, EventArgs e)
    {
        if (_initializingContext || IsDisposed || Disposing)
            return;

        _selectedEndpoint =
            (_endpointComboBox.SelectedItem as EndpointChoice)?.Endpoint;

        ClearObservation();

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

                _executionTargets = targets.Value!;

                endpoints.AddRange(
                    _executionTargets
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


        if (_selectedEndpoint is not null)
            await LoadCachedOrDiscoverAsync().ConfigureAwait(true);
    }

    private void PriceFilterValueChanged(object? sender, EventArgs e)
    {
        if (_updatingPriceFilters || IsDisposed || Disposing)
            return;

        _updatingPriceFilters = true;
        try
        {
            if (ReferenceEquals(sender, _minPriceFilter) &&
                _minPriceFilter.Value > _maxPriceFilter.Value)
            {
                _maxPriceFilter.Value = _minPriceFilter.Value;
            }
            else if (ReferenceEquals(sender, _maxPriceFilter) &&
                     _maxPriceFilter.Value < _minPriceFilter.Value)
            {
                _minPriceFilter.Value = _maxPriceFilter.Value;
            }

            UpdatePriceFilterLabels();
        }
        finally
        {
            _updatingPriceFilters = false;
        }

        ApplyModelFilters();
    }

    private void UpdatePriceFilterLabels()
    {
        _minPriceValueLabel.Text = FormatPriceSliderValue(_minPriceFilter.Value);
        _maxPriceValueLabel.Text = FormatPriceSliderValue(_maxPriceFilter.Value);
    }

    private static string FormatPriceSliderValue(int value) =>
        "$" + (value / (decimal)PriceSliderScale).ToString("0.00", CultureInfo.InvariantCulture);

    private void ConfigurePriceFiltersForSnapshot()
    {
        if (_snapshot is null)
            return;

        // Resolve pricing, free-ness, capability states, and target mapping once
        // per snapshot so filtering stays a comparison over cached values.
        _index = ModelCatalogIndex.Build(
            _snapshot.Models,
            _executionTargets,
            _selectedEndpoint);

        var comparablePrices = _index.ComparablePrices;

        var maximumPrice =
            ModelInformationFilter.CalculatePriceCeiling(comparablePrices);

        _highestComparablePrice = _index.HighestComparablePrice;

        var maximumValue = checked(
            (int)(maximumPrice * PriceSliderScale));

        _updatingPriceFilters = true;
        try
        {
            _minPriceFilter.Maximum = maximumValue;
            _maxPriceFilter.Maximum = maximumValue;
            _minPriceFilter.Value = 0;
            _maxPriceFilter.Value = maximumValue;
            UpdatePriceFilterLabels();
        }
        finally
        {
            _updatingPriceFilters = false;
        }
    }

    private void FilterChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        ApplyModelFilters();
    }

    private void ApplyModelFilters()
    {
        if (IsDisposed || Disposing || _snapshot is null)
            return;

        var selectedModelId =
            (_page.SelectedItem as ModelInformationRow)?.Model.ModelId;

        var criteria = CaptureFilterCriteria();
        var rows = CreateModelRows(criteria);

        _updatingModelList = true;
        try
        {
            _page.SetItemsForView(rows);
        }
        finally
        {
            _updatingModelList = false;
        }

        UpdateFilterNotice(criteria, rows.Count);

        ListViewItem? selectedItem = null;

        if (!string.IsNullOrWhiteSpace(selectedModelId))
        {
            selectedItem = _page.ListView.Items
                .Cast<ListViewItem>()
                .FirstOrDefault(item =>
                    item.Tag is ModelInformationRow row &&
                    string.Equals(
                        row.Model.ModelId,
                        selectedModelId,
                        StringComparison.OrdinalIgnoreCase));
        }

        selectedItem ??= _page.ListView.Items.Count > 0
            ? _page.ListView.Items[0]
            : null;

        if (selectedItem is not null)
        {
            selectedItem.Selected = true;
            selectedItem.Focused = true;

            if (selectedItem.Tag is ModelInformationRow row)
                RenderModelDetails(row);
        }
        else
        {
            RenderNoModelDetails();
        }
    }

    private IReadOnlyList<ModelInformationRow> CreateModelRows(
        ModelFilterCriteria criteria)
    {
        if (_snapshot is null)
            return Array.Empty<ModelInformationRow>();

        var matched = _index.Select(criteria);

        if (matched.Count == 0)
            return Array.Empty<ModelInformationRow>();

        var rows = new ModelInformationRow[matched.Count];

        for (var index = 0; index < matched.Count; index++)
        {
            var entry = matched[index];

            rows[index] = new ModelInformationRow(
                entry.Model,
                entry.Target,
                entry.Target is not null &&
                _favoriteTargetIdSet.Contains(entry.Target.Id),
                entry.Capabilities,
                entry.ComparablePricePerMillion);
        }

        return rows;
    }

    /// <summary>
    /// Explains, rather than silently applies, any model the current filter
    /// selection is holding back.
    /// </summary>
    /// <remarks>
    /// A bounded price range legitimately excludes models whose pricing Hive
    /// cannot compare. Without this notice the catalog simply appears empty,
    /// which reads as "this provider has no models" rather than "your filter is
    /// hiding rows".
    /// </remarks>
    private void UpdateFilterNotice(
        ModelFilterCriteria criteria,
        int visibleCount)
    {
        if (IsDisposed || Disposing || _snapshot is null)
            return;

        var total = _snapshot.Models.Count;

        if (visibleCount == 0)
        {
            _filterNotice.Text =
                $"No models match the current filters (of {total} discovered).";
            _filterNotice.Visible = true;
            return;
        }

        var withoutComparablePricing = _index.WithoutComparablePricingCount;

        var ceiling = _maxPriceFilter.Maximum / (decimal)PriceSliderScale;
        var aboveRange =
            _showAboveRangeModels.Checked
                ? 0
                : _index.CountAboveCeiling(ceiling);

        if (aboveRange > 0)
        {
            _filterNotice.Text =
                $"{aboveRange} of {total} models cost more than " +
                $"{ceiling:0.##} / 1M tokens and are outside the price range.";
            _filterNotice.Visible = true;
            return;
        }

        if (withoutComparablePricing > 0)
        {
            _filterNotice.Text =
                criteria.UnknownPricing == UnknownPricingVisibility.Exclude
                    ? $"{withoutComparablePricing} of {total} models have no comparable " +
                      "per-million price and are hidden by the price filter."
                    : $"{withoutComparablePricing} of {total} models have no comparable " +
                      "per-million price. Any rate shown for them is the provider's " +
                      "raw rate, not a per-million total.";
            _filterNotice.Visible = true;
            return;
        }

        _filterNotice.Visible = false;
    }

    /// <summary>
    /// Snapshots live control state into an immutable criteria value so the
    /// filter rule itself never reads a control.
    /// </summary>
    private ModelFilterCriteria CaptureFilterCriteria()
    {
        var maximumPrice = _maxPriceFilter.Maximum / (decimal)PriceSliderScale;
        var minimumPrice = _minPriceFilter.Value / (decimal)PriceSliderScale;
        var selectedMaximumPrice =
            _maxPriceFilter.Value / (decimal)PriceSliderScale;

        var capabilityKey =
            (_capabilityFilter.SelectedItem as CapabilityFilterChoice)?.Key;

        var capabilityState =
            (_capabilityStateFilter.SelectedItem as StateFilterChoice)?.State ??
            ModelCapabilityFilterState.Any;

        // "Show above range" lifts the comparison ceiling without disturbing the
        // slider position or the $0 free-only behaviour.
        var effectiveMaximumPrice =
            _showAboveRangeModels.Checked && _highestComparablePrice > 0m
                ? _highestComparablePrice
                : selectedMaximumPrice;

        return new ModelFilterCriteria(
            minimumPrice,
            effectiveMaximumPrice,
            IsFullPriceRange: minimumPrice <= 0m &&
                             selectedMaximumPrice >= maximumPrice,
            capabilityKey,
            capabilityState,
            _showUnpricedModels.Checked
                ? UnknownPricingVisibility.Include
                : UnknownPricingVisibility.Exclude);
    }

    private static IReadOnlyList<CapabilityFilterChoice> GetCapabilityFilterChoices() =>
    [
        new(null, "Any"),
        new(HiveCapabilityKeys.TextGeneration, "Text"),
        new(HiveCapabilityKeys.Vision, "Vision"),
        new(HiveCapabilityKeys.ToolCalling, "Tools"),
        new(HiveCapabilityKeys.StructuredOutput, "Structured"),
        new(HiveCapabilityKeys.Reasoning, "Reasoning"),
        new(HiveCapabilityKeys.Thinking, "Thinking")
    ];

    private static TrackBar CreatePriceSlider(int value) =>
        new()
        {
            Width = 120,
            Height = 32,
            Minimum = 0,
            Maximum = InitialPriceSliderValue,
            TickFrequency = PriceSliderScale * 5,
            SmallChange = 1,
            LargeChange = 20,
            Value = value,
            Margin = Padding.Empty,
            AccessibleRole = AccessibleRole.Slider,
            AccessibleDescription = "Comparable USD price per 1 million input or output tokens. Values are adjustable in $0.01 increments. Set maximum to zero to show only free-priced models."
        };

    private static Label CreatePriceValueLabel() =>
        new()
        {
            AutoSize = false,
            Width = 58,
            Height = 32,
            Margin = new Padding(2, 0, 4, 0),
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleRole = AccessibleRole.StaticText
        };

    private static Label CreateFilterLabel(string text) =>
        new()
        {
            AutoSize = true,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 6, 0),
            Padding = Padding.Empty
        };

    private static string FormatCapabilityState(CapabilityState state) =>
        state switch
        {
            CapabilityState.Supported => "✓",
            CapabilityState.Unsupported => "✕",
            _ => "—"
        };

    private static string FormatCapabilityDetailState(CapabilityState state) =>
        state switch
        {
            CapabilityState.Supported => "Supported",
            CapabilityState.Unsupported => "Unsupported",
            _ => "Unknown / unreported"
        };

    private static string FormatCapabilityState(
        ProviderModelMetadata model,
        CapabilityKey key) =>
        model.DiscoveredCapabilities
            .FirstOrDefault(item => item.Capability == key) is { } entry
                ? FormatCapabilityState(entry.State)
                : "—";

    /// <summary>
    /// Shows the comparable per-million price, or an explicit free marker.
    /// </summary>
    /// <remarks>
    /// The column states the same unit the price filter uses, so what the user
    /// filters on is what the user sees. A missing comparable rate is never shown
    /// as free.
    /// </remarks>
    private string FormatRowPrice(ModelInformationRow row)
    {
        if (ModelInformationFilter.IsFreeModel(
                row.Model,
                row.ComparablePricePerMillion))
        {
            return "Free";
        }

        return row.ComparablePricePerMillion is null
            ? "Not known"
            : "$" + row.ComparablePricePerMillion.Value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
    }

    private Color? GetPriceColor(ModelInformationRow row)
    {
        if (ModelInformationFilter.IsFreeModel(
                row.Model,
                row.ComparablePricePerMillion))
        {
            return _themeManager.Theme.VisualStates.Success;
        }

        return row.ComparablePricePerMillion is null
            ? _themeManager.Theme.Palette.MutedText
            : null;
    }

    private static string FormatRowContext(ProviderModelMetadata model) =>
        model.Limits?.ContextWindowTokens is { } tokens
            ? FormatTokenCount(tokens)
            : "—";

    private static string FormatTokenCount(long tokens)
    {
        if (tokens >= 1_000_000)
        {
            return (tokens / 1_000_000d)
                .ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (tokens >= 1_000)
        {
            return (tokens / 1_000d)
                .ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return tokens.ToString(CultureInfo.InvariantCulture);
    }

    private Color? GetCapabilityStateColor(CapabilityState state) =>
        state switch
        {
            CapabilityState.Supported => _themeManager.Theme.VisualStates.Success,
            CapabilityState.Unsupported => _themeManager.Theme.Palette.MutedText,
            _ => null
        };

    private static string FindCapabilityDetail(
        ProviderModelMetadata model,
        CapabilityKey key) =>
        model.DiscoveredCapabilities
            .FirstOrDefault(item => item.Capability == key) is { } entry
                ? FormatCapabilityDetailState(entry.State)
                : "Unknown / unreported";

    private Color? GetCapabilityStateColor(
        ProviderModelMetadata model,
        CapabilityKey key)
    {
        var state = model.DiscoveredCapabilities
            .FirstOrDefault(item => item.Capability == key)
            ?.State;

        return state switch
        {
            CapabilityState.Supported => _themeManager.Theme.VisualStates.Success,
            CapabilityState.Unsupported => _themeManager.Theme.VisualStates.Error,
            _ => _themeManager.Theme.Palette.MutedText
        };
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
            _page.SetStatus(
                "Select a Provider, Account, and endpoint first.",
                HiveStatusTone.Warning);
            return;
        }

        var version = Interlocked.Increment(ref _operationVersion);
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _operationCts, cts);
        previous?.Cancel();

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
                _page.SetStatus(
                    $"Discovery did not complete ({result.Error!.Code}).",
                    HiveStatusTone.Error);
                ReportError(
                    new InvalidOperationException(result.Error.Message),
                    "Provider model discovery could not be completed.");
                return;
            }

            _snapshot = result.Value!;
            ConfigurePriceFiltersForSnapshot();
            await ApplySnapshotAsync().ConfigureAwait(true);
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

            _page.SetStatus(
                "Model information could not be loaded.",
                HiveStatusTone.Error);
            ReportError(exception, "Provider model discovery could not be completed.");
        }
        finally
        {
            if (ReferenceEquals(_operationCts, cts))
                Interlocked.CompareExchange(ref _operationCts, null, cts);

            cts.Dispose();
        }
    }

    private async Task ApplySnapshotAsync()
    {
        if (_snapshot is null)
            return;

        await _page.RefreshAsync().ConfigureAwait(true);

        if (_page.ListView.Items.Count > 0)
        {
            var firstItem = _page.ListView.Items[0];
            firstItem.Selected = true;
            firstItem.Focused = true;
        }

        if (_page.SelectedItem is ModelInformationRow row)
            RenderModelDetails(row);
        else
            RenderNoModelDetails();

    }

    private Task<IReadOnlyList<ModelInformationRow>> LoadModelsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_snapshot is null)
            return Task.FromResult<IReadOnlyList<ModelInformationRow>>(
                Array.Empty<ModelInformationRow>());

        return Task.FromResult<IReadOnlyList<ModelInformationRow>>(
            CreateModelRows(CaptureFilterCriteria()));
    }

    private void ModelsListSelectionChanged(
        object? sender,
        ListViewItemSelectionChangedEventArgs e)
    {
        if (!e.IsSelected || _updatingModelList)
            return;

        if (_page.SelectedItem is ModelInformationRow row)
        {
            RenderModelDetails(row);
            return;
        }

        if (_snapshot?.Models.Count == 0)
            RenderNoModelDetails();
    }

    private async Task<ModelInformationRow?> AddSelectedModelToFavoritesAsync(
        ModelInformationRow? _,
        CancellationToken cancellationToken)
    {
        var row = _page.SelectedItem;
        if (row is null)
            return null;

        if (row.ExecutionTarget is null)
        {
            HiveMessageBox.ShowInformation(
                FindForm(),
                "The selected discovered model does not map to an ExecutionTarget for this endpoint.",
                "Add to Favorites");
            return null;
        }

        if (_favoriteTargetIdSet.Contains(row.ExecutionTarget.Id))
        {
            _page.SetStatus(
                "The selected ExecutionTarget is already a favorite.",
                HiveStatusTone.Neutral);
            return null;
        }

        var displayName = row.Model.DisplayName ?? row.Model.ModelId;
        var confirmed = FavoriteConfirmationOverride is { } confirmationOverride
            ? confirmationOverride(displayName)
            : HiveMessageBox.ShowQuestion(
                    FindForm(),
                    $"Add '{displayName}' to Favorite Execution Targets?",
                    "Add to Favorites",
                    MessageBoxButtons.YesNo,
                    _themeManager) == DialogResult.Yes;

        if (!confirmed)
            return null;

        var updatedIds = _favoriteExecutionTargetIds
            .Append(row.ExecutionTarget.Id)
            .ToArray();

        var result = await _management
            .ReplaceFavoriteExecutionTargetIdsAsync(
                updatedIds,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        _favoriteExecutionTargetIds = result.Value!;
        _favoriteTargetIdSet = _favoriteExecutionTargetIds.ToHashSet();

        row.IsFavorite = true;

        var selectedItem = _page.ListView.SelectedItems
            .Cast<ListViewItem>()
            .SingleOrDefault(item =>
                ReferenceEquals(item.Tag, row));

        if (selectedItem is not null)
        {
            selectedItem.SubItems[0].Text = FormatModelName(row);
            _page.ListView.Invalidate(selectedItem.Bounds);
        }

        _page.SetStatus(
            "ExecutionTarget added to Favorites.",
            HiveStatusTone.Neutral);

        return null;
    }

    private ExecutionTarget? ResolveExecutionTarget(
        ProviderModelMetadata model) =>
        _selectedEndpoint is null
            ? null
            : _executionTargets.FirstOrDefault(target =>
                EndpointsEqual(target.Endpoint, _selectedEndpoint) &&
                (string.Equals(
                     target.Model,
                     model.ModelId,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     target.Deployment,
                     model.ModelId,
                     StringComparison.OrdinalIgnoreCase)));

    private void ClearObservation()
    {
        CancelOperation();
        _snapshot = null;
        _executionTargets = Array.Empty<ExecutionTarget>();
        _page.ListView.Items.Clear();
        RenderNoModelDetails();
    }

    private void RenderNoModelDetails()
    {
        if (IsDisposed || Disposing)
            return;

        HideAllDetailSections();
        _operationalDetailsSection.Surface.Visible = true;

        var operational = _snapshot?.Operational;
        var selectionMessage = _snapshot is null
            ? "Choose a Provider, Account, and endpoint, then refresh."
            : _snapshot.Models.Count == 0
                ? "No model metadata was returned for this observation."
                : "Select a model from the discovered catalog.";

        _detailsTitle.Text = "No model selected";
        _detailsSummary.Text =
            "Model Information is read-only discovery evidence. " +
            selectionMessage;

        SetSectionLines(
            _operationalDetailsSection,
            $"Discovery state: {_snapshot?.ModelEnumerationState.ToString() ?? "Not started"}",
            $"Provider availability: {operational?.Availability.ToString() ?? "—"}",
            $"Provider health: {operational?.Health.ToString() ?? "—"}",
            $"Observed: {operational?.ObservedAtUtc.ToString("O") ?? "—"}",
            $"Stale after: {operational?.StaleAfterUtc.ToString("O") ?? "—"}");

        ResizeDetailsContent();
    }

    private void RenderModelDetails(ModelInformationRow row)
    {
        if (IsDisposed || Disposing)
            return;

        var model = row.Model;
        ShowAllDetailSections();

        _detailsTitle.Text = model.DisplayName ?? model.ModelId;
        _detailsSummary.Text = string.IsNullOrWhiteSpace(model.Description)
            ? "Provider-reported model metadata used for inspection only."
            : model.Description!;

        SetSectionLines(
            _identityDetailsSection,
            $"Model ID: {model.ModelId}",
            $"Provider attribution: {model.OwnedBy ?? "—"}",
            $"Family: {model.Family ?? "—"}",
            $"Type: {model.ModelType ?? "—"}",
            $"Category: {model.Category ?? "—"}",
            $"Version: {model.Version ?? "—"}",
            $"Operational state: {model.OperationalState ?? "—"}",
            $"Created: {FormatDateTime(model.CreatedAtUtc)}",
            $"ExecutionTarget mapping: {(row.ExecutionTarget is null ? "No matching target" : "Matched durable target")}");

        SetSectionLines(
            _inputsDetailsSection,
            $"Modalities: {FormatList(model.InputModalities)}");

        SetSectionLines(
            _outputsDetailsSection,
            $"Modalities: {FormatList(model.OutputModalities)}");

        SetSectionChips(
            _capabilitiesDetailsSection,
            GetCapabilityDisplayEntries(model));

        SetSectionLines(
            _reasoningDetailsSection,
            $"Reasoning: {FormatCapabilityDetailState(GetCapabilityState(model, HiveCapabilityKeys.Reasoning))}",
            $"Thinking: {FormatCapabilityDetailState(GetCapabilityState(model, HiveCapabilityKeys.Thinking))}",
            $"Available levels: {FormatList(model.ThinkingOptions)}",
            $"Default level: {model.DefaultThinkingLevel ?? "—"}");

        if (model.Limits is null)
        {
            SetSectionLines(
                _limitsDetailsSection,
                "No model limits were reported. Missing limits are not treated as unlimited.");
        }
        else
        {
            var limitLines = new List<string>
            {
                $"Context window: {FormatTokenLimit(model.Limits.ContextWindowTokens)}",
                $"Maximum input: {FormatTokenLimit(model.Limits.MaxInputTokens)}",
                $"Maximum output: {FormatTokenLimit(model.Limits.MaxOutputTokens)}"
            };

            if (model.Limits.AdditionalConstraints.Count > 0)
            {
                limitLines.Add("Additional constraints:");
                limitLines.AddRange(
                    model.Limits.AdditionalConstraints
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair =>
                            $"  {pair.Key}: {FormatJsonValue(pair.Value)}"));
            }

            SetSectionLines(
                _limitsDetailsSection,
                limitLines.ToArray());
        }

        RenderPricingDetails(model);
        RenderOperationalDetails(model);

        if (model.ExtensionData.Count == 0)
        {
            SetSectionLines(
                _additionalDetailsSection,
                "No additional provider information was reported.");
        }
        else
        {
            SetSectionLines(
                _additionalDetailsSection,
                model.ExtensionData
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair =>
                        $"{pair.Key}: {FormatJsonValue(pair.Value)}")
                    .ToArray());
        }

        ResizeDetailsContent();
    }

    private void RenderPricingDetails(ProviderModelMetadata model)
    {
        var lines = new List<string>();

        if (model.Pricing is null)
        {
            SetSectionLines(
                _pricingDetailsSection,
                "Decision price: Not known",
                "Missing pricing is not evidence that the model is free.");
            return;
        }

        var comparable =
            ModelInformationFilter.GetComparableTokenPricePerMillion(model);
        var isFree =
            ModelInformationFilter.IsFreeModel(model, comparable);

        lines.Add(
            isFree
                ? "Decision price: Free"
                : comparable is { } value
                    ? $"Decision price: ${value:0.00} / 1M tokens"
                    : "Decision price: Not comparable");
        lines.Add(
            $"Explicit free evidence: {(model.Pricing.ExplicitFreeEvidence ? "Yes" : "No")}");

        if (comparable is null)
        {
            lines.Add(
                "Comparable pricing requires a known USD source quantity; Hive does not guess one.");
        }

        if (model.Pricing.Prices.Count == 0)
        {
            lines.Add("Base rates: Not reported");
        }
        else
        {
            lines.Add("Base rates:");
            lines.AddRange(
                model.Pricing.Prices
                    .OrderBy(price => price.BillingUnit, StringComparer.Ordinal)
                    .Select(price => $"  {FormatPriceLine(price)}"));
        }

        if (model.Pricing.Variants.Count > 0)
        {
            lines.Add("Tiered rates:");
            foreach (var variant in model.Pricing.Variants)
            {
                var conditionText = variant.Conditions.Count == 0
                    ? "condition not reported"
                    : string.Join(
                        ", ",
                        variant.Conditions
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair =>
                                $"{FormatPricingConditionName(pair.Key)}={pair.Value}"));

                lines.Add(
                    $"  {variant.Key} ({conditionText})" +
                    (variant.IsDefault ? " — default" : string.Empty));

                lines.AddRange(
                    variant.Prices
                        .OrderBy(price => price.BillingUnit, StringComparer.Ordinal)
                        .Select(price => $"    {FormatPriceLine(price)}"));
            }
        }

        SetSectionLines(_pricingDetailsSection, lines.ToArray());
    }

    private void RenderOperationalDetails(ProviderModelMetadata model)
    {
        SetSectionLines(
            _operationalDetailsSection,
            $"Availability: {model.Availability}",
            $"Health: {model.Health}",
            $"Provider-reported state: {model.OperationalState ?? "—"}",
            $"Observed: {FormatDateTime(model.ObservedAtUtc)}",
            $"Stale after: {FormatDateTime(model.StaleAfterUtc)}");
    }

    private void HideAllDetailSections()
    {
        foreach (var section in GetDetailSections())
            section.Surface.Visible = false;
    }

    private void ShowAllDetailSections()
    {
        foreach (var section in GetDetailSections())
            section.Surface.Visible = true;
    }

    private IReadOnlyList<DetailSection> GetDetailSections() =>
    [
        _identityDetailsSection,
        _inputsDetailsSection,
        _outputsDetailsSection,
        _capabilitiesDetailsSection,
        _reasoningDetailsSection,
        _limitsDetailsSection,
        _pricingDetailsSection,
        _operationalDetailsSection,
        _additionalDetailsSection
    ];

    private DetailSection CreateDetailSection(string title)
    {
        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = title,
            Padding = Padding.Empty,
            Margin = new Padding(0, 0, 0, 7),
            Font = new Font(
                _themeManager.Theme.Typography.FontFamily,
                _themeManager.Theme.Typography.SectionSize,
                FontStyle.Bold),
            AccessibleRole = AccessibleRole.Heading
        };

        var body = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var surface = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(10, 8, 10, 10),
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = title + " section"
        };

        surface.Controls.Add(body);
        surface.Controls.Add(titleLabel);

        return new DetailSection(surface, titleLabel, body);
    }

    private void SetSectionLines(
        DetailSection section,
        params string[] lines)
    {
        ClearSectionBody(section);

        foreach (var line in lines)
        {
            section.Body.Controls.Add(
                new Label
                {
                    AutoSize = true,
                    Text = line,
                    Margin = new Padding(0, 0, 0, 6),
                    Padding = Padding.Empty,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AccessibleRole = AccessibleRole.StaticText
                });
        }
    }

    private void SetSectionChips(
        DetailSection section,
        IReadOnlyList<(string Name, CapabilityState State)> entries)
    {
        ClearSectionBody(section);
        section.Body.FlowDirection = FlowDirection.LeftToRight;
        section.Body.WrapContents = true;

        foreach (var entry in entries)
        {
            var chip = new Label
            {
                AutoSize = true,
                Text = $"{entry.Name} — {FormatCapabilityDetailState(entry.State)}",
                Margin = new Padding(0, 0, 7, 7),
                Padding = new Padding(7, 4, 7, 4),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(
                    _themeManager.Theme.Typography.FontFamily,
                    Math.Max(8f, _themeManager.Theme.Typography.SectionSize - 1f),
                    FontStyle.Bold),
                AccessibleRole = AccessibleRole.StaticText
            };

            chip.ForeColor = entry.State switch
            {
                CapabilityState.Supported =>
                    _themeManager.Theme.VisualStates.Success,
                CapabilityState.Unsupported =>
                    _themeManager.Theme.VisualStates.Error,
                _ =>
                    _themeManager.Theme.Palette.MutedText
            };

            section.Body.Controls.Add(chip);
        }
    }

    private static void ClearSectionBody(DetailSection section)
    {
        foreach (Control control in section.Body.Controls)
            control.Dispose();

        section.Body.Controls.Clear();
        section.Body.FlowDirection = FlowDirection.TopDown;
        section.Body.WrapContents = false;
    }

    private static IReadOnlyList<(string Name, CapabilityState State)>
        GetCapabilityDisplayEntries(ProviderModelMetadata model)
    {
        var known = new[]
        {
            HiveCapabilityKeys.TextGeneration,
            HiveCapabilityKeys.Vision,
            HiveCapabilityKeys.ToolCalling,
            HiveCapabilityKeys.StructuredOutput,
            HiveCapabilityKeys.Reasoning,
            HiveCapabilityKeys.Thinking
        };

        var knownSet = known.ToHashSet();
        var entries = new List<(string Name, CapabilityState State)>();

        foreach (var key in known)
        {
            entries.Add((
                FormatCapabilityName(key),
                GetCapabilityState(model, key)));
        }

        entries.AddRange(
            model.DiscoveredCapabilities
                .Where(item => !knownSet.Contains(item.Capability))
                .OrderBy(item => item.Capability.Value, StringComparer.Ordinal)
                .Select(item =>
                    (FormatCapabilityName(item.Capability), item.State)));

        return entries;
    }

    private static CapabilityState GetCapabilityState(
        ProviderModelMetadata model,
        CapabilityKey key) =>
        model.DiscoveredCapabilities
            .FirstOrDefault(item => item.Capability == key)
            ?.State ??
        CapabilityState.Unknown;

    private static string FormatCapabilityName(CapabilityKey key) =>
        key == HiveCapabilityKeys.TextGeneration
            ? "Text generation"
            : key == HiveCapabilityKeys.ToolCalling
                ? "Tool calling"
                : key == HiveCapabilityKeys.StructuredOutput
                    ? "Structured output"
                    : key == HiveCapabilityKeys.Vision
                        ? "Vision"
                        : key == HiveCapabilityKeys.Reasoning
                            ? "Reasoning"
                            : key == HiveCapabilityKeys.Thinking
                                ? "Thinking"
                                : key.Value;

    private static string FormatTokenLimit(long? tokens) =>
        tokens is { } value
            ? $"{value.ToString("N0", CultureInfo.InvariantCulture)} tokens"
            : "Not reported";

    private static string FormatDateTime(DateTimeOffset? value) =>
        value?.ToString("O") ?? "Not reported";

    private static string FormatPriceLine(ProviderModelPrice price)
    {
        var currency = price.Currency ?? "currency not reported";
        var amount = price.Price.ToString("0.##########", CultureInfo.InvariantCulture);

        return price.UnitQuantity is { } quantity
            ? $"{FormatBillingUnit(price.BillingUnit)}: {amount} {currency} per {FormatUnitQuantity(quantity)} units"
            : $"{FormatBillingUnit(price.BillingUnit)}: {amount} {currency} (source quantity not reported)";
    }

    private static string FormatUnitQuantity(decimal quantity)
    {
        if (quantity >= 1_000_000m)
            return (quantity / 1_000_000m).ToString("0.####", CultureInfo.InvariantCulture) + "M";

        if (quantity >= 1_000m)
            return (quantity / 1_000m).ToString("0.####", CultureInfo.InvariantCulture) + "K";

        return quantity.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string FormatBillingUnit(string billingUnit) =>
        billingUnit switch
        {
            "input_token" => "Input tokens",
            "output_token" => "Output tokens",
            "reasoning_token" => "Reasoning tokens",
            "cache_read_token" => "Cached input tokens",
            "cache_write_token" => "Cached output tokens",
            "image" => "Images",
            "audio" => "Audio",
            "request" => "Requests",
            _ => billingUnit,
        };

    private static string FormatPricingConditionName(string name) =>
        name switch
        {
            "min_prompt_tokens" => "minimum prompt tokens",
            "min_input_tokens" => "minimum input tokens",
            "min_tokens" => "minimum tokens",
            _ => name,
        };

    private void ResizeDetailsContent()
    {
        if (IsDisposed || Disposing)
            return;

        var availableWidth = Math.Max(
            300,
            _detailsScrollHost.ClientSize.Width -
            _detailsContent.Padding.Horizontal -
            8);

        _detailsTitle.MaximumSize = new Size(availableWidth, 0);
        _detailsSummary.MaximumSize = new Size(availableWidth, 0);
        _detailsSections.Width = availableWidth;

        foreach (var section in GetDetailSections())
        {
            section.Surface.Width = availableWidth;
            section.Body.Width =
                Math.Max(240, availableWidth - section.Surface.Padding.Horizontal);

            foreach (Control control in section.Body.Controls)
            {
                control.MaximumSize =
                    new Size(
                        Math.Max(240, availableWidth - 44),
                        0);
            }
        }

        _detailsContent.PerformLayout();
        _detailsScrollHost.Synchronize();
    }

    private void DetailsScrollHostOnResize(object? sender, EventArgs e) =>
        ResizeDetailsContent();
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

    private static string FormatModelName(
        ModelInformationRow row) =>
        row.IsFavorite
            ? $"★ {row.Model.DisplayName ?? row.Model.ModelId}"
            : row.Model.DisplayName ?? row.Model.ModelId;

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0
            ? "—"
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

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (IsDisposed || Disposing)
            return;

        _themeManager.Apply(this);

        if (_page.SelectedItem is ModelInformationRow row)
            RenderModelDetails(row);
        else
            RenderNoModelDetails();
    }

    private void PageOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        _page.SetStatus(
            "Operation failed. See technical details.",
            HiveStatusTone.Error);

        ReportError(
            e.Exception,
            "The Model Information operation could not be completed.");
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
            _page.OperationFailed -= PageOperationFailed;
            _page.ListView.ItemSelectionChanged -= ModelsListSelectionChanged;
            _minPriceFilter.ValueChanged -= PriceFilterValueChanged;
            _maxPriceFilter.ValueChanged -= PriceFilterValueChanged;
            _capabilityFilter.SelectedIndexChanged -= FilterChanged;
            _capabilityStateFilter.SelectedIndexChanged -= FilterChanged;
            _showUnpricedModels.CheckedChanged -= FilterChanged;
            _showAboveRangeModels.CheckedChanged -= FilterChanged;
            _detailsScrollHost.Resize -= DetailsScrollHostOnResize;
            _mainSplit.SizeChanged -= MainSplitSizeChanged;
            CancelOperation();
        }

        base.Dispose(disposing);
    }
}