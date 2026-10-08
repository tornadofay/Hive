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
    private readonly CheckBox _embeddedCreateDatabaseCheckBox;
    private readonly CheckBox _sqlCreateDatabaseCheckBox;
    private readonly NumericUpDown _embeddedTimeoutNumeric;
    private readonly NumericUpDown _sqlTimeoutNumeric;
    private readonly Label _statusLabel;
    private readonly HiveButton _loadButton;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _testButton;
    private readonly HiveButton _initializeButton;
    private readonly HiveButton _browseEmbeddedButton;
    private HivePersistenceDataMigrationSettingsView? _migrationView;
    private readonly TabPage _migrationTab;
    private readonly TableLayoutPanel _embeddedSection;
    private readonly TableLayoutPanel _sqlSection;
    private readonly TableLayoutPanel _sqlCredentialsField;
    private HiveStatusTone _statusTone = HiveStatusTone.Neutral;
    private bool _updatingBackendSelection;


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
            Dock = DockStyle.Fill
        };
        _editor.FieldsPanel.AutoSize = true;
        _editor.FieldsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _backendComboBox = new HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        _backendComboBox.Items.AddRange(
        [
            new BackendChoice("Embedded", HivePersistenceBackend.Embedded),
            new BackendChoice("SQL Server", HivePersistenceBackend.SqlServer)
        ]);
        _backendComboBox.SelectedIndexChanged += async (_, _) =>
            await BackendSelectionChangedAsync().ConfigureAwait(true);

        _embeddedStorageTextBox = CreateTextBox();
        _serverPicker = new HiveSqlServerInstancePicker(_themeManager);
        _serverPicker.RefreshRequested += async (_, _) =>
            await RunOperationAsync(RefreshSqlServerInstancesAsync).ConfigureAwait(true);

        _databaseTextBox = CreateTextBox();
        SetReadOnlyVisualState(_databaseTextBox, themeManager);

        _authenticationComboBox = new HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        _authenticationComboBox.Items.AddRange(
        [
            HiveSqlAuthenticationMode.WindowsIntegrated,
            HiveSqlAuthenticationMode.SqlPassword
        ]);
        _authenticationComboBox.SelectedIndexChanged += (_, _) => UpdateAuthenticationState();

        _userNameTextBox = CreateTextBox();
        _userNameTextBox.PlaceholderText = "SQL user";
        _passwordTextBox = CreateTextBox();
        _passwordTextBox.PlaceholderText = "Password";
        _passwordTextBox.UseSystemPasswordChar = true;
        _credentialStatus = CreateStatusLabel();

        _encryptCheckBox = new CheckBox
        {
            Text = "Encrypt connection",
            AutoSize = true
        };
        _trustServerCertificateCheckBox = new CheckBox
        {
            Text = "Trust server certificate",
            AutoSize = true
        };
        _embeddedCreateDatabaseCheckBox = new CheckBox
        {
            Text = "Allow Hive to create the Embedded database file when initialization is requested.",
            AutoSize = true
        };
        _sqlCreateDatabaseCheckBox = new CheckBox
        {
            Text = "Allow database creation when initializing Hive.",
            AutoSize = true
        };
        _embeddedTimeoutNumeric = CreateTimeoutInput();
        _sqlTimeoutNumeric = CreateTimeoutInput();


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
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };
        _browseEmbeddedButton.Click += (_, _) => BrowseEmbeddedStorage();

        _editor.FooterPanel.Controls.Add(_statusLabel);
        _editor.FooterPanel.Resize += FooterPanelOnResize;

        _loadButton.Click += async (_, _) => await RunOperationAsync(LoadAsync).ConfigureAwait(true);
        _saveButton.Click += async (_, _) => await RunOperationAsync(SaveAsync).ConfigureAwait(true);
        _testButton.Click += async (_, _) => await RunOperationAsync(TestAsync).ConfigureAwait(true);
        _initializeButton.Click += async (_, _) =>
            await RunOperationAsync(InitializeDatabaseAsync).ConfigureAwait(true);

        var backendSection = CreateSection(
            "Persistence Backend",
            "Choose where Hive stores its durable state. Changing this selector does not migrate data or activate a new backend.",
            CreateFormGrid(
                CreateFieldBlock("Backend", _backendComboBox, 120)));

        _updatingBackendSelection = true;
        try
        {
            _embeddedSection = CreateEmbeddedSection();
            _sqlCredentialsField = CreateCredentialsField();
            _sqlSection = CreateSqlSection();
        }
        finally
        {
            _updatingBackendSelection = false;
        }

        AddEditorSection(backendSection);
        AddEditorSection(_embeddedSection);
        AddEditorSection(_sqlSection);

        _tabs = new HiveTabControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Hive persistence settings tabs"
        };

        var setupTab = new TabPage("Database Setup")
        {
            Padding = new Padding(12),
            Margin = Padding.Empty
        };
        setupTab.Controls.Add(_editor);

        _migrationTab = new TabPage("Data Migration")
        {
            Padding = new Padding(12),
            Margin = Padding.Empty
        };
        _migrationTab.Controls.Add(CreateDeferredMigrationPlaceholder());

        _tabs.TabPages.Add(setupTab);
        _tabs.TabPages.Add(_migrationTab);
        _tabs.SelectedIndexChanged += TabsSelectedIndexChanged;

        Controls.Add(_tabs);

        _databaseTextBox.Text = HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
        _embeddedStorageTextBox.Text = DefaultEmbeddedStoragePath();
        _serverPicker.Port = 1433;

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);

        _updatingBackendSelection = true;
        try
        {
            _backendComboBox.SelectedItem =
                new BackendChoice("SQL Server", HivePersistenceBackend.SqlServer);
            _authenticationComboBox.SelectedItem =
                HiveSqlAuthenticationMode.WindowsIntegrated;
        }
        finally
        {
            _updatingBackendSelection = false;
        }

        UpdateBackendState();
        UpdateFooterStatusWidth();
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(cancellationToken);

    internal HiveTabControl NavigationTabs => _tabs;

    internal HiveComboBox BackendSelector => _backendComboBox;

    internal HiveSqlServerInstancePicker SqlServerPicker => _serverPicker;

    internal HiveButton BrowseEmbeddedButton => _browseEmbeddedButton;

    internal Label StatusLabel => _statusLabel;

    internal TextBox DatabaseInput => _databaseTextBox;

    internal HiveComboBox AuthenticationSelector => _authenticationComboBox;

    internal TextBox EmbeddedStorageInput => _embeddedStorageTextBox;

    internal int SqlPort => _serverPicker.Port ?? 0;

    private async void TabsSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabs.SelectedIndex != 1)
            return;

        var migrationView = EnsureMigrationView();
        if (migrationView.IsDisposed || migrationView.Disposing)
            return;

        try
        {
            await migrationView
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

    private HivePersistenceDataMigrationSettingsView EnsureMigrationView()
    {
        if (_migrationView is not null)
            return _migrationView;

        var migrationView = new HivePersistenceDataMigrationSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _applicationName,
            _output)
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _migrationTab.Controls.Clear();
        _migrationTab.Controls.Add(migrationView);
        _migrationView = migrationView;
        return migrationView;
    }

    private static Control CreateDeferredMigrationPlaceholder() =>
        new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

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

        _updatingBackendSelection = true;
        try
        {
            ApplyConfiguration(result.Value!);
        }
        finally
        {
            _updatingBackendSelection = false;
        }

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
            var failureMessage =
                configuration.Backend == HivePersistenceBackend.SqlServer
                    ? $"Connection test failed for SQL Server '{configuration.ServerName}' / database '{configuration.DatabaseName}': {result.Error!.Message}"
                    : $"Connection test failed for Embedded storage '{configuration.EmbeddedStoragePath}': {result.Error!.Message}";
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


        var backend =
            (_backendComboBox.SelectedItem as BackendChoice)?.Backend
            ?? throw new ArgumentException("Choose a persistence backend.");

        if (backend == HivePersistenceBackend.Embedded)
        {
            var embeddedConfiguration = HivePersistenceConfiguration.Embedded(
                _embeddedStorageTextBox.Text,
                createDatabaseIfMissing: _embeddedCreateDatabaseCheckBox.Checked,
                commandTimeoutSeconds: (int)_embeddedTimeoutNumeric.Value);

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
            _sqlCreateDatabaseCheckBox.Checked,
            (int)_sqlTimeoutNumeric.Value);

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
            string.IsNullOrWhiteSpace(configuration.ServerName)
                ? "localhost"
                : configuration.ServerName,
            configuration.Port);
        _databaseTextBox.Text = configuration.Backend == HivePersistenceBackend.SqlServer
            ? (string.IsNullOrWhiteSpace(configuration.DatabaseName)
                ? HivePersistenceConfiguration.BuildDatabaseName(_applicationName)
                : configuration.DatabaseName)
            : HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
        _authenticationComboBox.SelectedItem = configuration.AuthenticationMode;
        _userNameTextBox.Text = configuration.UserName ?? string.Empty;
        _passwordTextBox.Clear();
        _encryptCheckBox.Checked = configuration.Encrypt;
        _trustServerCertificateCheckBox.Checked = configuration.TrustServerCertificate;
        _embeddedCreateDatabaseCheckBox.Checked = configuration.CreateDatabaseIfMissing;
        _sqlCreateDatabaseCheckBox.Checked = configuration.CreateDatabaseIfMissing;
        SetTimeoutValue(
            _embeddedTimeoutNumeric,
            configuration.CommandTimeoutSeconds);
        SetTimeoutValue(
            _sqlTimeoutNumeric,
            configuration.CommandTimeoutSeconds);
        UpdateBackendState();
    }

    private HivePersistenceBackend SelectedBackend =>
        (_backendComboBox.SelectedItem as BackendChoice)?.Backend
        ?? HivePersistenceBackend.SqlServer;

    private void AddEditorSection(Control section)
    {
        var fields = _editor.FieldsPanel;
        var row = fields.RowCount++;
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.Controls.Add(section, 0, row);
        fields.SetColumnSpan(section, 2);
    }

    private TableLayoutPanel CreateEmbeddedSection()
    {
        var storagePath = CreateFormGrid(
            CreateFieldBlock(
                "Database file",
                CreateStorageLocationPanel()));

        var lifecycle = CreateFormGrid(
            CreateCheckBoxField(
                "Initialization",
                _embeddedCreateDatabaseCheckBox),
            CreateFieldBlock(
                "Command timeout",
                CreateTimeoutField(_embeddedTimeoutNumeric),
                180));

        return CreateSection(
            "Embedded Storage",
            "Application-owned local Hive database. Browse to another .db file when the default location is not suitable.",
            storagePath,
            lifecycle);
    }

    private TableLayoutPanel CreateSqlSection()
    {
        var server = CreateFormGrid(
            CreateFieldBlock(
                "SQL Server",
                _serverPicker,
                460));

        var initialization = CreateCheckBoxField(
            "Initialization",
            _sqlCreateDatabaseCheckBox);
        initialization.Margin = new Padding(0, 0, 0, 6);

        var identity = CreateFormGrid(
            CreateFieldBlock(
                "Database",
                _databaseTextBox,
                280),
            CreateFieldBlock(
                "Authentication",
                _authenticationComboBox,
                220));

        var security = CreateFormGrid(
            CreateFieldBlock(
                "Connection security",
                CreateSecurityCheckBoxHost(),
                280),
            CreateFieldBlock(
                "Command timeout",
                CreateTimeoutField(_sqlTimeoutNumeric),
                180));

        var content = CreateSection(
            "SQL Server Connection",
            "Connect to an existing SQL Server deployment. Discovery is automatic when SQL Server is selected; Custom... remains available for manual targets.",
            server,
            initialization,
            identity,
            _sqlCredentialsField,
            security);

        return content;
    }

    private Control CreateStorageLocationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
        panel.Controls.Add(_embeddedStorageTextBox, 0, 0);
        panel.Controls.Add(_browseEmbeddedButton, 1, 0);
        return panel;
    }

    private TableLayoutPanel CreateCredentialsField()
    {
        var credentials = CreateFormGrid(
            CreateFieldBlock("SQL user", _userNameTextBox, 260),
            CreateFieldBlock("Password", _passwordTextBox, 260));

        var container = CreateVerticalStack();
        container.Controls.Add(credentials);
        container.Controls.Add(_credentialStatus);
        _credentialStatus.Margin = new Padding(0, 6, 0, 0);

        return CreateLabeledContainer(
            "SQL credentials",
            "Only used with SQL Server Authentication. Hive stores a protected bootstrap reference rather than the password.",
            container);
    }

    private TableLayoutPanel CreateFormGrid(
        params Control[] controls)
    {
        if (controls.Length == 0)
            throw new ArgumentException(
                "At least one control is required.",
                nameof(controls));

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = controls.Length,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        var width = 100f / controls.Length;
        for (var i = 0; i < controls.Length; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));

            var control = controls[i];
            var rightGap = i < controls.Length - 1 ? 10 : 0;
            control.Margin = new Padding(
                control.Margin.Left,
                control.Margin.Top,
                rightGap,
                control.Margin.Bottom);
            grid.Controls.Add(control, i, 0);
        }

        return grid;
    }

    private static TableLayoutPanel CreateCheckBoxField(
        string title,
        CheckBox checkBox)
    {
        var field = CreateVerticalStack();
        field.Margin = new Padding(0, 0, 0, 8);
        field.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 20,
                Font = new Font(
                    SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                    FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            });

        checkBox.AutoSize = true;
        checkBox.Anchor = AnchorStyles.Left;
        checkBox.Margin = new Padding(0, 5, 0, 0);
        field.Controls.Add(checkBox);

        return field;
    }

    private TableLayoutPanel CreateSection(
        string title,
        string description,
        params Control[] content)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            AccessibleName = title
        };

        var heading = CreateSectionHeading(title);
        var copy = CreateSectionDescription(description);

        section.Controls.Add(heading, 0, 0);
        section.Controls.Add(copy, 0, 1);

        foreach (var child in content)
        {
            var row = section.RowCount++;
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.Controls.Add(child, 0, row);
        }

        return section;
    }

    private TableLayoutPanel CreateLabeledContainer(
        string title,
        string description,
        Control content)
    {
        var wrapper = CreateVerticalStack();
        wrapper.Controls.Add(CreateSectionHeading(title));
        wrapper.Controls.Add(CreateSectionDescription(description));
        wrapper.Controls.Add(content);
        return wrapper;
    }

    private static TableLayoutPanel CreateVerticalStack()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
    }

    private static TableLayoutPanel CreateFieldBlock(
        string title,
        Control editor,
        int? editorWidth = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var block = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 8, 0),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
        block.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 20,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        var dynamicHeightEditor = editor is HiveSqlServerInstancePicker;

        editor.AutoSize = dynamicHeightEditor;
        editor.Margin = Padding.Empty;

        if (editorWidth is > 0)
        {
            editor.Dock = DockStyle.Fill;
            editor.Width = editorWidth.Value;

            var editorHeight = Math.Max(
                34,
                Math.Max(
                    editor.Height,
                    Math.Max(
                        editor.MinimumSize.Height,
                        editor.PreferredSize.Height)));

            editor.MinimumSize = new Size(
                editorWidth.Value,
                editorHeight);

            var editorHost = new Panel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = editorWidth.Value,
                MinimumSize = new Size(
                    editorWidth.Value,
                    dynamicHeightEditor ? 36 : editorHeight),
                MaximumSize = new Size(
                    editorWidth.Value,
                    0),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            if (!dynamicHeightEditor)
                editor.Height = editorHeight;

            editorHost.Controls.Add(editor);

            block.Controls.Add(label, 0, 0);
            block.Controls.Add(editorHost, 0, 1);
            return block;
        }

        editor.Dock = DockStyle.Fill;
        var defaultEditorHeight = Math.Max(
            34,
            Math.Max(editor.Height, editor.MinimumSize.Height));
        editor.MinimumSize = new Size(
            editor.MinimumSize.Width,
            defaultEditorHeight);
        editor.Height = defaultEditorHeight;

        block.Controls.Add(label, 0, 0);
        block.Controls.Add(editor, 0, 1);
        return block;
    }

    private static Label CreateSectionHeading(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 24,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Label CreateSectionDescription(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = true,
            MaximumSize = new Size(0, 36),
            Margin = new Padding(0, 0, 0, 8)
        };

    private FlowLayoutPanel CreateSecurityCheckBoxHost()
    {
        var host = CreateHorizontalFlow();
        host.Controls.Add(_encryptCheckBox);
        host.Controls.Add(_trustServerCertificateCheckBox);
        return host;
    }

    private static FlowLayoutPanel CreateHorizontalFlow() =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

    private static TableLayoutPanel CreateTimeoutField(NumericUpDown numeric)
    {
        var host = CreateVerticalStack();
        host.RowCount = 1;
        host.Controls.Add(numeric);
        return host;
    }

    private static NumericUpDown CreateTimeoutInput() =>
        new()
        {
            Minimum = 1,
            Maximum = 600,
            Value = 30,
            DecimalPlaces = 0,
            Width = 72,
            Dock = DockStyle.Left,
            Margin = Padding.Empty
        };

    private static void SetTimeoutValue(
        NumericUpDown numeric,
        int value)
    {
        numeric.Value = Math.Min(
            numeric.Maximum,
            Math.Max(numeric.Minimum, value));
    }

    private void UpdateBackendState()
    {
        var embedded = SelectedBackend == HivePersistenceBackend.Embedded;

        SetEditorSectionVisibility(_embeddedSection, embedded);
        SetEditorSectionVisibility(_sqlSection, !embedded);

        UpdateAuthenticationState();

        _testButton.Text = embedded
            ? "Test readiness"
            : "Test connection";

        _embeddedCreateDatabaseCheckBox.Text =
            "Create the Embedded database file during explicit initialization.";
        _sqlCreateDatabaseCheckBox.Text =
            "Create the SQL Server database during explicit initialization.";
    }

    private async Task BackendSelectionChangedAsync()
    {
        UpdateBackendState();

        if (_updatingBackendSelection ||
            SelectedBackend != HivePersistenceBackend.SqlServer ||
            IsDisposed ||
            Disposing)
        {
            return;
        }

        await RunOperationAsync(RefreshSqlServerInstancesAsync)
            .ConfigureAwait(true);
    }

    private void SetEditorSectionVisibility(
        Control section,
        bool visible)
    {
        section.Visible = visible;

        var row = _editor.FieldsPanel.GetPositionFromControl(section).Row;
        if (row < 0 || row >= _editor.FieldsPanel.RowStyles.Count)
            return;

        var rowStyle = _editor.FieldsPanel.RowStyles[row];
        rowStyle.SizeType = visible
            ? SizeType.AutoSize
            : SizeType.Absolute;
        rowStyle.Height = 0;
    }

    private void UpdateAuthenticationState()
    {
        var sqlPassword =
            SelectedBackend == HivePersistenceBackend.SqlServer &&
            _authenticationComboBox.SelectedItem is HiveSqlAuthenticationMode.SqlPassword;

        _sqlCredentialsField.Visible = sqlPassword;

        var credentialsRow = _sqlSection.GetPositionFromControl(_sqlCredentialsField).Row;
        if (credentialsRow >= 0 &&
            credentialsRow < _sqlSection.RowStyles.Count)
        {
            var rowStyle = _sqlSection.RowStyles[credentialsRow];
            rowStyle.SizeType = sqlPassword
                ? SizeType.AutoSize
                : SizeType.Absolute;
            rowStyle.Height = 0;
        }

        if (!sqlPassword)
        {
            _userNameTextBox.Clear();
            _passwordTextBox.Clear();
        }

        _sqlSection.PerformLayout();
        _editor.FieldsPanel.PerformLayout();

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

    private string DefaultEmbeddedStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Hive",
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
            "hive.db");

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

        var preferred = _loadedConfiguration is null
            ? null
            : _serverPicker.ServerName;
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

    private void FooterPanelOnResize(object? sender, EventArgs e) =>
        UpdateFooterStatusWidth();

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
            Dock = DockStyle.Fill,
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

            _editor.FooterPanel.Resize -= FooterPanelOnResize;

            var operationCts = Interlocked.Exchange(
                ref _operationCts,
                null);
            operationCts?.Cancel();

            _migrationView?.Dispose();
            _migrationView = null;
        }

        base.Dispose(disposing);
    }
}
