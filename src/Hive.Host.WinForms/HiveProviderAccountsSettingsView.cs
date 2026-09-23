using System.Drawing;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderAccountsSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly ComboBox _providerComboBox;
    private readonly HiveCrudPage<ProviderAccount> _page;

    private IReadOnlyList<Provider> _providers = Array.Empty<Provider>();
    private Provider? _selectedProvider;
    private bool _loadingProviders;

    public HiveProviderAccountsSettingsView(
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

        _providerComboBox = new ComboBox
        {
            Width = 360,
            Height = 32,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = Padding.Empty
        };
        _providerComboBox.SelectedIndexChanged += ProviderComboBoxOnSelectedIndexChanged;

        _page = new HiveCrudPage<ProviderAccount>
        {
            Title = "Accounts / Credentials",
            Description =
                "Manage durable ProviderAccount resources under the selected Provider. Credentials are stored as Secret Store references; this is not a provider login screen.",
            PageSize = 25,
            AllowAdd = false,
            AllowEdit = true,
            AllowDelete = true,
            ShowRefresh = true,
            ShowSearch = true,
            SearchPlaceholder = "Search accounts..."
        };

        _page.SetColumns(
            new HiveCrudColumn<ProviderAccount>(
                "Lifecycle",
                120,
                item => HiveLifecyclePresentation.Format(
                    item.Resource.Lifecycle.Status),
                item => HiveLifecyclePresentation.Color(
                    item.Resource.Lifecycle.Status,
                    _themeManager)),

            new HiveCrudColumn<ProviderAccount>("Resource key", 180, item => item.Key),
            new HiveCrudColumn<ProviderAccount>("Name", 220, item => item.DisplayName),
            new HiveCrudColumn<ProviderAccount>(
                "External account",
                220,
                item => item.ExternalAccountId ?? "—"),
            new HiveCrudColumn<ProviderAccount>(
                "Credential",
                150,
                item => item.CredentialSecret is null ? "Not configured" : "Configured")
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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var filter = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 8, 0, 8),
            Margin = Padding.Empty
        };
        filter.Controls.Add(new Label
        {
            Text = "Provider",
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 0)
        });
        filter.Controls.Add(_providerComboBox);

        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(_page, 0, 1);
        Controls.Add(root);

        _themeManager.Apply(this);
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _management
            .ListProvidersAsync(
                _accessContext,
                includeRetired: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        _providers = result.Value!;

        _loadingProviders = true;
        try
        {
            _providerComboBox.BeginUpdate();
            try
            {
                _providerComboBox.Items.Clear();

                foreach (var provider in _providers)
                    _providerComboBox.Items.Add(new ProviderChoice(provider));

                _providerComboBox.SelectedIndex = -1;
                _providerComboBox.Text = "Select a Provider...";
                _selectedProvider = null;
            }
            finally
            {
                _providerComboBox.EndUpdate();
            }
        }
        finally
        {
            _loadingProviders = false;
        }

        _page.AllowAdd = false;
        _page.SetStatus("Select a Provider to manage its accounts and credentials.");

        await _page.RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private async void ProviderComboBoxOnSelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_loadingProviders)
            return;

        try
        {
            _selectedProvider =
                (_providerComboBox.SelectedItem as ProviderChoice)?.Value;

            _page.AllowAdd = _selectedProvider is not null;

            if (_selectedProvider is null)
                _providerComboBox.Text = "Select a Provider...";

            if (!IsDisposed)
                await _page.RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Provider Accounts",
                "Provider accounts could not be refreshed.",
                _output,
                _themeManager);
        }
    }

    private async Task<IReadOnlyList<ProviderAccount>> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (_selectedProvider is null)
            return Array.Empty<ProviderAccount>();

        var result = await _management
            .ListProviderAccountsAsync(
                _selectedProvider.Id,
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        return result.Value!;
    }

    private async Task<ProviderAccount?> EditAsync(
        ProviderAccount? account,
        CancellationToken cancellationToken)
    {
        if (_selectedProvider is null)
        {
            HiveMessageBox.ShowInformation(
                FindForm(),
                "Select a Provider first.",
                "Provider Accounts");

            return null;
        }

        using var editor = new HiveProviderAccountEditorForm(
            account,
            _selectedProvider,
            _accessContext,
            _themeManager,
            _output);

        if (editor.ShowDialog(FindForm()) != DialogResult.OK ||
            editor.Definition is null)
        {
            return null;
        }

        var result = account is null
            ? await _management.CreateProviderAccountAsync(
                editor.Definition,
                _accessContext,
                cancellationToken)
            : await _management.UpdateProviderAccountAsync(
                editor.Definition,
                _accessContext,
                cancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        var saved = result.Value!;
        var credentialText = editor.CredentialText;

        if (!string.IsNullOrWhiteSpace(credentialText))
        {
            var withCredential = await SaveCredentialAsync(
                saved,
                credentialText,
                cancellationToken);

            editor.ClearCredential();

            if (withCredential.CredentialSecret != saved.CredentialSecret)
            {
                var credentialResult = await _management
                    .UpdateProviderAccountAsync(
                        withCredential,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (credentialResult.IsFailure)
                    throw new InvalidOperationException(
                        credentialResult.Error!.Message);

                saved = credentialResult.Value!;
            }
        }

        return saved;
    }

    private async Task<ProviderAccount> SaveCredentialAsync(
        ProviderAccount account,
        string credential,
        CancellationToken cancellationToken)
    {
        if (account.CredentialSecret is { } existingReference)
        {
            var descriptor = await _management
                .GetSecretDescriptorAsync(
                    existingReference.Id,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (descriptor.IsFailure)
                throw new InvalidOperationException(descriptor.Error!.Message);

            using var material = SecretMaterial.Create(credential);

            var replacement = await _management
                .ReplaceSecretAsync(
                    existingReference.Id,
                    material,
                    descriptor.Value!.Resource.Version,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (replacement.IsFailure)
                throw new InvalidOperationException(replacement.Error!.Message);

            return account.WithCredentialSecret(
                new SecretReference(replacement.Value!.Id));
        }

        using (var material = SecretMaterial.Create(credential))
        {
            var secret = await _management
                .CreateSecretAsync(
                    $"hive-provider-account-{account.Id}",
                    $"API credential — {account.DisplayName}",
                    material,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (secret.IsFailure)
                throw new InvalidOperationException(secret.Error!.Message);

            return account.WithCredentialSecret(
                new SecretReference(secret.Value!.Id));
        }
    }

    private async Task DeleteAsync(
        ProviderAccount account,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .DeleteProviderAccountAsync(
                account.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private async Task ActivateAsync(
        ProviderAccount account,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .ReactivateProviderAccountAsync(
                account.Id,
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
            "Provider Account operation failed",
            "The provider account operation could not be completed.",
            _output,
            _themeManager);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _page.OperationFailed -= PageOperationFailed;
            _providerComboBox.SelectedIndexChanged -= ProviderComboBoxOnSelectedIndexChanged;
        }

        base.Dispose(disposing);
    }

    private sealed record ProviderChoice(Provider Value)
    {
        public override string ToString() => Value.DisplayName;
    }
}
