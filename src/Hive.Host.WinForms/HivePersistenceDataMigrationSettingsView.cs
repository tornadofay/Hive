using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HivePersistenceDataMigrationSettingsView : UserControl
{
    private enum MigrationDirection
    {
        SqlServerToEmbedded,
        EmbeddedToSqlServer
    }

    private sealed record DirectionChoice(
        string DisplayName,
        MigrationDirection Direction)
    {
        public override string ToString() => DisplayName;
    }

    private enum EndpointRole
    {
        Source,
        Destination
    }

    private sealed class MigrationEndpointEditor : IDisposable
    {
        private readonly IHiveThemeManager _themeManager;
        private readonly string _applicationName;

        private readonly TableLayoutPanel _root;
        private readonly TableLayoutPanel _embeddedPanel;
        private readonly TableLayoutPanel _sqlPanel;

        private readonly TextBox _embeddedPathTextBox;
        private readonly HiveButton _embeddedBrowseButton;
        private readonly CheckBox? _embeddedCreateDatabaseCheckBox;
        private readonly NumericUpDown _embeddedTimeoutNumeric;

        private readonly HiveSqlServerInstancePicker _sqlServerPicker;
        private readonly TextBox _sqlDatabaseTextBox;
        private readonly HiveComboBox _sqlAuthenticationComboBox;
        private readonly TextBox _sqlUserNameTextBox;
        private readonly TextBox _sqlPasswordTextBox;
        private readonly Label _sqlCredentialStatus;
        private readonly CheckBox _sqlEncryptCheckBox;
        private readonly CheckBox _sqlTrustServerCertificateCheckBox;
        private readonly CheckBox? _sqlCreateDatabaseCheckBox;
        private readonly NumericUpDown _sqlTimeoutNumeric;
        private readonly TableLayoutPanel _sqlCredentialField;
        private readonly TableLayoutPanel _sqlAuthenticationField;

        private HivePersistenceBackend _backend;

        public MigrationEndpointEditor(
            EndpointRole role,
            IHiveThemeManager themeManager,
            string applicationName)
        {
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _applicationName = applicationName;

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };

            _embeddedPanel = CreateEmbeddedPanel(
                role,
                out _embeddedPathTextBox,
                out _embeddedBrowseButton,
                out _embeddedCreateDatabaseCheckBox,
                out _embeddedTimeoutNumeric);

            _sqlPanel = CreateSqlPanel(
                role,
                out _sqlServerPicker,
                out _sqlDatabaseTextBox,
                out _sqlAuthenticationComboBox,
                out _sqlUserNameTextBox,
                out _sqlPasswordTextBox,
                out _sqlCredentialStatus,
                out _sqlEncryptCheckBox,
                out _sqlTrustServerCertificateCheckBox,
                out _sqlCreateDatabaseCheckBox,
                out _sqlTimeoutNumeric,
                out _sqlAuthenticationField,
                out _sqlCredentialField);

            _embeddedBrowseButton.Click += (_, _) => BrowseEmbeddedStorage();
            _sqlAuthenticationComboBox.SelectedIndexChanged += (_, _) =>
                UpdateSqlAuthenticationState();

            _root.Controls.Add(_embeddedPanel, 0, 0);
            _root.Controls.Add(_sqlPanel, 0, 0);

            _embeddedPathTextBox.Text = DefaultEmbeddedPath();
            _sqlDatabaseTextBox.Text =
                HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
            _sqlServerPicker.Port = 1433;
            _sqlAuthenticationComboBox.SelectedItem =
                HiveSqlAuthenticationMode.WindowsIntegrated;

            _themeManager.ThemeChanged += ThemeManagerOnChanged;

            Backend = HivePersistenceBackend.Embedded;
        }

        public Control View => _root;

        public HivePersistenceBackend Backend
        {
            get => _backend;
            set
            {
                _backend = value;
                _embeddedPanel.Visible = value == HivePersistenceBackend.Embedded;
                _sqlPanel.Visible = value == HivePersistenceBackend.SqlServer;
                UpdateSqlAuthenticationState();
                _root.PerformLayout();
            }
        }

        public string EmbeddedStoragePath =>
            _embeddedPathTextBox.Text.Trim();

        public TextBox EmbeddedStorageInput =>
            _embeddedPathTextBox;

        public CheckBox? EmbeddedCreateDatabaseCheckBox =>
            _embeddedCreateDatabaseCheckBox;

        public NumericUpDown EmbeddedTimeoutNumeric =>
            _embeddedTimeoutNumeric;

        public HiveSqlServerInstancePicker SqlServerPicker =>
            _sqlServerPicker;

        public string SqlServerName =>
            _sqlServerPicker.ServerName;

        public int? SqlPort =>
            _sqlServerPicker.Port;

        public string SqlDatabaseName =>
            _sqlDatabaseTextBox.Text.Trim();

        public HiveSqlAuthenticationMode SqlAuthentication =>
            _sqlAuthenticationComboBox.SelectedItem is HiveSqlAuthenticationMode value
                ? value
                : HiveSqlAuthenticationMode.WindowsIntegrated;

        public string SqlUserName =>
            _sqlUserNameTextBox.Text.Trim();

        public string SqlPassword =>
            _sqlPasswordTextBox.Text;

        public Label SqlCredentialStatus =>
            _sqlCredentialStatus;

        public CheckBox SqlEncryptCheckBox =>
            _sqlEncryptCheckBox;

        public CheckBox SqlTrustServerCertificateCheckBox =>
            _sqlTrustServerCertificateCheckBox;

        public CheckBox? SqlCreateDatabaseCheckBox =>
            _sqlCreateDatabaseCheckBox;

        public NumericUpDown SqlTimeoutNumeric =>
            _sqlTimeoutNumeric;

        public TableLayoutPanel SqlAuthenticationField =>
            _sqlAuthenticationField;

        public TableLayoutPanel SqlCredentialField =>
            _sqlCredentialField;

        public void SetSqlCredentialStatus(string text) =>
            _sqlCredentialStatus.Text = text;

        private TableLayoutPanel CreateEmbeddedPanel(
            EndpointRole role,
            out TextBox pathTextBox,
            out HiveButton browseButton,
            out CheckBox? createDatabaseCheckBox,
            out NumericUpDown timeoutNumeric)
        {
            pathTextBox = CreateTextBox();
            pathTextBox.PlaceholderText = "Hive database file (.db)";

            browseButton = new HiveButton
            {
                Text = "Browse...",
                Style = HiveButtonStyle.Secondary,
                Width = 88,
                Height = 36,
                Margin = new Padding(8, 0, 0, 0)
            };

            createDatabaseCheckBox = role == EndpointRole.Destination
                ? new CheckBox
                {
                    Text = "Create database file during migration",
                    AutoSize = true,
                    Checked = true
                }
                : null;

            timeoutNumeric = CreateTimeoutInput();

            var panel = CreateVerticalStack();
            panel.Controls.Add(
                CreateFieldBlock(
                    "Database file",
                    CreatePathPanel(pathTextBox, browseButton)));

            panel.Controls.Add(
                CreateFormGrid(
                    role == EndpointRole.Destination
                        ? CreateCheckBoxBlock(
                            "Initialization",
                            createDatabaseCheckBox!)
                        : CreateReadOnlyNote(
                            "Source is read-only. Hive never initializes or creates the source during migration."),
                    CreateFieldBlock(
                        "Command timeout",
                        timeoutNumeric,
                        150)));

            if (role == EndpointRole.Destination)
            {
                panel.Controls.Add(
                    CreateSectionNote(
                        "Migration may create the destination file only when this explicit operation runs. Existing non-empty destinations are rejected."));
            }

            return panel;
        }

        private TableLayoutPanel CreateSqlPanel(
            EndpointRole role,
            out HiveSqlServerInstancePicker serverPicker,
            out TextBox databaseTextBox,
            out HiveComboBox authenticationComboBox,
            out TextBox userNameTextBox,
            out TextBox passwordTextBox,
            out Label credentialStatus,
            out CheckBox encryptCheckBox,
            out CheckBox trustServerCertificateCheckBox,
            out CheckBox? createDatabaseCheckBox,
            out NumericUpDown timeoutNumeric,
            out TableLayoutPanel authenticationField,
            out TableLayoutPanel credentialField)
        {
            serverPicker = new HiveSqlServerInstancePicker(_themeManager)
            {
                Width = 340
            };

            databaseTextBox = CreateTextBox();
            databaseTextBox.PlaceholderText =
                HivePersistenceConfiguration.BuildDatabaseName(_applicationName);

            authenticationComboBox = new HiveComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180,
                Dock = DockStyle.Left
            };
            authenticationComboBox.Items.AddRange(
            [
                HiveSqlAuthenticationMode.WindowsIntegrated,
                HiveSqlAuthenticationMode.SqlPassword
            ]);

            userNameTextBox = CreateTextBox();
            userNameTextBox.PlaceholderText = "SQL user";

            passwordTextBox = CreateTextBox();
            passwordTextBox.PlaceholderText = "Password";
            passwordTextBox.UseSystemPasswordChar = true;

            credentialStatus = CreateStatusLabel();
            credentialStatus.MaximumSize = new Size(0, 34);

            encryptCheckBox = new CheckBox
            {
                Text = "Encrypt",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 10, 0)
            };

            trustServerCertificateCheckBox = new CheckBox
            {
                Text = "Trust server certificate",
                AutoSize = true
            };

            createDatabaseCheckBox = role == EndpointRole.Destination
                ? new CheckBox
                {
                    Text = "Create SQL database during migration",
                    AutoSize = true,
                    Checked = true
                }
                : null;

            timeoutNumeric = CreateTimeoutInput();

            authenticationField = CreateFieldBlock(
                "Authentication",
                authenticationComboBox,
                180);

            credentialField = CreateCredentialField(
                userNameTextBox,
                passwordTextBox,
                credentialStatus);

            var panel = CreateVerticalStack();

            panel.Controls.Add(
                CreateFieldBlock(
                    "SQL Server",
                    serverPicker,
                    340));

            panel.Controls.Add(
                CreateFormGrid(
                    CreateFieldBlock(
                        "Database",
                        databaseTextBox,
                        180),
                    authenticationField));

            panel.Controls.Add(credentialField);

            panel.Controls.Add(
                CreateFieldBlock(
                    "Connection security",
                    CreateSecurityPanel(
                        encryptCheckBox,
                        trustServerCertificateCheckBox),
                    260));

            panel.Controls.Add(
                CreateFormGrid(
                    role == EndpointRole.Destination
                        ? CreateCheckBoxBlock(
                            "Initialization",
                            createDatabaseCheckBox!)
                        : CreateReadOnlyNote(
                            "Source is read-only. Hive never creates or initializes the source database."),
                    CreateFieldBlock(
                        "Command timeout",
                        timeoutNumeric,
                        150)));

            if (role == EndpointRole.Destination)
            {
                panel.Controls.Add(
                    CreateSectionNote(
                        "The destination may be created only by the explicit migration operation. Existing non-empty destinations are rejected."));
            }

            return panel;
        }

        private TableLayoutPanel CreateCredentialField(
            TextBox userNameTextBox,
            TextBox passwordTextBox,
            Label statusLabel)
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                Controls =
                {
                    CreateSectionHeading("SQL credentials"),
                    CreateFormGrid(
                        CreateFieldBlock("SQL user", userNameTextBox, 165),
                        CreateFieldBlock("Password", passwordTextBox, 165)),
                    statusLabel
                }
            };
        }

        private void UpdateSqlAuthenticationState()
        {
            var visible = Backend == HivePersistenceBackend.SqlServer;
            var password = visible &&
                SqlAuthentication == HiveSqlAuthenticationMode.SqlPassword;

            _sqlAuthenticationField.Visible = visible;
            _sqlCredentialField.Visible = password;
            _sqlCredentialStatus.Visible = password;
            _sqlUserNameTextBox.Visible = password;
            _sqlPasswordTextBox.Visible = password;

            if (!password)
                return;

            if (string.IsNullOrWhiteSpace(_sqlCredentialStatus.Text))
            {
                _sqlCredentialStatus.Text =
                    "Enter the destination/source SQL password. Hive uses a protected temporary credential during migration.";
            }
        }

        private void BrowseEmbeddedStorage()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Choose Embedded Hive database location",
                Filter = "Hive database (*.db)|*.db|All files (*.*)|*.*",
                DefaultExt = "db",
                AddExtension = true,
                FileName = Path.GetFileName(_embeddedPathTextBox.Text),
                InitialDirectory = GetExistingDirectory(_embeddedPathTextBox.Text),
                OverwritePrompt = false
            };

            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                _embeddedPathTextBox.Text = dialog.FileName;
        }

        private static string GetExistingDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            return !string.IsNullOrWhiteSpace(directory) &&
                   Directory.Exists(directory)
                ? directory
                : Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
        }

        private string DefaultEmbeddedPath() =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Hive",
                HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
                "hive.db");

        private static TableLayoutPanel CreatePathPanel(
            TextBox pathTextBox,
            HiveButton browseButton)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));
            panel.Controls.Add(pathTextBox, 0, 0);
            panel.Controls.Add(browseButton, 1, 0);
            return panel;
        }

        private static TableLayoutPanel CreateSecurityPanel(
            CheckBox encrypt,
            CheckBox trust)
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            flow.Controls.Add(encrypt);
            flow.Controls.Add(trust);

            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Controls = { flow }
            };
        }

        private static TableLayoutPanel CreateFormGrid(
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
                grid.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, width));
                controls[i].Margin = new Padding(
                    controls[i].Margin.Left,
                    controls[i].Margin.Top,
                    i < controls.Length - 1 ? 8 : controls[i].Margin.Right,
                    controls[i].Margin.Bottom);
                grid.Controls.Add(controls[i], i, 0);
            }

            return grid;
        }

        private static TableLayoutPanel CreateFieldBlock(
            string title,
            Control control,
            int? width = null)
        {
            ArgumentNullException.ThrowIfNull(control);

            control.AutoSize = false;
            control.Dock = width is > 0 ? DockStyle.Left : DockStyle.Fill;
            if (width is > 0)
                control.Width = width.Value;

            if (control.Height < 36)
                control.Height = 36;

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
            block.RowStyles.Add(new RowStyle(SizeType.Absolute, 21f));
            block.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            block.Controls.Add(
                new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    Height = 21,
                    Font = new Font(
                        SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                        FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                },
                0,
                0);

            block.Controls.Add(control, 0, 1);
            return block;
        }

        private static TableLayoutPanel CreateCheckBoxBlock(
            string title,
            CheckBox checkBox)
        {
            var block = CreateVerticalStack();
            block.Margin = new Padding(0, 0, 8, 0);
            block.Controls.Add(
                new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    Height = 21,
                    Font = new Font(
                        SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                        FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                });
            checkBox.AutoSize = true;
            checkBox.Margin = new Padding(0, 4, 0, 0);
            block.Controls.Add(checkBox);
            return block;
        }

        private static Control CreateReadOnlyNote(string text)
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                Height = 44,
                Margin = new Padding(0, 0, 8, 0),
                Padding = Padding.Empty,
                Controls =
                {
                    new Label
                    {
                        Text = text,
                        Dock = DockStyle.Fill,
                        AutoSize = false,
                        AutoEllipsis = true,
                        Margin = Padding.Empty
                    }
                }
            };
        }

        private static TableLayoutPanel CreateVerticalStack() =>
            new()
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
                Margin = new Padding(0, 0, 0, 4)
            };

        private static Label CreateSectionNote(string text) =>
            new()
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = true,
                MaximumSize = new Size(0, 44),
                Margin = new Padding(0, 0, 0, 6)
            };

        private static Label CreateStatusLabel() =>
            new()
            {
                AutoEllipsis = true,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 6)
            };

        private static TextBox CreateTextBox() =>
            new()
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Multiline = false,
                Height = 36,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty
            };

        private static NumericUpDown CreateTimeoutInput() =>
            new()
            {
                Minimum = 1,
                Maximum = 600,
                Value = 30,
                DecimalPlaces = 0,
                Width = 78,
                Height = 36,
                Margin = Padding.Empty
            };

        private static void SetReadOnlyVisualState(
            TextBox textBox,
            IHiveThemeManager themeManager)
        {
            textBox.ReadOnly = true;
            textBox.TabStop = false;
            textBox.BackColor = themeManager.Theme.Palette.ElevatedSurface;
            textBox.ForeColor = themeManager.Theme.Palette.MutedText;
        }

        private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
            _themeManager.Apply(_root);

        public void Dispose()
        {
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;
        }
    }

    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly string _applicationName;
    private readonly HiveEditorLayout _editor;
    private readonly HiveComboBox _directionComboBox;
    private readonly Label _scopeLabel;
    private readonly Panel _sourceCard;
    private readonly Panel _destinationCard;
    private readonly Label _sourceHeader;
    private readonly Label _destinationHeader;
    private readonly Panel _sourceBody;
    private readonly Panel _destinationBody;
    private readonly TableLayoutPanel _roleColumns;
    private readonly MigrationEndpointEditor _sourceEndpoint;
    private readonly MigrationEndpointEditor _destinationEndpoint;
    private readonly HiveButton _refreshButton;
    private readonly HiveButton _migrateButton;
    private readonly Label _statusLabel;

    private HiveStatusTone _statusTone = HiveStatusTone.Neutral;
    private CancellationTokenSource? _operationCts;
    private HiveBootstrapCredentialReference? _createdSourceCredential;
    private HiveBootstrapCredentialReference? _createdDestinationCredential;
    private bool _busy;

    public HivePersistenceDataMigrationSettingsView(
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

        _editor = new HiveEditorLayout();

        _directionComboBox = new HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240,
            Dock = DockStyle.Left
        };
        _directionComboBox.Items.AddRange(
        [
            new DirectionChoice(
                "SQL Server → Embedded",
                MigrationDirection.SqlServerToEmbedded),
            new DirectionChoice(
                "Embedded → SQL Server",
                MigrationDirection.EmbeddedToSqlServer)
        ]);
        _directionComboBox.SelectedIndexChanged += async (_, _) =>
            await DirectionChangedAsync().ConfigureAwait(true);

        _scopeLabel = new Label
        {
            Text = "All Hive Data",
            Dock = DockStyle.Left,
            AutoSize = false,
            Width = 180,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        _sourceEndpoint = new MigrationEndpointEditor(
            EndpointRole.Source,
            _themeManager,
            _applicationName);
        _destinationEndpoint = new MigrationEndpointEditor(
            EndpointRole.Destination,
            _themeManager,
            _applicationName);

        _sourceEndpoint.SqlServerPicker.RefreshRequested += async (_, _) =>
            await RunOperationAsync(
                token => RefreshSqlServerInstancesAsync(
                    _sourceEndpoint,
                    token)).ConfigureAwait(true);
        _destinationEndpoint.SqlServerPicker.RefreshRequested += async (_, _) =>
            await RunOperationAsync(
                token => RefreshSqlServerInstancesAsync(
                    _destinationEndpoint,
                    token)).ConfigureAwait(true);

        _roleColumns = CreateRoleColumns();
        _sourceCard = CreateBackendCard(
            "SOURCE",
            out _sourceHeader,
            out _sourceBody);
        _destinationCard = CreateBackendCard(
            "DESTINATION",
            out _destinationHeader,
            out _destinationBody);

        _sourceBody.Controls.Add(_sourceEndpoint.View);
        _destinationBody.Controls.Add(_destinationEndpoint.View);

        _roleColumns.Controls.Add(_sourceCard, 0, 0);
        _roleColumns.Controls.Add(_destinationCard, 1, 0);

        var top = CreateTopConfigurationRow();

        _editor.FieldsPanel.Controls.Add(top, 0, 0);
        _editor.FieldsPanel.SetColumnSpan(top, 2);
        _editor.FieldsPanel.Controls.Add(_roleColumns, 0, 1);
        _editor.FieldsPanel.SetColumnSpan(_roleColumns, 2);
        _editor.FieldsPanel.RowStyles.Clear();
        _editor.FieldsPanel.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 64f));
        _editor.FieldsPanel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));
        _editor.FieldsPanel.RowCount = 2;

        _refreshButton = _editor.AddActionButton(
            "Refresh",
            HiveButtonStyle.Secondary,
            92);
        _migrateButton = _editor.AddActionButton(
            "Migrate All Data",
            HiveButtonStyle.Primary,
            132);

        _statusLabel = CreateStatusLabel();
        _editor.FooterPanel.Controls.Add(_statusLabel);
        _editor.FooterPanel.Resize += FooterPanelOnResize;

        _refreshButton.Click += async (_, _) =>
            await RunOperationAsync(RefreshStatusAsync).ConfigureAwait(true);
        _migrateButton.Click += async (_, _) =>
            await RunOperationAsync(RunMigrationAsync).ConfigureAwait(true);

        _directionComboBox.SelectedIndex = 0;

        Controls.Add(_editor);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        MinimumSize = new Size(0, 0);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);

        ApplyDirection();
        UpdateFooterStatusWidth();
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        RefreshStatusAsync(cancellationToken);

    internal HiveComboBox DirectionSelector => _directionComboBox;

    internal Label ScopeLabel => _scopeLabel;

    internal HiveComboBox SqlAuthenticationSelector =>
        _destinationEndpoint.SqlAuthentication;

    internal Label StatusLabel => _statusLabel;

    internal FlowLayoutPanel FooterPanel => _editor.FooterPanel;

    internal TableLayoutPanel RoleColumns => _roleColumns;

    internal Panel SourceCard => _sourceCard;

    internal Panel DestinationCard => _destinationCard;

    internal HiveComboBox SourceAuthenticationSelector =>
        _sourceEndpoint.SqlAuthentication;

    internal HiveSqlServerInstancePicker SourceSqlServerPicker =>
        _sourceEndpoint.SqlServerPicker;

    internal HiveSqlServerInstancePicker DestinationSqlServerPicker =>
        _destinationEndpoint.SqlServerPicker;

    internal TextBox SourceEmbeddedStorageInput =>
        _sourceEndpoint.EmbeddedStorageInput;

    internal TextBox DestinationEmbeddedStorageInput =>
        _destinationEndpoint.EmbeddedStorageInput;

    private Control CreateTopConfigurationRow()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

        table.Controls.Add(
            CreateFieldBlock(
                "Direction",
                _directionComboBox,
                220),
            0,
            0);

        table.Controls.Add(
            CreateFieldBlock(
                "Scope",
                _scopeLabel,
                180),
            1,
            0);

        return table;
    }

    private static TableLayoutPanel CreateRoleColumns() =>
        new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 50f),
                new ColumnStyle(SizeType.Percent, 50f)
            }
        };

    private Panel CreateBackendCard(
        string title,
        out Label header,
        out Panel body)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Padding = new Padding(12),
            BorderStyle = BorderStyle.FixedSingle
        };

        header = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty,
            AutoScroll = false
        };

        card.Controls.Add(body);
        card.Controls.Add(header);
        return card;
    }

    private static Label CreateStatusLabel() =>
        new()
        {
            AutoEllipsis = true,
            AutoSize = false,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

    private void ApplyDirection()
    {
        var sqlToEmbedded = SelectedDirection == MigrationDirection.SqlServerToEmbedded;
        _sourceEndpoint.Backend = sqlToEmbedded
            ? HivePersistenceBackend.SqlServer
            : HivePersistenceBackend.Embedded;
        _destinationEndpoint.Backend = sqlToEmbedded
            ? HivePersistenceBackend.Embedded
            : HivePersistenceBackend.SqlServer;

        _sourceHeader.Text = sqlToEmbedded
            ? "SOURCE · SQL Server"
            : "SOURCE · Embedded";
        _destinationHeader.Text = sqlToEmbedded
            ? "DESTINATION · Embedded"
            : "DESTINATION · SQL Server";
    }

    private async Task DirectionChangedAsync()
    {
        ApplyDirection();

        SetStatus(
            "Migration direction changed. Both endpoints remain independently editable. Review them before refreshing readiness.",
            HiveStatusTone.Information);

        if (_busy || IsDisposed || Disposing)
            return;

        await RefreshVisibleSqlServerInstancesAsync(CancellationToken.None)
            .ConfigureAwait(true);
    }

    private MigrationDirection SelectedDirection =>
        (_directionComboBox.SelectedItem as DirectionChoice)?.Direction
        ?? MigrationDirection.SqlServerToEmbedded;

    private async Task RefreshVisibleSqlServerInstancesAsync(
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        if (_sourceEndpoint.Backend == HivePersistenceBackend.SqlServer)
        {
            tasks.Add(
                _sourceEndpoint.SqlServerPicker.RefreshAsync(
                    _sourceEndpoint.SqlServerName,
                    cancellationToken));
        }

        if (_destinationEndpoint.Backend == HivePersistenceBackend.SqlServer)
        {
            tasks.Add(
                _destinationEndpoint.SqlServerPicker.RefreshAsync(
                    _destinationEndpoint.SqlServerName,
                    cancellationToken));
        }

        if (tasks.Count != 0)
            await Task.WhenAll(tasks).ConfigureAwait(true);
    }

    private async Task RefreshSqlServerInstancesAsync(
        MigrationEndpointEditor endpoint,
        CancellationToken cancellationToken)
    {
        if (endpoint.Backend != HivePersistenceBackend.SqlServer)
            return;

        SetStatus(
            "Discovering visible SQL Server instances...",
            HiveStatusTone.Information);

        await endpoint.SqlServerPicker
            .RefreshAsync(
                endpoint.SqlServerName,
                cancellationToken)
            .ConfigureAwait(true);

        SetStatus(
            string.IsNullOrWhiteSpace(endpoint.SqlServerName)
                ? "SQL Server instance list refreshed."
                : $"SQL Server instance list refreshed. Selection: {endpoint.SqlServerName}.",
            HiveStatusTone.Success);
    }

    private async Task RefreshStatusAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await RefreshVisibleSqlServerInstancesAsync(
                cancellationToken).ConfigureAwait(true);

            var source = await BuildEndpointConfigurationAsync(
                _sourceEndpoint,
                EndpointRole.Source,
                allowPasswordCreation: false,
                cancellationToken).ConfigureAwait(true);

            var destination = await BuildEndpointConfigurationAsync(
                _destinationEndpoint,
                EndpointRole.Destination,
                allowPasswordCreation: false,
                cancellationToken).ConfigureAwait(true);

            var sourceTest = await _management
                .TestPersistenceConnectionAsync(
                    source,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (sourceTest.IsFailure)
            {
                SetStatus(
                    $"Source: {sourceTest.Error!.Message}",
                    HiveStatusTone.Warning);
                return;
            }

            var destinationTest = await _management
                .TestPersistenceConnectionAsync(
                    destination,
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (destinationTest.IsFailure)
            {
                SetStatus(
                    $"Source: ready. Destination: {destinationTest.Error!.Message}",
                    HiveStatusTone.Warning);
                return;
            }

            var tone =
                destinationTest.Value!.DatabaseState == HiveDatabaseState.DatabaseNotFound
                    ? HiveStatusTone.Information
                    : sourceTest.Value!.DatabaseState == HiveDatabaseState.Current &&
                      destinationTest.Value.DatabaseState == HiveDatabaseState.Current
                        ? HiveStatusTone.Success
                        : HiveStatusTone.Warning;

            SetStatus(
                $"Source: {FormatStatus(sourceTest.Value)} Destination: {FormatStatus(destinationTest.Value)}",
                tone);
        }
        catch (ArgumentException exception)
        {
            SetStatus(
                $"Migration configuration: {exception.Message}",
                HiveStatusTone.Warning);
        }
    }

    private async Task RunMigrationAsync(
        CancellationToken cancellationToken)
    {
        var source = await BuildEndpointConfigurationAsync(
            _sourceEndpoint,
            EndpointRole.Source,
            allowPasswordCreation: true,
            cancellationToken).ConfigureAwait(true);

        var destination = await BuildEndpointConfigurationAsync(
            _destinationEndpoint,
            EndpointRole.Destination,
            allowPasswordCreation: true,
            cancellationToken).ConfigureAwait(true);

        if (source.Backend == destination.Backend)
        {
            throw new ArgumentException(
                "Source and destination must use different persistence backends.");
        }

        var sourcePreflight = await _management
            .TestPersistenceConnectionAsync(
                source,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (sourcePreflight.IsFailure)
        {
            await CleanupTemporaryCredentialsAsync().ConfigureAwait(true);
            SetStatus(
                $"Source preflight failed: {sourcePreflight.Error!.Message}",
                HiveStatusTone.Error);
            return;
        }

        var destinationPreflight = await _management
            .TestPersistenceConnectionAsync(
                destination,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (destinationPreflight.IsFailure)
        {
            await CleanupTemporaryCredentialsAsync().ConfigureAwait(true);
            SetStatus(
                $"Destination preflight failed: {destinationPreflight.Error!.Message}",
                HiveStatusTone.Error);
            return;
        }

        SetStatus(
            "Source and destination preflight passed. Migrating All Hive Data...",
            HiveStatusTone.Information);

        var result = await _management
            .MigratePersistenceDataAsync(
                new HivePersistenceMigrationRequest(
                    source,
                    destination),
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        await CleanupTemporaryCredentialsAsync().ConfigureAwait(true);

        if (result.IsFailure)
        {
            SetStatus(
                $"Migration failed: {result.Error!.Message}",
                HiveStatusTone.Error);
            return;
        }

        var value = result.Value!;
        SetStatus(
            $"Migration complete: {value.TotalRecordsMigrated} records. Source unchanged: {value.SourceVerifiedUnchanged}. Destination verified: {value.DestinationVerified}. Destination activated: {value.DestinationActivated}.",
            HiveStatusTone.Success);

        if (IsDisposed || Disposing)
            return;

        HiveMessageBox.ShowInformation(
            FindForm(),
            $"Full-data migration completed successfully. {value.TotalRecordsMigrated} records were migrated. The destination was verified but was not activated.",
            "Hive Persistence");
    }

    private async Task<HivePersistenceConfiguration> BuildEndpointConfigurationAsync(
        MigrationEndpointEditor endpoint,
        EndpointRole role,
        bool allowPasswordCreation,
        CancellationToken cancellationToken)
    {
        if (endpoint.Backend == HivePersistenceBackend.Embedded)
        {
            if (string.IsNullOrWhiteSpace(endpoint.EmbeddedStoragePath))
                throw new ArgumentException(
                    $"{role} Embedded database file is required.");

            return HivePersistenceConfiguration.Embedded(
                endpoint.EmbeddedStoragePath,
                createDatabaseIfMissing: role == EndpointRole.Destination &&
                    endpoint.EmbeddedCreateDatabaseCheckBox?.Checked == true,
                commandTimeoutSeconds: (int)endpoint.EmbeddedTimeoutNumeric.Value);
        }

        if (string.IsNullOrWhiteSpace(endpoint.SqlServerName))
            throw new ArgumentException(
                $"{role} SQL Server is required.");

        var databaseName = string.IsNullOrWhiteSpace(endpoint.SqlDatabaseName)
            ? HivePersistenceConfiguration.BuildDatabaseName(_applicationName)
            : endpoint.SqlDatabaseName;

        HiveBootstrapCredentialReference? credential =
            role == EndpointRole.Source
                ? _createdSourceCredential
                : _createdDestinationCredential;

        if (endpoint.SqlAuthentication == HiveSqlAuthenticationMode.SqlPassword)
        {
            if (allowPasswordCreation &&
                !string.IsNullOrWhiteSpace(endpoint.SqlPassword))
            {
                using var material = SecretMaterial.Create(endpoint.SqlPassword);
                var saved = await _management
                    .SaveBootstrapCredentialAsync(
                        material,
                        existingReference: null,
                        _accessContext,
                        cancellationToken)
                    .ConfigureAwait(true);

                if (saved.IsFailure)
                    throw new InvalidOperationException(saved.Error!.Message);

                credential = saved.Value;

                if (role == EndpointRole.Source)
                    _createdSourceCredential = credential;
                else
                    _createdDestinationCredential = credential;

                endpoint.SetSqlCredentialStatus(
                    "Temporary migration credential stored securely.");
            }

            if (credential is null)
            {
                throw new ArgumentException(
                    $"{role} SQL Server Authentication requires a password.");
            }
        }

        return new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            endpoint.SqlServerName,
            endpoint.SqlPort ?? 1433,
            databaseName,
            endpoint.SqlAuthentication,
            endpoint.SqlAuthentication == HiveSqlAuthenticationMode.SqlPassword
                ? endpoint.SqlUserName
                : null,
            credential,
            endpoint.SqlEncryptCheckBox.Checked,
            endpoint.SqlTrustServerCertificateCheckBox.Checked,
            createDatabaseIfMissing: role == EndpointRole.Destination &&
                endpoint.SqlCreateDatabaseCheckBox?.Checked == true,
            commandTimeoutSeconds: (int)endpoint.SqlTimeoutNumeric.Value);
    }

    private async Task CleanupTemporaryCredentialsAsync()
    {
        var references = new[]
        {
            _createdSourceCredential,
            _createdDestinationCredential
        };

        _createdSourceCredential = null;
        _createdDestinationCredential = null;

        foreach (var reference in references)
        {
            if (reference is not { } value)
                continue;

            try
            {
                await _management
                    .RemoveBootstrapCredentialAsync(
                        value,
                        _accessContext,
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Hive Persistence",
                    "A temporary migration SQL credential could not be removed.",
                    _output,
                    _themeManager);
            }
        }
    }

    private static string FormatStatus(
        HivePersistenceConnectionTest status) =>
        $"{status.Message} State: {status.DatabaseState}; schema {status.CurrentSchemaVersion?.ToString() ?? "not initialized"} of supported {status.SupportedSchemaVersion}.";

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation) =>
        await RunOperationAsync(
            operation,
            statusPrefix: null).ConfigureAwait(true);

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation,
        string? statusPrefix)
    {
        _operationCts?.Cancel();

        var operationCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(
            ref _operationCts,
            operationCts);
        previous?.Dispose();

        if (IsDisposed || Disposing)
        {
            operationCts.Dispose();
            return;
        }

        _busy = true;
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
        catch (ArgumentException exception)
        {
            SetStatus(
                exception.Message,
                HiveStatusTone.Error);
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Hive Persistence",
                "The migration configuration is invalid.",
                _output,
                _themeManager);
        }
        catch (Exception exception)
        {
            SetStatus(
                statusPrefix ?? "Migration operation failed. See technical details.",
                HiveStatusTone.Error);
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Hive Persistence",
                statusPrefix ?? "The persistence migration could not be completed.",
                _output,
                _themeManager);
        }
        finally
        {
            if (ReferenceEquals(_operationCts, operationCts))
                Interlocked.CompareExchange(
                    ref _operationCts,
                    null,
                    operationCts);

            operationCts.Dispose();
            _busy = false;

            if (!IsDisposed && !Disposing)
                SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _migrateButton.Enabled = !busy;
        _directionComboBox.Enabled = !busy;
        _sourceEndpoint.View.Enabled = !busy;
        _destinationEndpoint.View.Enabled = !busy;
    }

    private void SetStatus(
        string text,
        HiveStatusTone tone)
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

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        _themeManager.Apply(this);
        ApplyStatusVisual();
        UpdateFooterStatusWidth();
    }

    private void FooterPanelOnResize(object? sender, EventArgs e) =>
        UpdateFooterStatusWidth();

    private void UpdateFooterStatusWidth()
    {
        var buttonWidth = _editor.FooterPanel.Controls
            .OfType<HiveButton>()
            .Sum(static button => button.Width + button.Margin.Horizontal + 8);

        _statusLabel.Width = Math.Max(
            180,
            _editor.FooterPanel.ClientSize.Width - buttonWidth - 24);
    }

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

            _sourceEndpoint.Dispose();
            _destinationEndpoint.Dispose();
        }

        base.Dispose(disposing);
    }
}