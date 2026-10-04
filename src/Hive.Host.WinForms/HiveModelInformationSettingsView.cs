using System.Drawing;
using System.Globalization;
using System.Text;
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

    private enum ModelCapabilityFilterState
    {
        Any,
        Supported,
        Unsupported,
        Unknown
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

    internal sealed class ModelInformationRow
    {
        public ModelInformationRow(
            ProviderModelMetadata model,
            ExecutionTarget? executionTarget,
            bool isFavorite)
        {
            Model = model;
            ExecutionTarget = executionTarget;
            IsFavorite = isFavorite;
        }

        public ProviderModelMetadata Model { get; }

        public ExecutionTarget? ExecutionTarget { get; }

        public bool IsFavorite { get; set; }
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
    private readonly Label _detailsBody;
    private const int PriceSliderScale = 100;
    private const decimal MinimumPriceSliderMaximum = 5m;
    private const decimal PriceSliderMaximumStep = 5m;
    private readonly TrackBar _minPriceFilter;
    private readonly TrackBar _maxPriceFilter;
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

        _page.SetColumns(
            new HiveCrudColumn<ModelInformationRow>("Model", 250, FormatModelName),
            new HiveCrudColumn<ModelInformationRow>(
                "Text",
                50,
                row => FormatCapabilityState(row.Model, HiveCapabilityKeys.TextGeneration),
                row => GetCapabilityStateColor(row.Model, HiveCapabilityKeys.TextGeneration)),
            new HiveCrudColumn<ModelInformationRow>(
                "Vision",
                58,
                row => FormatCapabilityState(row.Model, HiveCapabilityKeys.Vision),
                row => GetCapabilityStateColor(row.Model, HiveCapabilityKeys.Vision)),
            new HiveCrudColumn<ModelInformationRow>(
                "Tools",
                52,
                row => FormatCapabilityState(row.Model, HiveCapabilityKeys.ToolCalling),
                row => GetCapabilityStateColor(row.Model, HiveCapabilityKeys.ToolCalling)),
            new HiveCrudColumn<ModelInformationRow>(
                "Structured",
                76,
                row => FormatCapabilityState(row.Model, HiveCapabilityKeys.StructuredOutput),
                row => GetCapabilityStateColor(row.Model, HiveCapabilityKeys.StructuredOutput)),
            new HiveCrudColumn<ModelInformationRow>(
                "Reasoning",
                72,
                row => FormatCapabilityState(row.Model, HiveCapabilityKeys.Reasoning),
                row => GetCapabilityStateColor(row.Model, HiveCapabilityKeys.Reasoning)),
            new HiveCrudColumn<ModelInformationRow>(
                "Thinking",
                64,
                row => FormatCapabilityState(row.Model, HiveCapabilityKeys.Thinking),
                row => GetCapabilityStateColor(row.Model, HiveCapabilityKeys.Thinking)));

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
            checked((int)(MinimumPriceSliderMaximum * PriceSliderScale)));

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

        _minPriceFilter.ValueChanged += PriceFilterValueChanged;
        _maxPriceFilter.ValueChanged += PriceFilterValueChanged;
        _capabilityFilter.SelectedIndexChanged += FilterChanged;
        _capabilityStateFilter.SelectedIndexChanged += FilterChanged;

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

        _detailsBody = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty,
            AccessibleRole = AccessibleRole.StaticText
        };

        _detailsContent.Controls.Add(_detailsBody);
        _detailsContent.Controls.Add(_detailsTitle);

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
        contextAndFilters.Controls.Add(contextCard, 0, 0);
        contextAndFilters.Controls.Add(filterBar, 0, 1);

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

        var highestComparablePrice =
            _snapshot.Models
                .Select(GetComparableTokenPricePerMillion)
                .Where(static price => price is not null)
                .Select(static price => price!.Value)
                .DefaultIfEmpty(0m)
                .Max();

        var maximumPrice =
            highestComparablePrice <= 0m
                ? MinimumPriceSliderMaximum
                : Math.Max(
                    MinimumPriceSliderMaximum,
                    Math.Ceiling(
                        highestComparablePrice / PriceSliderMaximumStep) *
                    PriceSliderMaximumStep);

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

        var rows = CreateModelRows();

        _updatingModelList = true;
        try
        {
            _page.SetItemsForView(rows);
        }
        finally
        {
            _updatingModelList = false;
        }

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
                RenderModelDetails(row.Model);
        }
        else
        {
            RenderNoModelDetails();
        }
    }

    private bool MatchesFilters(ProviderModelMetadata model)
    {
        var price = GetComparableTokenPricePerMillion(model);
        var minPrice = _minPriceFilter.Value / (decimal)PriceSliderScale;
        var maxPrice = _maxPriceFilter.Value / (decimal)PriceSliderScale;

        if (price is not null &&
            (price.Value < minPrice ||
             price.Value > maxPrice))
        {
            return false;
        }

        if (price is null)
        {
            var fullRangeMaximum =
                _maxPriceFilter.Maximum / (decimal)PriceSliderScale;

            if (minPrice > 0m ||
                maxPrice < fullRangeMaximum)
            {
                return false;
            }

            return true;
        }

        if (maxPrice == 0m &&
            price != 0m)
        {
            return false;
        }

        if (_capabilityFilter.SelectedItem is not CapabilityFilterChoice
            {
                Key: { } key
            })
        {
            return true;
        }

        var desiredState = _capabilityStateFilter.SelectedItem is StateFilterChoice state
            ? state.State
            : ModelCapabilityFilterState.Any;

        if (desiredState == ModelCapabilityFilterState.Any)
            return true;

        var actual = model.DiscoveredCapabilities
            .FirstOrDefault(item => item.Capability == key)
            ?.State;

        return desiredState switch
        {
            ModelCapabilityFilterState.Supported => actual == CapabilityState.Supported,
            ModelCapabilityFilterState.Unsupported => actual == CapabilityState.Unsupported,
            ModelCapabilityFilterState.Unknown =>
                actual == CapabilityState.Unknown || actual is null,
            _ => true
        };
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
            Maximum = checked((int)(MinimumPriceSliderMaximum * PriceSliderScale)),
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
            RenderModelDetails(row.Model);
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
            CreateModelRows());
    }

    private IReadOnlyList<ModelInformationRow> CreateModelRows()
    {
        if (_snapshot is null)
            return Array.Empty<ModelInformationRow>();

        return _snapshot.Models
            .Where(MatchesFilters)
            .Select(model =>
            {
                var target = ResolveExecutionTarget(model);
                return new ModelInformationRow(
                    model,
                    target,
                    target is not null &&
                    _favoriteTargetIdSet.Contains(target.Id));
            })
            .ToArray();
    }

    private void ModelsListSelectionChanged(
        object? sender,
        ListViewItemSelectionChangedEventArgs e)
    {
        if (!e.IsSelected || _updatingModelList)
            return;

        if (_page.SelectedItem is ModelInformationRow row)
        {
            RenderModelDetails(row.Model);
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
        var operational = _snapshot?.Operational;

        var message = _snapshot is null
            ? "Choose a Provider, Account, and endpoint, then refresh."
            : _snapshot.Models.Count == 0
                ? "No model metadata is available for this observation. Hive does not infer a model when enumeration returns none."
                : "Select a model from the discovered catalog.";

        SetDetails(
            "No model selected",
            string.Join(
                Environment.NewLine,
                $"Discovery state: {_snapshot?.ModelEnumerationState.ToString() ?? "Not started"}",
                $"Provider availability: {operational?.Availability.ToString() ?? "—"}",
                $"Provider health: {operational?.Health.ToString() ?? "—"}",
                $"Observed: {operational?.ObservedAtUtc.ToString("O") ?? "—"}",
                $"Stale after: {operational?.StaleAfterUtc.ToString("O") ?? "—"}",
                message));
    }

    private void RenderModelDetails(ProviderModelMetadata model)
    {
        var builder = new StringBuilder();

        AppendDetailSection(
            builder,
            "Identity",
            $"Model ID: {model.ModelId}",
            $"Owner / attribution: {model.OwnedBy ?? "—"}",
            $"Family: {model.Family ?? "—"}",
            $"Type: {model.ModelType ?? "—"}",
            $"Category: {model.Category ?? "—"}",
            $"Version: {model.Version ?? "—"}",
            $"Operational state: {model.OperationalState ?? "—"}",
            $"Created: {model.CreatedAtUtc?.ToString("O") ?? "—"}",
            $"Description: {model.Description ?? "—"}");

        AppendDetailSection(
            builder,
            "Inputs & Outputs",
            $"Input modalities: {FormatList(model.InputModalities)}",
            $"Output modalities: {FormatList(model.OutputModalities)}");

        var capabilityLines = model.DiscoveredCapabilities.Count == 0
            ? new[] { "State: —" }
            : model.DiscoveredCapabilities
                .OrderBy(item => item.Capability.Value, StringComparer.Ordinal)
                .Select(item =>
                    $"{item.Capability.Value}: {FormatCapabilityDetailState(item.State)}")
                .ToArray();
        AppendDetailSection(builder, "Capabilities", capabilityLines);

        AppendDetailSection(
            builder,
            "Reasoning & Thinking",
            $"Reasoning: {FindCapabilityDetail(model, HiveCapabilityKeys.Reasoning)}",
            $"Thinking: {FindCapabilityDetail(model, HiveCapabilityKeys.Thinking)}",
            $"Options: {FormatList(model.ThinkingOptions)}",
            $"Default: {model.DefaultThinkingLevel ?? "—"}");

        if (model.Limits is null)
        {
            AppendDetailSection(builder, "Limits", "Status: —");
        }
        else
        {
            AppendDetailSection(
                builder,
                "Limits",
                $"Context window tokens: {model.Limits.ContextWindowTokens?.ToString() ?? "—"}",
                $"Max input tokens: {model.Limits.MaxInputTokens?.ToString() ?? "—"}",
                $"Max output tokens: {model.Limits.MaxOutputTokens?.ToString() ?? "—"}",
                $"Additional constraints: {FormatJsonDictionary(model.Limits.AdditionalConstraints)}");
        }

        if (model.Pricing is null)
        {
            AppendDetailSection(
                builder,
                "Pricing & economics",
                "Status: —",
                "Interpretation: Missing pricing is not evidence that the model is free.");
        }
        else
        {
            var comparablePrice = GetComparableTokenPricePerMillion(model);
            var pricingLines = new List<string>
            {
                $"Explicit free evidence: {(model.Pricing.ExplicitFreeEvidence ? "True" : "False")}",
                $"Filter-comparable input/output token rate (highest): {(comparablePrice is { } value ? $"${value:0.00} / 1M tokens" : "—")}"
            };

            if (model.Pricing.Prices.Count == 0)
            {
                pricingLines.Add("Rates: —");
            }
            else
            {
                pricingLines.AddRange(
                    model.Pricing.Prices.Select(price =>
                    {
                        var quantity = price.UnitQuantity is { } value
                            ? $" per {value:0.####}"
                            : string.Empty;
                        return $"{price.BillingUnit}: {price.Price:0.##########} {price.Currency ?? "—"}{quantity}";
                    }));
            }

            AppendDetailSection(
                builder,
                "Pricing & economics",
                pricingLines.ToArray());
        }

        AppendDetailSection(
            builder,
            "Operational state",
            $"Availability: {model.Availability}",
            $"Health: {model.Health}",
            $"Observed: {model.ObservedAtUtc?.ToString("O") ?? "Snapshot observation timestamp"}",
            $"Stale after: {model.StaleAfterUtc?.ToString("O") ?? "Snapshot freshness boundary"}");

        if (model.ExtensionData.Count == 0)
        {
            AppendDetailSection(
                builder,
                "Additional provider information",
                "Status: —");
        }
        else
        {
            AppendDetailSection(
                builder,
                "ADDITIONAL PROVIDER INFORMATION",
                model.ExtensionData
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}: {FormatJsonValue(pair.Value)}")
                    .ToArray());
        }

        SetDetails(
            model.DisplayName ?? model.ModelId,
            builder.ToString().TrimEnd());
    }

    private void SetDetails(string title, string body)
    {
        if (IsDisposed || Disposing)
            return;

        _detailsTitle.Text = title;
        _detailsBody.Text = body;
        ResizeDetailsContent();
    }

    private static decimal? GetComparableTokenPricePerMillion(
        ProviderModelMetadata model)
    {
        if (model.Pricing?.ExplicitFreeEvidence == true &&
            model.Pricing.Prices.All(price => !IsTokenBillingUnit(price.BillingUnit)))
        {
            return 0m;
        }

        var values = model.Pricing?.Prices
            .Where(static price =>
                IsTokenBillingUnit(price.BillingUnit) &&
                string.Equals(price.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            .Select(static price =>
            {
                var quantity = price.UnitQuantity ?? 1m;
                return quantity <= 0m
                    ? decimal.MaxValue
                    : price.Price * 1_000_000m / quantity;
            })
            .ToArray();

        return values is { Length: > 0 }
            ? values.Max()
            : null;
    }

    private static bool IsTokenBillingUnit(string billingUnit) =>
        string.Equals(billingUnit, "input_token", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(billingUnit, "output_token", StringComparison.OrdinalIgnoreCase);

    private static void AppendDetailSection(
        StringBuilder builder,
        string title,
        params string[] lines)
    {
        if (builder.Length > 0)
            builder.AppendLine().AppendLine();

        builder.AppendLine(title);
        foreach (var line in lines)
            builder.AppendLine(line);
    }

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
        _detailsBody.MaximumSize = new Size(availableWidth, 0);

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
            _detailsScrollHost.Resize -= DetailsScrollHostOnResize;
            _mainSplit.SizeChanged -= MainSplitSizeChanged;
            CancelOperation();
        }

        base.Dispose(disposing);
    }
}
