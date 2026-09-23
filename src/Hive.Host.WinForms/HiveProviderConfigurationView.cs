using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderConfigurationView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveCrudPage<Provider> _page;

    public HiveProviderConfigurationView(
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

        _page = new HiveCrudPage<Provider>
        {
            Title = "Provider Configuration",
            Description =
                "Define Provider identity and transport only. API keys belong to Accounts / Credentials; API endpoints and models belong to Execution Targets.",
            PageSize = 25,
            AllowAdd = true,
            AllowEdit = true,
            AllowDelete = true,
            ShowRefresh = true,
            ShowSearch = true,
            SearchPlaceholder = "Search providers..."
        };

        _page.SetColumns(
            new HiveCrudColumn<Provider>("Resource key", 180, item => item.Key),
            new HiveCrudColumn<Provider>("Name", 240, item => item.DisplayName),
            new HiveCrudColumn<Provider>("Transport", 190, item => item.TransportKind),
            new HiveCrudColumn<Provider>(
                "Lifecycle",
                120,
                item => HiveLifecyclePresentation.Format(
                    item.Resource.Lifecycle.Status),
                item => HiveLifecyclePresentation.Color(
                    item.Resource.Lifecycle.Status,
                    _themeManager))

            );

        _page.LoadItemsAsync = LoadAsync;
        _page.EditItemAsync = EditAsync;
        _page.DeleteItemAsync = DeleteAsync;
        _page.ActivateItemAsync = ActivateAsync;
        _page.StatusSelector = item => item.Resource.Lifecycle.Status.ToString();
        _page.CanEditItem = item => HiveLifecyclePresentation.IsActive(item.Resource);
        _page.CanDeleteItem = item => HiveLifecyclePresentation.IsActive(item.Resource);
        _page.CanActivateItem = item => HiveLifecyclePresentation.IsRetired(item.Resource);
        _page.GetItemDisplayName = item => $"{item.DisplayName} [{item.Key}]";

        _page.OperationFailed += PageOperationFailed;

        Controls.Add(_page);
        _themeManager.Apply(this);
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        _page.RefreshAsync(cancellationToken);

    private async Task<IReadOnlyList<Provider>> LoadAsync(
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

        return result.Value!;
    }

    private async Task<Provider?> EditAsync(
        Provider? provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var editor = new HiveProviderEditorForm(
            provider,
            _accessContext,
            _themeManager,
            _output);

        if (editor.ShowDialog(FindForm()) != DialogResult.OK ||
            editor.Definition is null)
        {
            return null;
        }

        var result = provider is null
            ? await _management.CreateProviderAsync(
                editor.Definition,
                _accessContext,
                cancellationToken)
            : await _management.UpdateProviderAsync(
                editor.Definition,
                _accessContext,
                cancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        return result.Value;
    }

    private async Task DeleteAsync(
        Provider provider,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .DeleteProviderAsync(
                provider.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private async Task ActivateAsync(
        Provider provider,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .ReactivateProviderAsync(
                provider.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private void PageOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        _page.SetStatus(e.Exception.Message);

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
            _page.OperationFailed -= PageOperationFailed;

        base.Dispose(disposing);
    }
}
