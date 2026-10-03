using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveProvidersSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveCrudPage<ConfiguredProviderRow> _page;
    private readonly HiveButton _refreshButton;
    private readonly HiveButton _advancedButton;
    private readonly HiveTabControl _tabs;
    private readonly HiveFavoriteExecutionTargetsSettingsView _favoritesPage;
    private CancellationTokenSource? _refreshCts;
    private bool _favoritesInitialized;
    private int _refreshRunning;

    public HiveProvidersSettingsView(
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

        _page = new HiveCrudPage<ConfiguredProviderRow>
        {
            Title = "Providers",
            Description =
                "Configure built-in providers here. Hive creates and maintains the underlying account and execution-target resources automatically.",
            PageSize = 25,
            AllowAdd = true,
            AllowEdit = true,
            AllowDelete = true,
            AddButtonText = "Add Provider",
            ShowRefresh = false,
            ShowSearch = true,
            SearchPlaceholder = "Search providers..."
        };

        _page.SetColumns(
            new HiveCrudColumn<ConfiguredProviderRow>(
                "Lifecycle",
                110,
                item => HiveLifecyclePresentation.Format(item.Provider.Resource.Lifecycle.Status),
                item => HiveLifecyclePresentation.Color(
                    item.Provider.Resource.Lifecycle.Status,
                    _themeManager)),
            new HiveCrudColumn<ConfiguredProviderRow>(
                "Provider",
                210,
                item => item.Provider.DisplayName),
            new HiveCrudColumn<ConfiguredProviderRow>(
                "Credential",
                120,
                item => item.CredentialStatus),
            new HiveCrudColumn<ConfiguredProviderRow>(
                "Automatic Targets",
                130,
                item => item.ActiveAutomaticTargetCount.ToString()),
            new HiveCrudColumn<ConfiguredProviderRow>(
                "Status",
                220,
                item => item.Status));

        _page.LoadItemsAsync = LoadAsync;
        _page.EditItemAsync = EditAsync;
        _page.DeleteItemAsync = DeleteAsync;
        _page.ActivateItemAsync = ActivateAsync;
        _page.StatusSelector = item => item.Provider.Resource.Lifecycle.Status.ToString();
        _page.CanEditItem = item =>
            item.Provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active &&
            BuiltInProviderCatalog.Find(item.Provider.Key) is
            {
                NormalOnboardingSupported: true,
                CredentialRequirement: not BuiltInProviderCredentialRequirement.None
            };
        _page.CanDeleteItem = item =>
            item.Provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active;
        _page.CanActivateItem = item =>
            item.Provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired;
        _page.GetItemDisplayName = item =>
            $"{item.Provider.DisplayName} [{item.Provider.Key}]";

        _refreshButton = CreateToolbarButton(
            "Refresh",
            HiveButtonStyle.Secondary,
            "Refresh provider discovery and automatic execution targets.");

        _advancedButton = CreateToolbarButton(
            "Advanced",
            HiveButtonStyle.Administrative,
            "Open Advanced Provider Configuration for Provider, Account / Credential, and Execution Target administration.");

        _refreshButton.Click += async (_, _) => await RefreshProvidersAsync();
        _advancedButton.Click += async (_, _) => await OpenAdvancedAsync();

        _advancedButton.Dock = DockStyle.Right;
        _refreshButton.Dock = DockStyle.Right;
        _page.ActionBarPanel.Controls.Add(_advancedButton);
        _page.ActionBarPanel.Controls.Add(_refreshButton);
        _page.OperationFailed += PageOperationFailed;

        _tabs = new HiveTabControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Provider settings sections"
        };

        var providersTab = new TabPage("Providers")
        {
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        providersTab.Controls.Add(_page);

        _favoritesPage = new HiveFavoriteExecutionTargetsSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _output);

        var favoritesTab = new TabPage("Favorite Execution Targets")
        {
            Padding = new Padding(8),
            Margin = Padding.Empty
        };
        favoritesTab.Controls.Add(_favoritesPage);

        _tabs.TabPages.Add(providersTab);
        _tabs.TabPages.Add(favoritesTab);
        _tabs.SelectedIndexChanged += TabsSelectedIndexChanged;

        Controls.Add(_tabs);
        _themeManager.Apply(this);
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        _page.RefreshAsync(cancellationToken);

    internal HiveTabControl NavigationTabs => _tabs;

    internal HiveFavoriteExecutionTargetsSettingsView FavoriteTargetsPage =>
        _favoritesPage;

    private async void TabsSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabs.SelectedIndex != 1 ||
            _favoritesPage.IsDisposed ||
            _favoritesPage.Disposing ||
            _favoritesInitialized)
        {
            return;
        }

        try
        {
            await _favoritesPage.InitializeAsync().ConfigureAwait(true);
            _favoritesInitialized = true;
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Favorite Execution Targets",
                    "The favorite execution-target settings could not be loaded.",
                    _output,
                    _themeManager);
            }
        }
    }

    private async Task<IReadOnlyList<ConfiguredProviderRow>> LoadAsync(
        CancellationToken cancellationToken)
    {
        var result = await _management
            .ListProvidersAsync(
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        var rows = new List<ConfiguredProviderRow>(result.Value!.Count);

        foreach (var provider in result.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(
                await BuildRowAsync(
                    provider,
                    cancellationToken).ConfigureAwait(true));
        }

        return rows;
    }

    private async Task<ConfiguredProviderRow> BuildRowAsync(
        Provider provider,
        CancellationToken cancellationToken)
    {
        var accounts = await _management
            .ListProviderAccountsAsync(
                provider.Id,
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (accounts.IsFailure)
            throw new InvalidOperationException(accounts.Error!.Message);

        var defaultAccount = accounts.Value!.FirstOrDefault(
            account => string.Equals(
                account.Key,
                "default",
                StringComparison.OrdinalIgnoreCase));

        var credentialConfigured = defaultAccount?.CredentialSecret is not null;
        var automaticTargets = 0;

        foreach (var account in accounts.Value!)
        {
            var targets = await _management
                .ListExecutionTargetsAsync(
                    account.Id,
                    _accessContext,
                    includeRetired: false,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            if (targets.IsFailure)
                throw new InvalidOperationException(targets.Error!.Message);

            automaticTargets += targets.Value!.Count(IsAutomaticTarget);
        }

        var catalog = BuiltInProviderCatalog.Find(provider.Key);
        var status = provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active
            ? "Retired"
            : catalog is null
                ? "Advanced Provider Configuration"
                : catalog.CredentialRequirement == BuiltInProviderCredentialRequirement.Required &&
                  !credentialConfigured
                    ? "Credential missing"
                    : automaticTargets > 0
                        ? "Configured"
                        : "Configured — discovery pending";

        var credentialStatus = catalog?.CredentialRequirement switch
        {
            BuiltInProviderCredentialRequirement.None => "Not required",
            BuiltInProviderCredentialRequirement.Optional when !credentialConfigured => "Optional — not configured",
            BuiltInProviderCredentialRequirement.Optional => "Optional — configured",
            BuiltInProviderCredentialRequirement.Required when credentialConfigured => "Configured",
            BuiltInProviderCredentialRequirement.Required => "Required — missing",
            _ => "Unknown"
        };

        return new ConfiguredProviderRow(
            provider,
            credentialStatus,
            automaticTargets,
            status);
    }

    private async Task<ConfiguredProviderRow?> EditAsync(
        ConfiguredProviderRow? row,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var editor = new HiveProviderSetupEditorForm(
            row?.Provider,
            _themeManager,
            _output);

        if (editor.ShowDialog(FindForm()) != DialogResult.OK)
            return null;

        if (string.IsNullOrWhiteSpace(editor.ProviderKey))
            throw new InvalidOperationException("A provider must be selected.");

        if (row is null)
        {
            using var credential = CreateCredential(editor.Credential);
            var result = await _management
                .ConfigureBuiltInProviderAsync(
                    editor.ProviderKey,
                    credential,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.Message);

            ReportDiscoveryOutcome(result.Value!);

            return await BuildRowAsync(
                result.Value!.Provider,
                cancellationToken).ConfigureAwait(true);
        }

        var catalog = BuiltInProviderCatalog.Find(row.Provider.Key);
        if (catalog?.NormalOnboardingSupported != true)
        {
            throw new InvalidOperationException(
                "This provider must be edited through Advanced Provider Configuration.");
        }

        using var replacement = CreateCredential(editor.Credential);

        if (replacement is null &&
            catalog.CredentialRequirement == BuiltInProviderCredentialRequirement.Required)
        {
            throw new InvalidOperationException("Enter a replacement API key.");
        }

        if (replacement is null)
            return row;

        var replaced = await _management
            .ReplaceBuiltInProviderCredentialAsync(
                row.Provider.Id,
                replacement,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (replaced.IsFailure)
            throw new InvalidOperationException(replaced.Error!.Message);

        ReportDiscoveryOutcome(replaced.Value!);

        return await BuildRowAsync(
            replaced.Value!.Provider,
            cancellationToken).ConfigureAwait(true);
    }

    private async Task DeleteAsync(
        ConfiguredProviderRow row,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .DeleteProviderAsync(
                row.Provider.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private async Task ActivateAsync(
        ConfiguredProviderRow row,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .ReactivateProviderAsync(
                row.Provider.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private async Task RefreshProvidersAsync()
    {
        if (Interlocked.Exchange(ref _refreshRunning, 1) != 0 ||
            IsDisposed ||
            Disposing)
        {
            return;
        }

        var refreshCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _refreshCts, refreshCts);
        previous?.Cancel();

        _refreshButton.Enabled = false;
        _advancedButton.Enabled = false;
        _page.SetStatus(
            "Refreshing provider discovery and automatic execution targets...",
            HiveStatusTone.Information);

        try
        {
            var providers = await _management
                .ListProvidersAsync(
                    _accessContext,
                    includeRetired: false,
                    refreshCts.Token)
                .ConfigureAwait(true);

            if (providers.IsFailure)
            {
                _page.SetStatus(
                    "Provider refresh failed. See technical details.",
                    HiveStatusTone.Error);
                HiveUiErrorReporter.Report(
                    FindForm(),
                    new InvalidOperationException(providers.Error!.Message),
                    "Provider Refresh",
                    "Provider refresh could not be completed.",
                    _output,
                    _themeManager);
                return;
            }

            var errors = new List<Error>();

            foreach (var provider in providers.Value!)
            {
                refreshCts.Token.ThrowIfCancellationRequested();

                var result = await _management
                    .RefreshProviderAsync(
                        provider.Id,
                        _accessContext,
                        refreshCts.Token)
                    .ConfigureAwait(true);

                if (result.IsFailure)
                {
                    errors.Add(result.Error!);
                    continue;
                }

                errors.AddRange(result.Value!.DiscoveryErrors);
            }

            await _page.RefreshAsync(refreshCts.Token).ConfigureAwait(true);

            if (errors.Count == 0)
            {
                _page.SetStatus(
                    "Provider discovery and automatic target reconciliation completed.",
                    HiveStatusTone.Success);
            }
            else
            {
                _page.SetStatus(
                    "Provider configuration was preserved, but one or more discovery operations need attention.",
                    HiveStatusTone.Warning);
                ReportErrors("Provider Refresh", errors);
            }
        }
        catch (OperationCanceledException)
            when (refreshCts.IsCancellationRequested ||
                  IsDisposed ||
                  Disposing)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                _page.SetStatus(
                    "Provider refresh failed. See technical details.",
                    HiveStatusTone.Error);
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Provider Refresh",
                    "Provider discovery and target reconciliation could not be completed.",
                    _output,
                    _themeManager);
            }
        }
        finally
        {
            if (ReferenceEquals(_refreshCts, refreshCts))
                Interlocked.CompareExchange(ref _refreshCts, null, refreshCts);

            refreshCts.Dispose();

            if (!IsDisposed && !Disposing)
            {
                _refreshButton.Enabled = true;
                _advancedButton.Enabled = true;
            }

            Interlocked.Exchange(ref _refreshRunning, 0);
        }
    }

    private void ReportDiscoveryOutcome(ProviderSettingsOperationResult result)
    {
        if (!result.HasDiscoveryErrors)
        {
            _page.SetStatus(
                $"Provider saved. {result.ActiveAutomaticTargetCount} automatic execution target(s) are available.",
                HiveStatusTone.Success);
            return;
        }

        _page.SetStatus(
            "Provider saved. Discovery failed or was unavailable; existing configuration was preserved.",
            HiveStatusTone.Warning);
        ReportErrors("Provider Discovery", result.DiscoveryErrors);
    }

    private void ReportErrors(string title, IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
            return;

        var message = string.Join(
            Environment.NewLine,
            errors
                .Select(error => $"{error.Code}: {error.Message}")
                .Distinct(StringComparer.Ordinal));

        HiveUiErrorReporter.Report(
            FindForm(),
            new InvalidOperationException(message),
            title,
            "The provider configuration was saved, but the operational discovery step reported one or more problems.",
            _output,
            _themeManager);
    }

    private async Task OpenAdvancedAsync()
    {
        using var form = new HiveAdvancedProviderConfigurationForm(
            _management,
            _accessContext,
            _themeManager,
            _output);

        form.ShowDialog(FindForm());

        if (IsDisposed || Disposing)
            return;

        try
        {
            await _page.RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Providers",
                    "The Providers page could not be refreshed after closing Advanced Provider Configuration.",
                    _output,
                    _themeManager);
            }
        }
    }

    private static SecretMaterial? CreateCredential(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : SecretMaterial.Create(value.Trim());

    private static bool IsAutomaticTarget(ExecutionTarget target)
    {
        try
        {
            return target.ManagementMode == ExecutionTargetManagementMode.Automatic;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static HiveButton CreateToolbarButton(
        string text,
        HiveButtonStyle style,
        string accessibleDescription)
    {
        return new HiveButton
        {
            Text = text,
            Style = style,
            Width = text == "Advanced" ? 104 : 92,
            Height = 32,
            Margin = new Padding(6, 4, 0, 4),
            AccessibleName = text,
            AccessibleDescription = accessibleDescription
        };
    }

    private void PageOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        _page.SetStatus(
            "Provider operation failed. See technical details.",
            HiveStatusTone.Error);

        HiveUiErrorReporter.Report(
            FindForm(),
            e.Exception,
            "Provider operation failed",
            "The provider operation could not be completed.",
            _output,
            _themeManager);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _page.OperationFailed -= PageOperationFailed;
            _tabs.SelectedIndexChanged -= TabsSelectedIndexChanged;

            var refreshCts = Interlocked.Exchange(ref _refreshCts, null);
            refreshCts?.Cancel();
            refreshCts?.Dispose();

        }

        base.Dispose(disposing);
    }

    private sealed record ConfiguredProviderRow(
        Provider Provider,
        string CredentialStatus,
        int ActiveAutomaticTargetCount,
        string Status);
}
