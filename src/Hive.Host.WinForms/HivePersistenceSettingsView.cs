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
    private readonly TableLayoutPanel _embeddedSection;
    private readonly TableLayoutPanel _sqlSection;
    private readonly TableLayoutPanel _sqlCredentialsField;
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
            Dock = DockStyle.Fill
        };
        ConfigureEditorContent();

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
        _backendComboBox.SelectedIndexChanged += (_, _) => UpdateBackendState();

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
        _createDatabaseCheckBox = new CheckBox
        {
            Text = "Allow database creation when initializing Hive",
            AutoSize = true
        };
        _timeoutNumeric = CreateTimeoutInput();

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
                CreateFieldBlock("Backend", _backendComboBox)));

        _embeddedSection = CreateEmbeddedSection();
        _sqlSection = CreateSqlSection();
        _sqlCredentialsField = CreateCredentialsField();

        AddEditorSection(backendSection);
        AddEditorSection(_embeddedSection);
        AddEditorSection(_sqlSection);

        _migrationView = new HivePersistenceDataMigrationSettingsView(
            _management,
            _accessContext,
            _themeManager,
            _applicationName,
            _output)
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

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

        var migrationTab = new TabPage("Data Migration")
        {
            Padding = new Padding(12),
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

    private void ConfigureEditorContent()
    {
        var fields = _editor.FieldsPanel;
        fields.AutoSize = true;
        fields.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        fields.ColumnStyles.Clear();
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        fields.ColumnCount = 1;
        fields.Padding = Padding.Empty;
    }

    private void AddEditorSection(Control section)
    {
        var fields = _editor.FieldsPanel;
        var row = fields.RowCount++;
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.Controls.Add(section, 0, row);
    }

    private TableLayoutPanel CreateEmbeddedSection()
    {
        var storagePath = CreateFormGrid(
            CreateFieldBlock(
                "Database file",
                CreateStorageLocationPanel()));

        var lifecycle = CreateFormGrid(
            CreateFieldBlock(
                "Initialization",
                CreateCheckBoxHost(
                    _createDatabaseCheckBox)),
            CreateFieldBlock(
                "Command timeout",
                CreateTimeoutField(_timeoutNumeric)));

        return CreateSection(
            "Embedded Storage",
            "The local Hive database is application-owned. Browse to another .db file when the default Local AppData location is not suitable.",
            storagePath,
            lifecycle);
    }

    private TableLayoutPanel CreateSqlSection()
    {
        var server = CreateFormGrid(
            CreateFieldBlock(
                "SQL Server",
                _serverPicker));

        var identity = CreateFormGrid(
            CreateFieldBlock(
                "Database",
                _databaseTextBox),
            CreateFieldBlock(
                "Authentication",
                _authenticationComboBox));

        var security = CreateFormGrid(
            CreateFieldBlock(
                "Connection security",
                CreateSecurityCheckBoxHost()),
            CreateFieldBlock(
                "Command timeout",
                CreateTimeoutField(_timeoutNumeric)));

        var initialization = CreateFormGrid(
            CreateCheckBoxHost(
                _createDatabaseCheckBox));

        var content = CreateSection(
            "SQL Server Connection",
            "Connect to an existing SQL Server deployment. Server discovery is best-effort; Custom... always remains available.",
            server,
            identity,
            _sqlCredentialsField,
            security,
            initialization);

        return content;
    }

    private TableLayoutPanel CreateCredentialsField()
    {
        var credentials = CreateFormGrid(
            CreateFieldBlock("SQL user", _userNameTextBox),
            CreateFieldBlock("Password", _passwordTextBox));

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
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = controls.Length,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        var width = 100f / Math.Max(1, controls.Length);
        for (var i = 0; i < controls.Length; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            grid.Controls.Add(controls[i], i, 0);
        }

        if (controls.Length > 1)
        {
            foreach (Control control in controls)
                control.Margin = new Padding(0, 0, 10, 0);
        }

        if (grid.Controls.Count > 0)
            grid.Controls[^1].Margin = controls.Length > 1
                ? new Padding(0, 0, 0, 0)
                : Padding.Empty;

        return grid;
    }

    private TableLayoutPanel CreateSection(
        string title,
        string description,
        params Control[] content)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(14),
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
        Control editor)
    {
        var block = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 10, 0),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        editor.Dock = DockStyle.Fill;
        editor.Margin = Padding.Empty;

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
            Height = 26,
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
            MaximumSize = new Size(0, 42),
            Margin = new Padding(0, 0, 0, 12)
        };

    private static Control CreateCheckBoxHost(CheckBox checkBox)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        checkBox.Dock = DockStyle.None;
        checkBox.Anchor = AnchorStyles.Left;
        checkBox.Margin = new Padding(0, 0, 0, 0);
        host.Controls.Add(checkBox, 0, 0);
        return host;
    }

    private TableLayoutPanel CreateSecurityCheckBoxHost()
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

    private void UpdateBackendState()
    {
        var embedded = SelectedBackend == HivePersistenceBackend.Embedded;

        _embeddedSection.Visible = embedded;
        _sqlSection.Visible = !embedded;

        UpdateAuthenticationState();

        _testButton.Text = embedded
            ? "Test readiness"
            : "Test connection";

        _createDatabaseCheckBox.Text = embedded
            ? "Allow Hive to create the Embedded database file when initializing"
            : "Allow database creation when initializing Hive";
    }

    private void UpdateAuthenticationState()
    {
        var sqlPassword =
            SelectedBackend == HivePersistenceBackend.SqlServer &&
            _authenticationComboBox.SelectedItem is HiveSqlAuthenticationMode.SqlPassword;

        _sqlCredentialsField.Visible = sqlPassword;

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

    private string DefaultEmbeddedStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Hive",
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
            "hive.db");

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
        }

        base.Dispose(disposing);
    }
}
