using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HivePersistenceSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveEditorLayout _editor;
    private readonly ComboBox _serverComboBox;
    private readonly TextBox _portTextBox;
    private readonly TextBox _databaseTextBox;
    private readonly ComboBox _authenticationComboBox;
    private readonly TextBox _userNameTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly Label _credentialStatus;
    private readonly CheckBox _encryptCheckBox;
    private readonly CheckBox _trustServerCertificateCheckBox;
    private readonly CheckBox _createDatabaseCheckBox;
    private readonly NumericUpDown _timeoutNumeric;
    private readonly Label _statusLabel;
    private readonly HiveButton _loadButton;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _testButton;

    private CancellationTokenSource? _operationCts;
    private HivePersistenceConfiguration? _loadedConfiguration;

    public HivePersistenceSettingsView(
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

        _serverComboBox = new ComboBox
        {
            Height = 32,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        _portTextBox = CreateTextBox();
        _databaseTextBox = CreateTextBox();
        _authenticationComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _authenticationComboBox.Items.AddRange(
        [
            HiveSqlAuthenticationMode.WindowsIntegrated,
            HiveSqlAuthenticationMode.SqlPassword
        ]);
        _authenticationComboBox.SelectedIndexChanged += (_, _) =>
            UpdateAuthenticationState();

        _userNameTextBox = CreateTextBox();
        _passwordTextBox = CreateTextBox();
        _passwordTextBox.UseSystemPasswordChar = true;

        _credentialStatus = CreateStatusLabel();

        _encryptCheckBox = new CheckBox
        {
            Text = "Encrypt SQL connection",
            AutoSize = true
        };

        _trustServerCertificateCheckBox = new CheckBox
        {
            Text = "Trust server certificate",
            AutoSize = true
        };

        _createDatabaseCheckBox = new CheckBox
        {
            Text = "Allow database creation when initializing Hive",
            AutoSize = true
        };

        _timeoutNumeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 600,
            Value = 30,
            DecimalPlaces = 0
        };

        _statusLabel = CreateStatusLabel();
        _statusLabel.AutoEllipsis = true;

        _loadButton = _editor.AddActionButton(
            "Load",
            HiveButtonStyle.Secondary,
            96);
        _saveButton = _editor.AddActionButton(
            "Save",
            HiveButtonStyle.Primary,
            96);
        _testButton = _editor.AddActionButton(
            "Test connection",
            HiveButtonStyle.Secondary,
            132);

        _loadButton.Click += async (_, _) => await RunOperationAsync(LoadAsync);
        _saveButton.Click += async (_, _) => await RunOperationAsync(SaveAsync);
        _testButton.Click += async (_, _) => await RunOperationAsync(TestAsync);

        _editor.AddField(
            "Server / instance",
            "SQL Server host or instance name. Keep the port in the separate Port field.",
            _serverComboBox);

        _editor.AddField(
            "Port",
            "Optional TCP port. Leave empty for the server default.",
            _portTextBox);

        _editor.AddField(
            "Database",
            "Hive-owned SQL Server database name.",
            _databaseTextBox);

        _editor.AddField(
            "Authentication",
            "Choose Windows integrated authentication or SQL login credentials.",
            _authenticationComboBox);

        _editor.AddField(
            "SQL user",
            "Only used with SQL password authentication.",
            _userNameTextBox);

        var passwordPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty
        };
        _passwordTextBox.Dock = DockStyle.Top;
        _passwordTextBox.Height = 32;
        passwordPanel.Controls.Add(_passwordTextBox);
        _credentialStatus.Dock = DockStyle.Bottom;
        _credentialStatus.Height = 22;
        passwordPanel.Controls.Add(_credentialStatus);

        _editor.AddField(
            "SQL password",
            "Never stored in the settings file. It is protected by the Windows user-scoped DPAPI bootstrap store; the settings file keeps only its bootstrap reference.",
            passwordPanel,
            86);

        var securityPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        securityPanel.Controls.Add(_encryptCheckBox);
        securityPanel.Controls.Add(_trustServerCertificateCheckBox);
        _editor.AddField(
            "Connection security",
            "Transport/security flags for the SQL Server connection.",
            securityPanel,
            88);

        _editor.AddField(
            "Initialization",
            "This option is consumed only when Hive database initialization/migration is explicitly requested; connection testing never creates a database.",
            _createDatabaseCheckBox,
            72);

        _editor.AddField(
            "Command timeout",
            "Default SQL command timeout in seconds.",
            _timeoutNumeric);

        _editor.AddField(
            "Status",
            "Connection tests report server connectivity separately from database/schema state.",
            _statusLabel,
            72);

        Controls.Add(_editor);

        _themeManager.Apply(this);
        _authenticationComboBox.SelectedItem =
            HiveSqlAuthenticationMode.WindowsIntegrated;
        UpdateAuthenticationState();
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(cancellationToken);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var result = await _management
            .GetPersistenceConfigurationAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            SetStatus(result.Error!.Message, isError: true);
            return;
        }

        ApplyConfiguration(result.Value!);
        SetStatus("Persistence configuration loaded.", isError: false);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        HivePersistenceConfiguration? configuration;
        var previousBootstrapReference = _loadedConfiguration?.BootstrapCredential;

        try
        {
            configuration = await BuildConfigurationAsync(cancellationToken)
                .ConfigureAwait(true);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        var result = await _management
            .SavePersistenceConfigurationAsync(
                configuration,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            if (configuration.BootstrapCredential is { } createdReference &&
                createdReference != previousBootstrapReference)
            {
                await _management
                    .RemoveBootstrapCredentialAsync(
                        createdReference,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);
            }

            SetStatus(result.Error!.Message, isError: true);
            return;
        }

        _loadedConfiguration = result.Value!;
        if (previousBootstrapReference is { } previousReference &&
            previousReference != _loadedConfiguration.BootstrapCredential)
        {
            var cleanup = await _management
                .RemoveBootstrapCredentialAsync(
                    previousReference,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (cleanup.IsFailure)
            {
                _passwordTextBox.Clear();
                UpdateCredentialStatus(_loadedConfiguration);
                SetStatus(
                    "Persistence configuration saved, but the previous bootstrap credential could not be removed.",
                    isError: true);
                return;
            }
        }

        _passwordTextBox.Clear();
        UpdateCredentialStatus(_loadedConfiguration);
        SetStatus(
            "Persistence configuration saved. Database/schema state was not changed.",
            isError: false);
    }

    private async Task TestAsync(CancellationToken cancellationToken)
    {
        HivePersistenceConfiguration configuration;

        try
        {
            configuration = await BuildConfigurationAsync(
                    cancellationToken,
                    persistCredential: false)
                .ConfigureAwait(true);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        var result = await _management
            .TestPersistenceConnectionAsync(
                configuration,
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
            $"{value.Message} Schema={value.CurrentSchemaVersion?.ToString() ?? "none"}; supported={value.SupportedSchemaVersion}.",
            isError: false);
    }

    private async Task<HivePersistenceConfiguration> BuildConfigurationAsync(
        CancellationToken cancellationToken,
        bool persistCredential = true)
    {
        if (!int.TryParse(_portTextBox.Text.Trim(), out var port))
            port = 0;

        int? resolvedPort = port <= 0 ? null : port;

        if (!int.TryParse(_timeoutNumeric.Value.ToString(), out var timeout))
            timeout = 30;

        var authentication =
            _authenticationComboBox.SelectedItem is HiveSqlAuthenticationMode value
                ? value
                : HiveSqlAuthenticationMode.WindowsIntegrated;

        HiveBootstrapCredentialReference? credential =
            authentication == HiveSqlAuthenticationMode.SqlPassword
                ? _loadedConfiguration?.BootstrapCredential
                : null;

        var configuration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            _serverComboBox.Text,
            resolvedPort,
            _databaseTextBox.Text,
            authentication,
            authentication == HiveSqlAuthenticationMode.SqlPassword
                ? _userNameTextBox.Text
                : null,
            credential,
            _encryptCheckBox.Checked,
            _trustServerCertificateCheckBox.Checked,
            _createDatabaseCheckBox.Checked,
            timeout);

        if (authentication != HiveSqlAuthenticationMode.SqlPassword)
            return configuration;

        if (!string.IsNullOrWhiteSpace(_passwordTextBox.Text))
        {
            if (!persistCredential)
            {
                if (credential is null)
                {
                    throw new ArgumentException(
                        "A saved SQL password credential is required for a non-destructive connection test.");
                }
            }
            else
            {
                credential = await SaveCredentialAsync(
                    _passwordTextBox.Text,
                    existing: null,
                    cancellationToken).ConfigureAwait(true);

                configuration = new HivePersistenceConfiguration(
                    configuration.Backend,
                    configuration.ServerName,
                    configuration.Port,
                    configuration.DatabaseName,
                    configuration.AuthenticationMode,
                    configuration.UserName,
                    credential,
                    configuration.Encrypt,
                    configuration.TrustServerCertificate,
                    configuration.CreateDatabaseIfMissing,
                    configuration.CommandTimeoutSeconds);
            }
        }

        if (credential is null)
        {
            throw new ArgumentException(
                "SQL password authentication requires a saved credential. Enter a password and save the settings first.");
        }

        return configuration;
    }

    private async Task<HiveBootstrapCredentialReference> SaveCredentialAsync(
        string password,
        HiveBootstrapCredentialReference? existing,
        CancellationToken cancellationToken)
    {
        using var material = SecretMaterial.Create(password);

        var result = await _management
            .SaveBootstrapCredentialAsync(
                material,
                existing,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);

        return result.Value;
    }

    private void ApplyConfiguration(HivePersistenceConfiguration configuration)
    {
        _loadedConfiguration = configuration;
        _serverComboBox.Text = configuration.ServerName;
        RememberServer(configuration.ServerName);
        _portTextBox.Text = configuration.Port?.ToString() ?? string.Empty;
        _databaseTextBox.Text = configuration.DatabaseName;
        _authenticationComboBox.SelectedItem = configuration.AuthenticationMode;
        _userNameTextBox.Text = configuration.UserName ?? string.Empty;
        _passwordTextBox.Clear();
        _encryptCheckBox.Checked = configuration.Encrypt;
        _trustServerCertificateCheckBox.Checked = configuration.TrustServerCertificate;
        _createDatabaseCheckBox.Checked = configuration.CreateDatabaseIfMissing;
        _timeoutNumeric.Value = Math.Min(
            _timeoutNumeric.Maximum,
            Math.Max(_timeoutNumeric.Minimum, configuration.CommandTimeoutSeconds));
        UpdateCredentialStatus(configuration);
        UpdateAuthenticationState();
    }

    private void UpdateAuthenticationState()
    {
        var sqlPassword =
            _authenticationComboBox.SelectedItem is HiveSqlAuthenticationMode.SqlPassword;

        _userNameTextBox.Enabled = sqlPassword;
        _passwordTextBox.Enabled = sqlPassword;
        _credentialStatus.Enabled = sqlPassword;

        if (!sqlPassword)
        {
            _userNameTextBox.Clear();
            _passwordTextBox.Clear();
        }

        if (sqlPassword && _loadedConfiguration?.BootstrapCredential is not null)
            _credentialStatus.Text = "Saved credential: configured (material hidden).";
        else
            _credentialStatus.Text = sqlPassword
                ? "Saved credential: not configured."
                : "Credential not used.";
    }

    private void UpdateCredentialStatus(HivePersistenceConfiguration configuration)
    {
        _credentialStatus.Text =
            configuration.BootstrapCredential is null
                ? "Saved credential: not configured."
                : "Saved credential: configured (material hidden).";
    }

    private void SetStatus(string text, bool isError)
    {
        _statusLabel.Text = text;

        var theme = _themeManager.Theme;
        _statusLabel.ForeColor = isError
            ? theme.VisualStates.Error
            : theme.Palette.MutedText;
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
        _loadButton.Enabled = !busy;
        _saveButton.Enabled = !busy;
        _testButton.Enabled = !busy;
    }

    private void RememberServer(string? server)
    {
        var value = server?.Trim();

        if (string.IsNullOrWhiteSpace(value))
            return;

        for (var index = 0; index < _serverComboBox.Items.Count; index++)
        {
            if (string.Equals(
                    Convert.ToString(_serverComboBox.Items[index]),
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        _serverComboBox.Items.Add(value);
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationCts?.Cancel();
            _operationCts?.Dispose();
        }

        base.Dispose(disposing);
    }
}
