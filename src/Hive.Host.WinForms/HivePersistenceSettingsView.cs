using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HivePersistenceSettingsView : UserControl
{
    private sealed record BackendChoice(
        string DisplayName,
        HivePersistenceBackend Backend)
    {
        public override string ToString() => DisplayName;
    }

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly string _applicationName;
    private readonly HiveEditorLayout _editor;
    private readonly HiveTabControl _tabs;
    private readonly HiveComboBox _backendComboBox;
    private readonly TextBox _embeddedStorageTextBox;
    private readonly HiveSqlServerInstancePicker _serverPicker;
    private readonly TextBox _databaseTextBox;
    private readonly HiveComboBox _authenticationComboBox;
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
    private readonly HiveButton _initializeButton;
    private readonly HiveButton _browseEmbeddedButton;
    private readonly HivePersistenceDataMigrationSettingsView _migrationView;
    private readonly List<int> _sqlFieldRows = [];
    private readonly Dictionary<int, float> _fieldRowHeights = [];
    private int _embeddedStorageRow = -1;
    private int _credentialsRow = -1;
    private HiveStatusTone _statusTone = HiveStatusTone.Neutral;

    private CancellationTokenSource? _operationCts;
    private HivePersistenceConfiguration? _loadedConfiguration;

    public HivePersistenceSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        string? applicationName = null,
        IHiveExampleOutput? output = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;
        _applicationName = string.IsNullOrWhiteSpace(applicationName)
            ? HivePersistenceConfiguration.DefaultApplicationName
            : applicationName.Trim();

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        _editor = new HiveEditorLayout
        {
            LabelColumnWidth = 176
        };

        _backendComboBox = new HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _backendComboBox.Items.AddRange(
        [
            new BackendChoice("Embedded", HivePersistenceBackend.Embedded),
            new BackendChoice("SQL Server", HivePersistenceBackend.SqlServer)
        ]);
        _backendComboBox.SelectedIndexChanged += (_, _) => UpdateBackendState();

        _embeddedStorageTextBox = CreateTextBox();
        _serverPicker = new HiveSqlServerInstancePicker(_themeManager);
        _serverPicker.RefreshRequested += async (_, _) =>
            await RunOperationAsync(RefreshSqlServerInstancesAsync).ConfigureAwait(true);

        _databaseTextBox = CreateTextBox();
        _authenticationComboBox = new HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _authenticationComboBox.Items.AddRange(
        [
            HiveSqlAuthenticationMode.WindowsIntegrated,
            HiveSqlAuthenticationMode.SqlPassword
        ]);
        _authenticationComboBox.SelectedIndexChanged += (_, _) => UpdateAuthenticationState();

        _userNameTextBox = CreateTextBox();
        _passwordTextBox = CreateTextBox();
        _passwordTextBox.UseSystemPasswordChar = true;
        _credentialStatus = CreateStatusLabel();

        _encryptCheckBox = new CheckBox
        {
            Text = "Encrypt",
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
            DecimalPlaces = 0,
            Width = 84
        };

        _statusLabel = CreateStatusLabel();
        _statusLabel.AutoSize = false;
        _statusLabel.Height = 36;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;

        _saveButton = _editor.AddActionButton("Save", HiveButtonStyle.Primary, 96);
        _loadButton = _editor.AddActionButton("Load", HiveButtonStyle.Secondary, 96);
        _testButton = _editor.AddActionButton("Test connection", HiveButtonStyle.Secondary, 132);
        _initializeButton = _editor.AddActionButton("Initialize Hive", HiveButtonStyle.Secondary, 122);

        _browseEmbeddedButton = new HiveButton
        {
            Text = "Browse...",
            Style = HiveButtonStyle.Secondary,
            Width = 92,
            Height = 32,
            Margin = new Padding(8, 0, 0, 0)
        };
        _browseEmbeddedButton.Click += (_, _) => BrowseEmbeddedStorage();

        _editor.FooterPanel.Controls.Add(_statusLabel);
        _editor.FooterPanel.Resize += (_, _) => UpdateFooterStatusWidth();
        UpdateFooterStatusWidth();

        _loadButton.Click += async (_, _) => await RunOperationAsync(LoadAsync).ConfigureAwait(true);
        _saveButton.Click += async (_, _) => await RunOperationAsync(SaveAsync).ConfigureAwait(true);
        _testButton.Click += async (_, _) => await RunOperationAsync(TestAsync).ConfigureAwait(true);
        _initializeButton.Click += async (_, _) =>
            await RunOperationAsync(InitializeDatabaseAsync).ConfigureAwait(true);

        _editor.AddField(
            "Backend",
            "Select Embedded or SQL Server. Changing this selector changes configuration only; it does not migrate or activate data.",
            _backendComboBox,
            70);

        _embeddedStorageRow = _editor.FieldsPanel.RowCount;
        _editor.AddField(
            "Storage location",
            "Application-owned Embedded database file. Choose another .db location when the default Local AppData location is not suitable.",
            CreateStorageLocationPanel(),
            74);

        AddSqlField(
            "Server / port",
            "Choose a discoverable SQL Server instance or select Custom... to enter a server or named instance. Port is optional.",
            _serverPicker,
            78);

        SetReadOnlyVisualState(_databaseTextBox, themeManager);
        AddSqlField(
            "Database",
            "Assigned automatically by Hive from the application name.",
            _databaseTextBox,
            62);

        AddSqlField(
            "Authentication",
            "Windows integrated authentication uses the current Windows identity. SQL Server Authentication uses the protected Hive bootstrap credential.",
            _authenticationComboBox,
            62);

        _credentialsRow = _editor.FieldsPanel.RowCount;
        AddSqlField(
            "SQL credentials",
            "Shown only for SQL Server Authentication. Passwords are never stored in the settings JSON; only a protected bootstrap reference is retained.",
            CreateCredentialPanel(),
            82);

        AddSqlField(
            "Security / timeout",
            "Connection encryption settings and command timeout for persistence operations.",
            CreateSecurityTimeoutPanel(),
            70);

        _editor.AddField(
            "Initialization",
            "Explicit initialization may create missing storage and apply Hive schema migrations. Save and readiness testing remain non-destructive.",
            _createDatabaseCheckBox,
            70);

        _migrationView = new HivePersistenceDataMigrationSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _applicationName,
            _output);

        _tabs = new HiveTabControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Hive persistence settings tabs"
        };

        var setupTab = new TabPage("Database Setup")
        {
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        setupTab.Controls.Add(_editor);

        var migrationTab = new TabPage("Data Migration")
        {
            Padding = new Padding(8),
            Margin = Padding.Empty
        };
        migrationTab.Controls.Add(_migrationView);

        _tabs.TabPages.Add(setupTab);
        _tabs.TabPages.Add(migrationTab);
        _tabs.SelectedIndexChanged += TabsSelectedIndexChanged;

        Controls.Add(_tabs);

        _databaseTextBox.Text = HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
        _embeddedStorageTextBox.Text = DefaultEmbeddedStoragePath();

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);

        _backendComboBox.SelectedItem =
            new BackendChoice("SQL Server", HivePersistenceBackend.SqlServer);
        _authenticationComboBox.SelectedItem =
            HiveSqlAuthenticationMode.WindowsIntegrated;

        UpdateBackendState();
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(cancellationToken);

    internal HiveTabControl NavigationTabs => _tabs;

    internal HiveComboBox BackendSelector => _backendComboBox;

    internal HiveSqlServerInstancePicker SqlServerPicker => _serverPicker;

    internal HiveButton BrowseEmbeddedButton => _browseEmbeddedButton;

    internal Label StatusLabel => _statusLabel;

    private async void TabsSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabs.SelectedIndex != 1 ||
            _migrationView.IsDisposed ||
            _migrationView.Disposing)
        {
            return;
        }

        try
        {
            await _migrationView
                .InitializeAsync()
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    "The Data Migration tab could not be initialized.",
                    _output,
                    _themeManager);
            }
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var result = await _management
            .GetPersistenceConfigurationAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            if (!IsDisposed && !Disposing)
            {
                SetStatus(result.Error!.Message, HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    result.Error!.Message,
                    "Hive Persistence",
                    _output,
                    _themeManager);
            }

            return;
        }

        if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
            return;

        ApplyConfiguration(result.Value!);
        if (result.Value!.Backend == HivePersistenceBackend.SqlServer)
            await RefreshSqlServerInstancesAsync(cancellationToken).ConfigureAwait(true);
        SetStatus("Persistence configuration loaded.", HiveStatusTone.Success);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        SetStatus("Saving persistence configuration...", HiveStatusTone.Information);

        HivePersistenceConfiguration configuration;
        HiveBootstrapCredentialReference? createdBootstrapReference = null;
        var previousBootstrapReference = _loadedConfiguration?.BootstrapCredential;

        try
        {
            var built = await BuildConfigurationAsync(cancellationToken)
                .ConfigureAwait(true);

            configuration = built.Configuration;
            createdBootstrapReference = built.CreatedBootstrapCredential;
        }
        catch (ArgumentException exception)
        {
            if (!IsDisposed && !Disposing)
            {
                SetStatus(
                    "Operation failed. See technical details.",
                    HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    "The persistence configuration is invalid.",
                    _output,
                    _themeManager);
            }

            return;
        }

        Result<HivePersistenceConfiguration> result;

        try
        {
            result = await _management
                .SavePersistenceConfigurationAsync(
                    configuration,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (createdBootstrapReference is { } createdReference &&
                createdReference != previousBootstrapReference)
            {
                await TryRemoveBootstrapCredentialAsync(
                    createdReference,
                    "The newly created bootstrap credential could not be removed after the save was cancelled.")
                    .ConfigureAwait(true);
            }

            throw;
        }
        catch
        {
            if (createdBootstrapReference is { } createdReference &&
                createdReference != previousBootstrapReference)
            {
                await TryRemoveBootstrapCredentialAsync(
                    createdReference,
                    "The newly created bootstrap credential could not be removed after the save failed.")
                    .ConfigureAwait(true);
            }

            throw;
        }

        if (result.IsFailure)
        {
            if (createdBootstrapReference is { } createdReference &&
                createdReference != previousBootstrapReference)
            {
                await TryRemoveBootstrapCredentialAsync(
                    createdReference,
                    "The newly created bootstrap credential could not be removed after the save failed.")
                    .ConfigureAwait(true);
            }

            if (!IsDisposed && !Disposing)
            {
                SetStatus(result.Error!.Message, HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    result.Error!.Message,
                    "Hive Persistence",
                    _output,
                    _themeManager);
            }

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
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (cleanup.IsFailure)
            {
                if (IsDisposed || Disposing)
                    return;

                _passwordTextBox.Clear();
                UpdateCredentialStatus(_loadedConfiguration);
                const string message =
                    "Persistence configuration saved, but the previous bootstrap credential could not be removed.";

                SetStatus(message, HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    message,
                    "Hive Persistence",
                    _output,
                    _themeManager);
                return;
            }
        }

        if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
            return;

        _passwordTextBox.Clear();
        UpdateCredentialStatus(_loadedConfiguration);
        SetStatus(
            "Persistence configuration saved successfully. Database/schema state was not changed.",
            HiveStatusTone.Success);

        if (IsDisposed || Disposing)
            return;

        var targetDescription = _loadedConfiguration.Backend == HivePersistenceBackend.Embedded
            ? $"Embedded storage: {_loadedConfiguration.EmbeddedStoragePath}"
            : $"Database: {_loadedConfiguration.DatabaseName}";

        HiveMessageBox.ShowInformation(
            FindForm(),
            $"Persistence settings saved. {targetDescription}",
            "Hive Persistence");
    }

    private async Task InitializeDatabaseAsync(
        CancellationToken cancellationToken)
    {
        HivePersistenceConfiguration configuration;

        try
        {
            configuration = (await BuildConfigurationAsync(
                    cancellationToken,
                    persistCredential: false)
                .ConfigureAwait(true)).Configuration;
        }
        catch (ArgumentException exception)
        {
            if (!IsDisposed && !Disposing)
            {
                SetStatus(
                    "Persistence configuration is invalid. See technical details.",
                    HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    "The persistence configuration is invalid.",
                    _output,
                    _themeManager);
            }

            return;
        }

        if (IsDisposed || Disposing)
            return;

        SetStatus(
            $"Initializing {configuration.Backend} persistence and applying schema migrations...",
            HiveStatusTone.Information);

        var result = await _management
            .InitializePersistenceAsync(
                configuration,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
            return;

        if (result.IsFailure)
        {
            SetStatus(result.Error!.Message, HiveStatusTone.Error);

            HiveUiErrorReporter.Report(
                FindForm(),
                result.Error!.Message,
                "Hive Persistence",
                _output,
                _themeManager);
            return;
        }

        var message = configuration.Backend == HivePersistenceBackend.Embedded
            ? "Embedded Hive persistence initialization completed successfully. The local database is now ready for normal Hive operations."
            : "SQL Server Hive database initialization completed successfully. The database is now ready for normal Hive operations.";

        SetStatus(message, HiveStatusTone.Success);

        if (IsDisposed || Disposing)
            return;

        HiveMessageBox.ShowInformation(
            FindForm(),
            message,
            "Hive Persistence");
    }

    private async Task TestAsync(CancellationToken cancellationToken)
    {
        SetStatus(
            SelectedBackend == HivePersistenceBackend.Embedded
                ? "Testing Embedded storage readiness..."
                : "Testing SQL Server connection...",
            HiveStatusTone.Information);

        HivePersistenceConfiguration configuration;

        try
        {
            configuration = (await BuildConfigurationAsync(
                    cancellationToken,
                    persistCredential: false)
                .ConfigureAwait(true)).Configuration;
        }
        catch (ArgumentException exception)
        {
            if (!IsDisposed && !Disposing)
            {
                SetStatus(
                    "Persistence configuration is invalid. See technical details.",
                    HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    "The persistence configuration is invalid.",
                    _output,
                    _themeManager);
            }

            return;
        }

        var result = await _management
            .TestPersistenceConnectionAsync(
                configuration,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
            return;

        if (result.IsFailure)
        {
            var failureMessage = $"Connection test failed: {result.Error!.Message}";
            SetStatus(failureMessage, HiveStatusTone.Error);

            HiveUiErrorReporter.Report(
                FindForm(),
                failureMessage,
                "Hive Persistence",
                _output,
                _themeManager);
            return;
        }

        var value = result.Value!;
        var successMessage =
            $"{value.Message} " +
            $"Database state: {value.DatabaseState}. " +
            $"Schema: {value.CurrentSchemaVersion?.ToString() ?? "not initialized"} " +
            $"(supported {value.SupportedSchemaVersion}).";

        SetStatus(successMessage, HiveStatusTone.Success);

        if (IsDisposed || Disposing)
            return;

        HiveMessageBox.ShowInformation(
            FindForm(),
            successMessage,
            "Hive Persistence");
    }

    private async Task<(
        HivePersistenceConfiguration Configuration,
        HiveBootstrapCredentialReference? CreatedBootstrapCredential)> BuildConfigurationAsync(
        CancellationToken cancellationToken,
        bool persistCredential = true)
    {
        if (!int.TryParse(_timeoutNumeric.Value.ToString(), out var timeout))
            timeout = 30;

        var backend =
            (_backendComboBox.SelectedItem as BackendChoice)?.Backend
            ?? throw new ArgumentException("Choose a persistence backend.");

        if (backend == HivePersistenceBackend.Embedded)
        {
            var embeddedConfiguration = HivePersistenceConfiguration.Embedded(
                _embeddedStorageTextBox.Text,
                createDatabaseIfMissing: _createDatabaseCheckBox.Checked,
                commandTimeoutSeconds: timeout);

            return (embeddedConfiguration, null);
        }

        var resolvedPort = _serverPicker.Port;

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
            _serverPicker.ServerName,
            resolvedPort,
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
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
            return (configuration, null);

        HiveBootstrapCredentialReference? createdBootstrapCredential = null;

        try
        {
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

                    createdBootstrapCredential = credential;

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

            return (configuration, createdBootstrapCredential);
        }
        catch
        {
            if (createdBootstrapCredential is { } createdReference)
            {
                await TryRemoveBootstrapCredentialAsync(
                    createdReference,
                    "The newly created bootstrap credential could not be removed after configuration construction failed.")
                    .ConfigureAwait(true);
            }

            throw;
        }
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
        _backendComboBox.SelectedItem = configuration.Backend == HivePersistenceBackend.Embedded
            ? new BackendChoice("Embedded", HivePersistenceBackend.Embedded)
            : new BackendChoice("SQL Server", HivePersistenceBackend.SqlServer);
        _embeddedStorageTextBox.Text =
            configuration.EmbeddedStoragePath ?? DefaultEmbeddedStoragePath();
        _serverPicker.SetValue(
            configuration.ServerName,
            configuration.Port);
        _databaseTextBox.Text = configuration.Backend == HivePersistenceBackend.SqlServer
            ? configuration.DatabaseName
            : string.Empty;
        _authenticationComboBox.SelectedItem = configuration.AuthenticationMode;
        _userNameTextBox.Text = configuration.UserName ?? string.Empty;
        _passwordTextBox.Clear();
        _encryptCheckBox.Checked = configuration.Encrypt;
        _trustServerCertificateCheckBox.Checked = configuration.TrustServerCertificate;
        _createDatabaseCheckBox.Checked = configuration.CreateDatabaseIfMissing;
        _timeoutNumeric.Value = Math.Min(
            _timeoutNumeric.Maximum,
            Math.Max(_timeoutNumeric.Minimum, configuration.CommandTimeoutSeconds));
        UpdateBackendState();
    }

    private HivePersistenceBackend SelectedBackend =>
        (_backendComboBox.SelectedItem as BackendChoice)?.Backend
        ?? HivePersistenceBackend.SqlServer;

    private void UpdateBackendState()
    {
        var embedded = SelectedBackend == HivePersistenceBackend.Embedded;

        SetFieldVisible(_embeddedStorageRow, embedded);

        foreach (var row in _sqlFieldRows)
            SetFieldVisible(row, !embedded);

        UpdateAuthenticationState();

        _testButton.Text = embedded
            ? "Test readiness"
            : "Test connection";

        _createDatabaseCheckBox.Text = embedded
            ? "Allow Hive to create the Embedded database file when initializing"
            : "Allow database creation when initializing Hive";
    }

    private void SetFieldVisible(int row, bool visible)
    {
        var fields = _editor.FieldsPanel;
        if (row < 0 || row >= fields.RowCount)
            return;

        var labelPanel = fields.GetControlFromPosition(0, row);
        var editorPanel = fields.GetControlFromPosition(1, row);

        if (labelPanel is not null)
            labelPanel.Visible = visible;
        if (editorPanel is not null)
            editorPanel.Visible = visible;

        if (row >= fields.RowStyles.Count)
            return;

        if (visible && _fieldRowHeights.TryGetValue(row, out var height))
            fields.RowStyles[row].Height = height;
        else if (!visible)
            fields.RowStyles[row].Height = 0;
    }

    private void AddSqlField(
        string title,
        string description,
        Control editor,
        int height = 72)
    {
        var row = _editor.FieldsPanel.RowCount;
        _editor.AddField(title, description, editor, height);
        _sqlFieldRows.Add(row);
        _fieldRowHeights[row] = _editor.FieldsPanel.RowStyles[row].Height;
    }

    private void UpdateAuthenticationState()
    {
        var sqlPassword =
            SelectedBackend == HivePersistenceBackend.SqlServer &&
            _authenticationComboBox.SelectedItem is HiveSqlAuthenticationMode.SqlPassword;

        SetFieldVisible(_credentialsRow, sqlPassword);

        if (!sqlPassword)
        {
            _userNameTextBox.Clear();
            _passwordTextBox.Clear();
        }

        if (SelectedBackend == HivePersistenceBackend.Embedded)
        {
            _credentialStatus.Text = "Not used for Embedded persistence.";
        }
        else if (_authenticationComboBox.SelectedItem is HiveSqlAuthenticationMode.WindowsIntegrated)
        {
            _credentialStatus.Text = "Windows identity is used; no SQL credentials are required.";
        }
        else if (_loadedConfiguration?.BootstrapCredential is not null)
        {
            _credentialStatus.Text = "Saved credential: configured (material hidden).";
        }
        else
        {
            _credentialStatus.Text = "Enter a password and save to create the protected bootstrap credential.";
        }
    }

    private Control CreateStorageLocationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        panel.Controls.Add(_embeddedStorageTextBox, 0, 0);
        panel.Controls.Add(_browseEmbeddedButton, 1, 0);
        return panel;
    }

    private Control CreateCredentialPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));

        panel.Controls.Add(_userNameTextBox, 0, 0);

        var passwordHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        passwordHost.Controls.Add(_passwordTextBox, 0, 0);
        panel.Controls.Add(passwordHost, 1, 0);
        panel.Controls.Add(_credentialStatus, 0, 1);
        panel.SetColumnSpan(_credentialStatus, 2);
        return panel;
    }

    private Control CreateSecurityTimeoutPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));

        panel.Controls.Add(_encryptCheckBox, 0, 0);
        panel.Controls.Add(_trustServerCertificateCheckBox, 1, 0);

        var timeout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        timeout.Controls.Add(new Label
        {
            Text = "Timeout (s)",
            AutoSize = true,
            Margin = new Padding(0, 7, 6, 0)
        });
        timeout.Controls.Add(_timeoutNumeric);
        panel.Controls.Add(timeout, 2, 0);
        return panel;
    }

    private void BrowseEmbeddedStorage()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Choose Embedded Hive database location",
            Filter = "Hive database (*.db)|*.db|All files (*.*)|*.*",
            DefaultExt = "db",
            AddExtension = true,
            FileName = Path.GetFileName(_embeddedStorageTextBox.Text),
            InitialDirectory = GetExistingDirectory(_embeddedStorageTextBox.Text),
            OverwritePrompt = false
        };

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            _embeddedStorageTextBox.Text = dialog.FileName;
    }

    private static string GetExistingDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private async Task RefreshSqlServerInstancesAsync(CancellationToken cancellationToken)
    {
        if (SelectedBackend != HivePersistenceBackend.SqlServer)
            return;

        var preferred = _serverPicker.ServerName;
        SetStatus("Discovering visible SQL Server instances...", HiveStatusTone.Information);

        try
        {
            await _serverPicker
                .RefreshAsync(preferred, cancellationToken)
                .ConfigureAwait(true);

            SetStatus(
                string.IsNullOrWhiteSpace(preferred)
                    ? "SQL Server instance list refreshed."
                    : $"SQL Server instance list refreshed. Current selection: {preferred}.",
                HiveStatusTone.Success);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            SetStatus(
                "SQL Server instance discovery failed; Custom... remains available.",
                HiveStatusTone.Warning);

            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Hive Persistence",
                "SQL Server instance discovery could not be completed. You can enter a server manually using Custom....",
                _output,
                _themeManager);
        }
    }

    private async Task TryRemoveBootstrapCredentialAsync(
        HiveBootstrapCredentialReference reference,
        string cleanupFailureMessage)
    {
        try
        {
            var configuration = await _management
                .GetPersistenceConfigurationAsync(
                    _accessContext,
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (configuration.IsFailure)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    cleanupFailureMessage,
                    "Hive Persistence",
                    _output,
                    _themeManager);
                return;
            }

            if (configuration.Value!.BootstrapCredential == reference)
                return;

            var cleanup = await _management
                .RemoveBootstrapCredentialAsync(
                    reference,
                    _accessContext,
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (cleanup.IsSuccess || IsDisposed || Disposing)
                return;

            HiveUiErrorReporter.Report(
                FindForm(),
                cleanupFailureMessage,
                "Hive Persistence",
                _output,
                _themeManager);
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    cleanupFailureMessage,
                    _output,
                    _themeManager);
            }
        }
    }

    private void UpdateCredentialStatus(HivePersistenceConfiguration configuration)
    {
        if (configuration.Backend == HivePersistenceBackend.Embedded)
        {
            _credentialStatus.Text = "Credential not used — Embedded persistence.";
            return;
        }

        _credentialStatus.Text =
            configuration.AuthenticationMode == HiveSqlAuthenticationMode.WindowsIntegrated
                ? "Credential not used — Windows integrated authentication."
                : configuration.BootstrapCredential is null
                    ? "Saved credential: not configured."
                    : "Saved credential: configured (material hidden).";
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        ApplyStatusVisual();

    private void UpdateFooterStatusWidth()
    {
        var buttonsWidth = _editor.FooterPanel.Controls
            .OfType<HiveButton>()
            .Sum(static button => button.Width + button.Margin.Horizontal + 8);

        _statusLabel.Width = Math.Max(
            180,
            _editor.FooterPanel.ClientSize.Width - buttonsWidth - 24);
    }

    private void SetStatus(string text, HiveStatusTone tone)
    {
        _statusTone = tone;
        _statusLabel.Text = text;
        ApplyStatusVisual();
    }

    private void ApplyStatusVisual()
    {
        var theme = _themeManager.Theme;
        _statusLabel.ForeColor = _statusTone switch
        {
            HiveStatusTone.Information => theme.VisualStates.Information,
            HiveStatusTone.Success => theme.VisualStates.Success,
            HiveStatusTone.Warning => theme.VisualStates.Warning,
            HiveStatusTone.Error => theme.VisualStates.Error,
            _ => theme.Palette.MutedText
        };
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation)
    {
        _operationCts?.Cancel();

        var operationCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(
            ref _operationCts,
            operationCts);
        previous?.Cancel();

        if (IsDisposed || Disposing)
        {
            Interlocked.CompareExchange(
                ref _operationCts,
                null,
                operationCts);
            operationCts.Dispose();
            return;
        }

        SetBusy(true);

        try
        {
            await operation(operationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
            when (operationCts.IsCancellationRequested)
        {
            if (!IsDisposed && !Disposing)
                SetStatus("Operation cancelled.", HiveStatusTone.Warning);
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                SetStatus(
                    "Operation failed. See technical details.",
                    HiveStatusTone.Error);

                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    "The persistence operation could not be completed.",
                    _output,
                    _themeManager);
            }
        }
        finally
        {
            if (ReferenceEquals(_operationCts, operationCts))
                Interlocked.CompareExchange(
                    ref _operationCts,
                    null,
                    operationCts);

            operationCts.Dispose();

            if (!IsDisposed && !Disposing)
                SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _loadButton.Enabled = !busy;
        _saveButton.Enabled = !busy;
        _testButton.Enabled = !busy;
        _initializeButton.Enabled = !busy;
        _backendComboBox.Enabled = !busy;
        _embeddedStorageTextBox.Enabled = !busy;
        _browseEmbeddedButton.Enabled = !busy;
        _serverPicker.SetEnabled(!busy);
        if (!busy)
            UpdateBackendState();
    }

    private static TextBox CreateTextBox() =>
        new()
        {
            Height = 32,
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle
        };


    private static void SetReadOnlyVisualState(
        TextBox textBox,
        IHiveThemeManager themeManager)
    {
        textBox.ReadOnly = true;
        textBox.TabStop = false;
        textBox.Cursor = Cursors.Arrow;
        textBox.BackColor = themeManager.Theme.Palette.ElevatedSurface;
        textBox.ForeColor = themeManager.Theme.Palette.MutedText;
    }

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
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

            var operationCts = Interlocked.Exchange(
                ref _operationCts,
                null);
            operationCts?.Cancel();
        }

        base.Dispose(disposing);
    }
}
