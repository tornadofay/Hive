using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveProviderSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveEditorLayout _editor;

    private readonly ComboBox _providerComboBox;
    private readonly ComboBox _accountComboBox;
    private readonly ComboBox _targetComboBox;

    private readonly TextBox _providerKeyTextBox;
    private readonly TextBox _providerNameTextBox;
    private readonly TextBox _transportTextBox;

    private readonly TextBox _accountKeyTextBox;
    private readonly TextBox _accountNameTextBox;
    private readonly TextBox _externalAccountTextBox;
    private readonly TextBox _credentialTextBox;
    private readonly Label _credentialStatus;

    private readonly TextBox _targetKeyTextBox;
    private readonly TextBox _targetNameTextBox;
    private readonly TextBox _endpointTextBox;
    private readonly TextBox _modelTextBox;
    private readonly TextBox _deploymentTextBox;

    private readonly HiveButton _refreshButton;
    private readonly HiveButton _newProviderButton;
    private readonly HiveButton _saveProviderButton;
    private readonly HiveButton _newAccountButton;
    private readonly HiveButton _saveAccountButton;
    private readonly HiveButton _newTargetButton;
    private readonly HiveButton _saveTargetButton;
    private readonly HiveButton _testButton;
    private readonly Label _statusLabel;

    private readonly List<Provider> _providers = new();
    private readonly List<ProviderAccount> _accounts = new();
    private readonly List<ExecutionTarget> _targets = new();

    private Provider? _selectedProvider;
    private ProviderAccount? _selectedAccount;
    private ExecutionTarget? _selectedTarget;
    private CancellationTokenSource? _operationCts;
    private bool _suppressSelectionChanged;

    public HiveProviderSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        _editor = new HiveEditorLayout();

        _providerComboBox = CreateComboBox();
        _accountComboBox = CreateComboBox();
        _targetComboBox = CreateComboBox();

        _providerKeyTextBox = CreateTextBox();
        _providerNameTextBox = CreateTextBox();
        _transportTextBox = CreateTextBox();

        _accountKeyTextBox = CreateTextBox();
        _accountNameTextBox = CreateTextBox();
        _externalAccountTextBox = CreateTextBox();
        _credentialTextBox = CreateTextBox();
        _credentialTextBox.UseSystemPasswordChar = true;
        _credentialStatus = CreateStatusLabel();

        _targetKeyTextBox = CreateTextBox();
        _targetNameTextBox = CreateTextBox();
        _endpointTextBox = CreateTextBox();
        _modelTextBox = CreateTextBox();
        _deploymentTextBox = CreateTextBox();

        _statusLabel = CreateStatusLabel();
        _statusLabel.AutoEllipsis = true;

        _refreshButton = _editor.AddActionButton(
            "Refresh",
            HiveButtonStyle.Secondary,
            92);
        _newProviderButton = _editor.AddActionButton(
            "New provider",
            HiveButtonStyle.Secondary,
            112);
        _saveProviderButton = _editor.AddActionButton(
            "Save provider",
            HiveButtonStyle.Primary,
            112);
        _newAccountButton = _editor.AddActionButton(
            "New account",
            HiveButtonStyle.Secondary,
            104);
        _saveAccountButton = _editor.AddActionButton(
            "Save account",
            HiveButtonStyle.Primary,
            104);
        _newTargetButton = _editor.AddActionButton(
            "New target",
            HiveButtonStyle.Secondary,
            96);
        _saveTargetButton = _editor.AddActionButton(
            "Save target",
            HiveButtonStyle.Primary,
            104);
        _testButton = _editor.AddActionButton(
            "Test connection",
            HiveButtonStyle.Secondary,
            124);

        _refreshButton.Click += async (_, _) =>
            await RunOperationAsync(RefreshAsync);
        _newProviderButton.Click += (_, _) => NewProvider();
        _saveProviderButton.Click += async (_, _) =>
            await RunOperationAsync(SaveProviderAsync);
        _newAccountButton.Click += (_, _) => NewAccount();
        _saveAccountButton.Click += async (_, _) =>
            await RunOperationAsync(SaveAccountAsync);
        _newTargetButton.Click += (_, _) => NewTarget();
        _saveTargetButton.Click += async (_, _) =>
            await RunOperationAsync(SaveTargetAsync);
        _testButton.Click += async (_, _) =>
            await RunOperationAsync(TestConnectionAsync);

        _providerComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressSelectionChanged)
                return;

            await RunOperationAsync(ProviderSelectionChangedAsync);
        };
        _accountComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressSelectionChanged)
                return;

            await RunOperationAsync(AccountSelectionChangedAsync);
        };
        _targetComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressSelectionChanged)
                return;

            SelectTarget();
        };

        _editor.AddField(
            "Provider",
            "Stored Provider resource and transport kind.",
            _providerComboBox);

        _editor.AddField(
            "Provider key",
            "Stable provider identity key. Changing it after creation is not supported.",
            _providerKeyTextBox);

        _editor.AddField(
            "Provider name",
            "Human-readable provider name.",
            _providerNameTextBox);

        _editor.AddField(
            "Transport",
            "Transport boundary, for example openai-compatible.",
            _transportTextBox);

        _editor.AddField(
            "Account",
            "Stored ProviderAccount under the selected Provider.",
            _accountComboBox);

        _editor.AddField(
            "Account key",
            "Stable account identity key. Changing it after creation is not supported.",
            _accountKeyTextBox);

        _editor.AddField(
            "Account name",
            "Human-readable provider account name.",
            _accountNameTextBox);

        _editor.AddField(
            "External account",
            "Optional vendor/project/account identifier.",
            _externalAccountTextBox);

        var credentialPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        _credentialTextBox.Dock = DockStyle.Top;
        _credentialTextBox.Height = 32;
        credentialPanel.Controls.Add(_credentialTextBox);
        _credentialStatus.Dock = DockStyle.Bottom;
        _credentialStatus.Height = 22;
        credentialPanel.Controls.Add(_credentialStatus);

        _editor.AddField(
            "API credential",
            "Never persisted in ProviderAccount. It is stored through Hive Secret Store and only its secret identity is retained.",
            credentialPanel,
            86);

        _editor.AddField(
            "Execution target",
            "Stored endpoint/model target under the selected ProviderAccount.",
            _targetComboBox);

        _editor.AddField(
            "Target key",
            "Stable execution-target identity key.",
            _targetKeyTextBox);

        _editor.AddField(
            "Target name",
            "Human-readable execution target name.",
            _targetNameTextBox);

        _editor.AddField(
            "Endpoint",
            "Absolute HTTP/HTTPS endpoint. Credentials must never be embedded in the URI.",
            _endpointTextBox);

        _editor.AddField(
            "Model",
            "Model identifier for model-backed targets.",
            _modelTextBox);

        _editor.AddField(
            "Deployment",
            "Optional deployment identifier when the provider uses deployments.",
            _deploymentTextBox);

        _editor.AddField(
            "Status",
            "Provider connection tests use the selected ExecutionTarget and its referenced Secret Store credential.",
            _statusLabel,
            72);

        Controls.Add(_editor);

        _themeManager.Apply(this);
        ResetSelectors();
        _credentialStatus.Text = "Saved credential: not configured.";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        await RefreshAsync(cancellationToken).ConfigureAwait(true);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var providers = await _management
            .ListProvidersAsync(_accessContext, cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (providers.IsFailure)
        {
            SetStatus(providers.Error!.Message, isError: true);
            return;
        }

        _providers.Clear();
        _providers.AddRange(providers.Value!);

        RebuildCombo(
            _providerComboBox,
            _providers,
            provider => new Choice<Provider>(
                provider,
                $"{provider.DisplayName}  [{provider.Key}]"));

        if (_providers.Count == 0)
        {
            NewProvider();
            SetStatus("No providers are configured.", isError: false);
            return;
        }

        SelectProvider(
            _selectedProvider is null
                ? _providers[0]
                : _providers.FirstOrDefault(
                    provider => provider.Id == _selectedProvider.Id)
                    ?? _providers[0]);

        await ProviderSelectionChangedAsync(
            cancellationToken).ConfigureAwait(true);

        SetStatus(
            $"{_providers.Count} provider(s) loaded.",
            isError: false);
    }

    private async Task ProviderSelectionChangedAsync(CancellationToken cancellationToken)
    {
        if (_providerComboBox.SelectedItem is not Choice<Provider> choice)
        {
            NewProvider();
            return;
        }

        SelectProvider(choice.Value);

        var accounts = await _management
            .ListProviderAccountsAsync(
                choice.Value.Id,
                _accessContext,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (accounts.IsFailure)
        {
            SetStatus(accounts.Error!.Message, isError: true);
            return;
        }

        _accounts.Clear();
        _accounts.AddRange(accounts.Value!);

        RebuildCombo(
            _accountComboBox,
            _accounts,
            account => new Choice<ProviderAccount>(
                account,
                $"{account.DisplayName}  [{account.Key}]"));

        if (_accounts.Count == 0)
        {
            NewAccount();
            return;
        }

        SelectAccount(
            _selectedAccount is null
                ? _accounts[0]
                : _accounts.FirstOrDefault(
                    account => account.Id == _selectedAccount.Id)
                    ?? _accounts[0]);

        SetStatus(
            $"{_accounts.Count} account(s) loaded for the selected provider.",
            isError: false);
    }

    private async Task AccountSelectionChangedAsync(CancellationToken cancellationToken)
    {
        if (_accountComboBox.SelectedItem is not Choice<ProviderAccount> choice)
        {
            NewAccount();
            return;
        }

        SelectAccount(choice.Value);

        var targets = await _management
            .ListExecutionTargetsAsync(
                choice.Value.Id,
                _accessContext,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (targets.IsFailure)
        {
            SetStatus(targets.Error!.Message, isError: true);
            return;
        }

        _targets.Clear();
        _targets.AddRange(targets.Value!);

        RebuildCombo(
            _targetComboBox,
            _targets,
            target => new Choice<ExecutionTarget>(
                target,
                $"{target.DisplayName}  [{target.Key}]"));

        if (_targets.Count == 0)
        {
            NewTarget();
            return;
        }

        SelectTarget(
            _selectedTarget is null
                ? _targets[0]
                : _targets.FirstOrDefault(
                    target => target.Id == _selectedTarget.Id)
                    ?? _targets[0]);

        SetStatus(
            $"{_targets.Count} execution target(s) loaded for the selected account.",
            isError: false);
    }

    private async Task SaveProviderAsync(CancellationToken cancellationToken)
    {
        Provider provider;

        try
        {
            if (_selectedProvider is null)
            {
                provider = CreateProvider();
                var created = await _management
                    .CreateProviderAsync(
                        provider,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (created.IsFailure)
                {
                    SetStatus(created.Error!.Message, isError: true);
                    return;
                }

                _selectedProvider = created.Value!;
            }
            else
            {
                provider = _selectedProvider
                    .WithDisplayName(_providerNameTextBox.Text)
                    .WithTransportKind(_transportTextBox.Text);

                var updated = await _management
                    .UpdateProviderAsync(
                        provider,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (updated.IsFailure)
                {
                    SetStatus(updated.Error!.Message, isError: true);
                    return;
                }

                _selectedProvider = updated.Value!;
            }
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        SetStatus("Provider saved.", isError: false);
    }

    private async Task SaveAccountAsync(CancellationToken cancellationToken)
    {
        if (_selectedProvider is null)
        {
            SetStatus("Select or create a provider first.", isError: true);
            return;
        }

        try
        {
            ProviderAccount account;

            if (_selectedAccount is null)
            {
                account = CreateAccount(_selectedProvider);
                var created = await _management
                    .CreateProviderAccountAsync(
                        account,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (created.IsFailure)
                {
                    SetStatus(created.Error!.Message, isError: true);
                    return;
                }

                account = created.Value!;
            }
            else
            {
                account = _selectedAccount
                    .WithDisplayName(_accountNameTextBox.Text)
                    .WithExternalAccountId(
                        string.IsNullOrWhiteSpace(_externalAccountTextBox.Text)
                            ? null
                            : _externalAccountTextBox.Text);

                if (!string.IsNullOrWhiteSpace(_credentialTextBox.Text))
                {
                    var credential = await SaveCredentialAsync(
                        _credentialTextBox.Text,
                        account.CredentialSecret,
                        cancellationToken).ConfigureAwait(true);
                    account = account.WithCredentialSecret(credential);
                }

                var updated = await _management
                    .UpdateProviderAccountAsync(
                        account,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (updated.IsFailure)
                {
                    SetStatus(updated.Error!.Message, isError: true);
                    return;
                }

                account = updated.Value!;
            }

            if (_selectedAccount is null &&
                !string.IsNullOrWhiteSpace(_credentialTextBox.Text))
            {
                var credential = await SaveCredentialAsync(
                    _credentialTextBox.Text,
                    account.CredentialSecret,
                    cancellationToken).ConfigureAwait(true);

                account = account.WithCredentialSecret(credential);

                var updated = await _management
                    .UpdateProviderAccountAsync(
                        account,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (updated.IsFailure)
                {
                    SetStatus(updated.Error!.Message, isError: true);
                    return;
                }

                account = updated.Value!;
            }

            _credentialTextBox.Clear();
            _selectedAccount = account;
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        await ProviderSelectionChangedAsync(cancellationToken).ConfigureAwait(true);
        SetStatus("Provider account saved.", isError: false);
    }

    private async Task SaveTargetAsync(CancellationToken cancellationToken)
    {
        if (_selectedAccount is null)
        {
            SetStatus("Select or create a provider account first.", isError: true);
            return;
        }

        try
        {
            ExecutionTarget target;

            if (_selectedTarget is null)
            {
                target = CreateTarget(_selectedProvider!, _selectedAccount);
                var created = await _management
                    .CreateExecutionTargetAsync(
                        target,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (created.IsFailure)
                {
                    SetStatus(created.Error!.Message, isError: true);
                    return;
                }

                _selectedTarget = created.Value!;
            }
            else
            {
                target = _selectedTarget
                    .WithDisplayName(_targetNameTextBox.Text)
                    .WithEndpoint(new Uri(
                        _endpointTextBox.Text.Trim(),
                        UriKind.Absolute))
                    .WithModel(
                        string.IsNullOrWhiteSpace(_modelTextBox.Text)
                            ? null
                            : _modelTextBox.Text)
                    .WithDeployment(
                        string.IsNullOrWhiteSpace(_deploymentTextBox.Text)
                            ? null
                            : _deploymentTextBox.Text);

                var updated = await _management
                    .UpdateExecutionTargetAsync(
                        target,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (updated.IsFailure)
                {
                    SetStatus(updated.Error!.Message, isError: true);
                    return;
                }

                _selectedTarget = updated.Value!;
            }
        }
        catch (UriFormatException)
        {
            SetStatus(
                "Execution target endpoint must be an absolute HTTP or HTTPS URI.",
                isError: true);
            return;
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        await AccountSelectionChangedAsync(cancellationToken).ConfigureAwait(true);
        SetStatus("Execution target saved.", isError: false);
    }

    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        if (_selectedTarget is null)
        {
            SetStatus("Select or create an execution target first.", isError: true);
            return;
        }

        var result = await _management
            .TestExecutionTargetConnectionAsync(
                _selectedTarget.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            SetStatus(
                $"Connection test failed: {result.Error!.Message}",
                isError: true);
            return;
        }

        var value = result.Value!;
        SetStatus(
            $"{value.Message} Provider={value.ProviderKey}; Target={value.ExecutionTargetKey}; Model={value.Model}; Duration={value.Duration.TotalMilliseconds:0} ms.",
            isError: false);
    }

    private async Task<SecretReference> SaveCredentialAsync(
        string materialValue,
        SecretReference? existing,
        CancellationToken cancellationToken)
    {
        using var material = SecretMaterial.Create(materialValue);

        if (existing is null)
        {
            var created = await _management
                .CreateSecretAsync(
                    $"hive-provider-credential-{Guid.NewGuid():N}",
                    "Hive provider credential",
                    material,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (created.IsFailure)
                throw new InvalidOperationException(created.Error!.Message);

            return new SecretReference(created.Value!.Id);
        }

        var descriptor = await _management
            .GetSecretDescriptorAsync(
                existing.Value.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (descriptor.IsFailure)
            throw new InvalidOperationException(descriptor.Error!.Message);

        var replaced = await _management
            .ReplaceSecretAsync(
                existing.Value.Id,
                material,
                descriptor.Value!.Resource.Version,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (replaced.IsFailure)
            throw new InvalidOperationException(replaced.Error!.Message);

        return existing.Value;
    }

    private Provider CreateProvider()
    {
        var now = DateTimeOffset.UtcNow;

        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                _accessContext.PrincipalId!.Value,
                ResourceScope.Tenant(_accessContext.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    _accessContext.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            _providerKeyTextBox.Text,
            _providerNameTextBox.Text,
            _transportTextBox.Text);
    }

    private ProviderAccount CreateAccount(Provider provider)
    {
        var now = DateTimeOffset.UtcNow;

        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                _accessContext.PrincipalId!.Value,
                ResourceScope.Tenant(_accessContext.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    _accessContext.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            _accountKeyTextBox.Text,
            _accountNameTextBox.Text,
            string.IsNullOrWhiteSpace(_externalAccountTextBox.Text)
                ? null
                : _externalAccountTextBox.Text);
    }

    private ExecutionTarget CreateTarget(
        Provider provider,
        ProviderAccount account)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                account.Resource.Owner,
                account.Resource.Scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    account.Resource.Provenance.CreatedBy,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            account.Id,
            _targetKeyTextBox.Text,
            _targetNameTextBox.Text,
            new Uri(
                _endpointTextBox.Text.Trim(),
                UriKind.Absolute),
            string.IsNullOrWhiteSpace(_modelTextBox.Text)
                ? null
                : _modelTextBox.Text,
            string.IsNullOrWhiteSpace(_deploymentTextBox.Text)
                ? null
                : _deploymentTextBox.Text,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Unknown)
            ]);
    }

    private void NewProvider()
    {
        _selectedProvider = null;
        _selectedAccount = null;
        _selectedTarget = null;

        _providerKeyTextBox.ReadOnly = false;
        _providerKeyTextBox.Text = "openai-compatible";
        _providerNameTextBox.Text = "OpenAI-compatible provider";
        _transportTextBox.Text = "openai-compatible";

        _accounts.Clear();
        _targets.Clear();
        _accountComboBox.Items.Clear();
        _targetComboBox.Items.Clear();
        NewAccount();
    }

    private void NewAccount()
    {
        _selectedAccount = null;
        _selectedTarget = null;

        _accountKeyTextBox.ReadOnly = false;
        _accountKeyTextBox.Text = "default";
        _accountNameTextBox.Text = "Default account";
        _externalAccountTextBox.Clear();
        _credentialTextBox.Clear();
        _credentialStatus.Text = "Unsaved credential: enter a value to store it securely.";

        _targets.Clear();
        _targetComboBox.Items.Clear();
        NewTarget();
    }

    private void NewTarget()
    {
        _selectedTarget = null;
        _targetKeyTextBox.ReadOnly = false;
        _targetKeyTextBox.Text = "default";
        _targetNameTextBox.Text = "Default target";
        _endpointTextBox.Text = "https://example.invalid/v1";
        _modelTextBox.Text = "model";
        _deploymentTextBox.Clear();
    }

    private void SelectProvider(Provider provider)
    {
        _selectedProvider = provider;
        _providerKeyTextBox.Text = provider.Key;
        _providerKeyTextBox.ReadOnly = true;
        _providerNameTextBox.Text = provider.DisplayName;
        _transportTextBox.Text = provider.TransportKind;

        if (_providerComboBox.SelectedItem is not Choice<Provider> choice ||
            choice.Value.Id != provider.Id)
        {
            SelectComboValue(_providerComboBox, provider.Id);
        }
    }

    private void SelectAccount(ProviderAccount account)
    {
        _selectedAccount = account;
        _accountKeyTextBox.Text = account.Key;
        _accountKeyTextBox.ReadOnly = true;
        _accountNameTextBox.Text = account.DisplayName;
        _externalAccountTextBox.Text = account.ExternalAccountId ?? string.Empty;
        _credentialTextBox.Clear();
        _credentialStatus.Text =
            account.CredentialSecret is null
                ? "Saved credential: not configured."
                : "Saved credential: configured (material hidden).";

        SelectComboValue(_accountComboBox, account.Id);
    }

    private void SelectTarget(ExecutionTarget? target = null)
    {
        target ??= _targetComboBox.SelectedItem is Choice<ExecutionTarget> choice
            ? choice.Value
            : null;

        _selectedTarget = target;

        if (target is null)
        {
            _targetKeyTextBox.Clear();
            _targetNameTextBox.Clear();
            _endpointTextBox.Clear();
            _modelTextBox.Clear();
            _deploymentTextBox.Clear();
            return;
        }

        _targetKeyTextBox.Text = target.Key;
        _targetKeyTextBox.ReadOnly = true;
        _targetNameTextBox.Text = target.DisplayName;
        _endpointTextBox.Text = target.Endpoint.AbsoluteUri;
        _modelTextBox.Text = target.Model ?? string.Empty;
        _deploymentTextBox.Text = target.Deployment ?? string.Empty;

        SelectComboValue(_targetComboBox, target.Id);
    }

    private void ResetSelectors()
    {
        _providerComboBox.Items.Clear();
        _accountComboBox.Items.Clear();
        _targetComboBox.Items.Clear();
        NewProvider();
    }

    private static ComboBox CreateComboBox() =>
        new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };

    private static TextBox CreateTextBox() =>
        new()
        {
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle
        };

    private static Label CreateStatusLabel() =>
        new()
        {
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private void RebuildCombo<T>(
        ComboBox comboBox,
        IReadOnlyList<T> values,
        Func<T, object> createItem)
    {
        _suppressSelectionChanged = true;
        try
        {
            comboBox.BeginUpdate();
            try
            {
                comboBox.Items.Clear();
                foreach (var value in values)
                    comboBox.Items.Add(createItem(value));
            }
            finally
            {
                comboBox.EndUpdate();
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void SelectComboValue(
        ComboBox comboBox,
        Guid id)
    {
        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            var item = comboBox.Items[index];

            var itemId = item switch
            {
                Choice<Provider> provider => provider.Value.Id.Value,
                Choice<ProviderAccount> account => account.Value.Id.Value,
                Choice<ExecutionTarget> target => target.Value.Id.Value,
                _ => Guid.Empty
            };

            if (itemId == id)
            {
                _suppressSelectionChanged = true;
                try
                {
                    comboBox.SelectedIndex = index;
                }
                finally
                {
                    _suppressSelectionChanged = false;
                }

                return;
            }
        }
    }

    private void SetStatus(string text, bool isError)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError
            ? _themeManager.Theme.VisualStates.Error
            : _themeManager.Theme.Palette.MutedText;
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation)
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();

        SetBusy(true);

        try
        {
            await operation(_operationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_operationCts.IsCancellationRequested)
        {
            SetStatus("Operation cancelled.", isError: false);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
            _operationCts.Dispose();
            _operationCts = null;
        }
    }

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _newProviderButton.Enabled = !busy;
        _saveProviderButton.Enabled = !busy;
        _newAccountButton.Enabled = !busy;
        _saveAccountButton.Enabled = !busy;
        _newTargetButton.Enabled = !busy;
        _saveTargetButton.Enabled = !busy;
        _testButton.Enabled = !busy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationCts?.Cancel();
            _operationCts?.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record Choice<T>(
        T Value,
        string Display)
    {
        public override string ToString() => Display;
    }
}
