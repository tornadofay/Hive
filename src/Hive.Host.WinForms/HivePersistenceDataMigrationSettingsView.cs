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

    private readonly Control _embeddedSourcePanel;
    private readonly Control _embeddedDestinationPanel;
    private readonly Control _sqlSourcePanel;
    private readonly Control _sqlDestinationPanel;

    private readonly TextBox _embeddedPathTextBox;
    private readonly HiveButton _browseEmbeddedButton;

    private readonly HiveSqlServerInstancePicker _sqlServerPicker;
    private readonly TextBox _sqlDatabaseTextBox;
    private readonly HiveComboBox _sqlAuthenticationComboBox;
    private readonly TextBox _sqlUserNameTextBox;
    private readonly TextBox _sqlPasswordTextBox;
    private readonly Label _sqlCredentialStatus;
    private readonly CheckBox _sqlEncryptCheckBox;
    private readonly CheckBox _sqlTrustServerCertificateCheckBox;
    private readonly CheckBox _sqlCreateDatabaseCheckBox;
    private readonly NumericUpDown _sqlTimeoutNumeric;
    private readonly CheckBox _embeddedCreateDatabaseCheckBox;
    private readonly NumericUpDown _embeddedTimeoutNumeric;
    private readonly TableLayoutPanel _sqlAuthenticationField;
    private TableLayoutPanel? _sqlCredentialField;

    private readonly HiveButton _refreshButton;
    private readonly HiveButton _migrateButton;
    private readonly Label _statusLabel;

    private HiveStatusTone _statusTone = HiveStatusTone.Neutral;
    private CancellationTokenSource? _operationCts;
    private HivePersistenceConfiguration? _sourceConfiguration;
    private HiveBootstrapCredentialReference? _createdDestinationCredential;
    private bool _destinationConfigurationInitialized;
    private bool _initializingDirection;
    private bool _directionUserOverride;

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
            DropDownStyle = ComboBoxStyle.DropDownList
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
        _directionComboBox.SelectedIndexChanged += (_, _) => DirectionChanged();

        _scopeLabel = CreateReadOnlyLabel("All Hive Data");

        _embeddedPathTextBox = CreateTextBox();
        _browseEmbeddedButton = CreateButton("Browse...", HiveButtonStyle.Secondary, 92);
        _browseEmbeddedButton.Click += (_, _) => BrowseEmbeddedStorage();

        _sqlServerPicker = new HiveSqlServerInstancePicker(_themeManager);
        _sqlServerPicker.RefreshRequested += async (_, _) =>
            await RunOperationAsync(RefreshSqlServerInstancesAsync).ConfigureAwait(true);

        _sqlDatabaseTextBox = CreateTextBox();
        _sqlDatabaseTextBox.ReadOnly = true;
        SetReadOnlyVisualState(_sqlDatabaseTextBox);

        _sqlAuthenticationComboBox = new HiveComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _sqlAuthenticationComboBox.Items.AddRange(
        [
            HiveSqlAuthenticationMode.WindowsIntegrated,
            HiveSqlAuthenticationMode.SqlPassword
        ]);
        _sqlAuthenticationComboBox.SelectedIndexChanged += (_, _) =>
            UpdateSqlAuthenticationState();

        _sqlAuthenticationField = CreateFieldBlock(
            "Authentication",
            _sqlAuthenticationComboBox,
            180);

        _sqlUserNameTextBox = CreateTextBox();
        _sqlPasswordTextBox = CreateTextBox();
        _sqlPasswordTextBox.UseSystemPasswordChar = true;
        _sqlCredentialStatus = CreateStatusLabel();

        _sqlEncryptCheckBox = new CheckBox
        {
            Text = "Encrypt",
            AutoSize = true,
            Checked = true
        };
        _sqlTrustServerCertificateCheckBox = new CheckBox
        {
            Text = "Trust server certificate",
            AutoSize = true
        };
        _sqlCreateDatabaseCheckBox = new CheckBox
        {
            Text = "Allow database creation",
            AutoSize = true,
            Checked = true
        };
        _embeddedCreateDatabaseCheckBox = new CheckBox
        {
            Text = "Allow database creation",
            AutoSize = true,
            Checked = true
        };
        _sqlTimeoutNumeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 600,
            Value = 30,
            DecimalPlaces = 0,
            Width = 82
        };
        _embeddedTimeoutNumeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 600,
            Value = 30,
            DecimalPlaces = 0,
            Width = 82
        };

        _statusLabel = CreateStatusLabel();
        _statusLabel.AutoSize = false;
        _statusLabel.Height = 36;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;

        _refreshButton = _editor.AddActionButton(
            "Refresh",
            HiveButtonStyle.Secondary,
            92);
        _migrateButton = _editor.AddActionButton(
            "Migrate All Data",
            HiveButtonStyle.Primary,
            132);
        _editor.FooterPanel.Controls.Add(_statusLabel);
        _editor.FooterPanel.Resize += FooterPanelOnResize;

        var top = CreateTopConfigurationRow();
        _roleColumns = CreateRoleColumns();

        _editor.FieldsPanel.Controls.Add(top, 0, 0);
        _editor.FieldsPanel.SetColumnSpan(top, 2);
        _editor.FieldsPanel.Controls.Add(_roleColumns, 0, 1);
        _editor.FieldsPanel.SetColumnSpan(_roleColumns, 2);
        _editor.FieldsPanel.RowStyles.Clear();
        _editor.FieldsPanel.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 74f));
        _editor.FieldsPanel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));
        _editor.FieldsPanel.RowCount = 2;

        _embeddedSourcePanel = CreateEmbeddedSourcePanel();
        _embeddedDestinationPanel = CreateEmbeddedDestinationPanel();
        _sqlSourcePanel = CreateSqlSourcePanel();
        _sqlDestinationPanel = CreateSqlDestinationPanel();

        _sourceCard = CreateBackendCard(
            "SOURCE",
            out _sourceHeader,
            out _sourceBody);
        _destinationCard = CreateBackendCard(
            "DESTINATION",
            out _destinationHeader,
            out _destinationBody);

        // Migration reads from source on the left to destination on the right.
        _sourceCard.Margin = new Padding(0, 0, 7, 0);
        _destinationCard.Margin = new Padding(7, 0, 0, 0);
        _roleColumns.Controls.Add(_sourceCard, 0, 0);
        _roleColumns.Controls.Add(_destinationCard, 1, 0);

        _refreshButton.Click += async (_, _) =>
            await RunOperationAsync(RefreshStatusAsync).ConfigureAwait(true);
        _migrateButton.Click += async (_, _) =>
            await RunOperationAsync(RunMigrationAsync).ConfigureAwait(true);

        _directionComboBox.SelectedIndex = 0;
        _embeddedPathTextBox.Text = DefaultEmbeddedPath();
        _sqlServerPicker.SetValue("localhost", 1433);
        _sqlDatabaseTextBox.Text =
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
        _sqlAuthenticationComboBox.SelectedItem =
            HiveSqlAuthenticationMode.WindowsIntegrated;

        Controls.Add(_editor);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        MinimumSize = new Size(0, 0);
        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);
        UpdateRolePanels();
        UpdateSqlAuthenticationState();
        UpdateFooterStatusWidth();
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        RefreshStatusAsync(cancellationToken);

    internal HiveComboBox DirectionSelector => _directionComboBox;

    internal Label ScopeLabel => _scopeLabel;

    internal HiveComboBox SqlAuthenticationSelector =>
        _sqlAuthenticationComboBox;

    internal Label StatusLabel => _statusLabel;

    internal FlowLayoutPanel FooterPanel => _editor.FooterPanel;

    internal TableLayoutPanel RoleColumns => _roleColumns;

    internal Panel SourceCard => _sourceCard;

    internal Panel DestinationCard => _destinationCard;

    private Control CreateTopConfigurationRow()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 12),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

        table.Controls.Add(
            CreateFieldBlock("Direction", _directionComboBox),
            0,
            0);

        var scope = CreateFieldBlock("Scope", _scopeLabel);
        scope.Margin = new Padding(14, 0, 0, 0);
        table.Controls.Add(scope, 1, 0);

        return table;
    }

    private static TableLayoutPanel CreateRoleColumns()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        return table;
    }

    private Panel CreateBackendCard(
        string title,
        out Label header,
        out Panel body)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 7, 0),
            BorderStyle = BorderStyle.FixedSingle
        };

        header = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            Margin = Padding.Empty,
            AutoScroll = false
        };

        card.Controls.Add(body);
        card.Controls.Add(header);
        return card;
    }

    private Control CreateEmbeddedSourcePanel() =>
        CreateSummaryPanel(
            "Embedded",
            "Application-owned Hive database. The source is read-only during migration.",
            ("Storage location", DefaultEmbeddedPath()),
            ("Role", "Read-only source"));

    private Control CreateSqlSourcePanel() =>
        CreateSummaryPanel(
            "SQL Server",
            "Current active Hive database. The source is read-only during migration.",
            ("Server / port", "—"),
            ("Database", HivePersistenceConfiguration.BuildDatabaseName(_applicationName)),
            ("Authentication", "—"),
            ("SQL user", "—"),
            ("Role", "Read-only source"));

    private Control CreateSummaryPanel(
        string backend,
        string subtitle,
        params (string Title, string Value)[] fields)
    {
        var stack = CreateVerticalStack();
        stack.Padding = new Padding(2);

        var heading = new Label
        {
            Text = backend,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Font = new Font(
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2)
        };
        stack.Controls.Add(heading);

        var copy = CreateSectionDescription(subtitle);
        copy.Margin = new Padding(0, 0, 0, 12);
        stack.Controls.Add(copy);

        foreach (var field in fields)
            stack.Controls.Add(CreateSummaryField(field.Title, field.Value));

        return stack;
    }

    private static TableLayoutPanel CreateSummaryField(
        string title,
        string value)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        row.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font(
                    SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                    FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Margin = Padding.Empty
            },
            0,
            0);

        row.Controls.Add(
            new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft,
                Margin = Padding.Empty
            },
            1,
            0);

        return row;
    }

    private Control CreateEmbeddedDestinationPanel()
    {
        var stack = CreateDestinationStack();

        stack.Controls.Add(
            CreateFieldBlock(
                "Database file",
                CreateEmbeddedPathPanelCore()));

        stack.Controls.Add(
            CreateFormGrid(
                CreateCheckBoxBlock(
                    "Initialization",
                    _embeddedCreateDatabaseCheckBox),
                CreateFieldBlock(
                    "Command timeout",
                    _embeddedTimeoutNumeric)));

        stack.Controls.Add(
            CreateSectionNote(
                "Hive initializes the destination only when the explicit migration operation runs. An existing non-empty destination is rejected."));

        return stack;
    }

    private Control CreateSqlDestinationPanel()
    {
        var stack = CreateDestinationStack();

        stack.Controls.Add(
            CreateFieldBlock(
                "SQL Server",
                _sqlServerPicker,
                350));

        stack.Controls.Add(
            CreateFormGrid(
                CreateFieldBlock("Database", _sqlDatabaseTextBox, 180),
                _sqlAuthenticationField));

        _sqlCredentialField = CreateCredentialField();
        stack.Controls.Add(_sqlCredentialField);

        stack.Controls.Add(
            CreateFormGrid(
                CreateFieldBlock(
                    "Connection security",
                    CreateSecurityPanelCore(),
                    180),
                CreateFieldBlock(
                    "Command timeout",
                    _sqlTimeoutNumeric,
                    140)));

        stack.Controls.Add(
            CreateCheckBoxBlock(
                "Initialization",
                _sqlCreateDatabaseCheckBox));

        stack.Controls.Add(
            CreateSectionNote(
                "All Hive Data is migrated as one governed operation. The destination must be clean/current and is never activated automatically."));

        return stack;
    }

    private static TableLayoutPanel CreateDestinationStack()
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

    private TableLayoutPanel CreateCredentialField()
    {
        var credentials = CreateFormGrid(
            CreateFieldBlock("SQL user", _sqlUserNameTextBox),
            CreateFieldBlock("Password", _sqlPasswordTextBox));

        var wrapper = CreateVerticalStack();
        wrapper.Controls.Add(
            CreateSectionHeading("SQL credentials"));
        wrapper.Controls.Add(
            CreateSectionDescription(
                "Required only for SQL Server Authentication. The password is stored through the protected bootstrap-credential boundary."));
        wrapper.Controls.Add(credentials);
        wrapper.Controls.Add(_sqlCredentialStatus);

        _sqlCredentialStatus.Margin = new Padding(0, 6, 0, 0);
        return wrapper;
    }

    private static TableLayoutPanel CreateFieldBlock(
        string title,
        Control control,
        int? editorWidth = null)
    {
        ArgumentNullException.ThrowIfNull(control);

        var block = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10),
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        block.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        block.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 22,
                Font = new Font(
                    SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                    FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            },
            0,
            0);

        control.AutoSize = false;
        control.Dock = editorWidth is > 0
            ? DockStyle.Left
            : DockStyle.Fill;
        control.Margin = Padding.Empty;

        if (editorWidth is > 0)
            control.Width = editorWidth.Value;

        if (control.Height < 36)
            control.Height = 36;

        block.Controls.Add(control, 0, 1);
        return block;
    }

    private static TableLayoutPanel CreateCheckBoxBlock(
        string title,
        CheckBox checkBox)
    {
        var block = CreateVerticalStack();
        block.Margin = new Padding(0, 0, 0, 10);
        block.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 22,
                Font = new Font(
                    SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                    FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            });

        checkBox.AutoSize = true;
        checkBox.Anchor = AnchorStyles.Left;
        checkBox.Margin = new Padding(0, 5, 0, 0);
        block.Controls.Add(checkBox);
        return block;
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
            Margin = new Padding(0, 0, 0, 10),
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

    private Control CreateEmbeddedPathPanelCore()
    {
        var path = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        path.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        path.Controls.Add(_embeddedPathTextBox, 0, 0);
        path.Controls.Add(_browseEmbeddedButton, 1, 0);
        return path;
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
            Margin = new Padding(0, 0, 0, 4),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Label CreateSectionDescription(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = true,
            MaximumSize = new Size(0, 48),
            Margin = new Padding(0, 0, 0, 10)
        };

    private static Label CreateSectionNote(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = true,
            MaximumSize = new Size(0, 48),
            Margin = new Padding(0, 0, 0, 10)
        };

    private Control CreateSecurityPanelCore()
    {
        var host = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        host.Controls.Add(_sqlEncryptCheckBox);
        host.Controls.Add(_sqlTrustServerCertificateCheckBox);
        return host;
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

    private void DirectionChanged()
    {
        if (!_initializingDirection)
            _directionUserOverride = true;

        UpdateRolePanels();

        if (SelectedDirection == MigrationDirection.EmbeddedToSqlServer &&
            !_initializingDirection &&
            !IsDisposed &&
            !Disposing)
        {
            _ = RunOperationAsync(RefreshSqlServerInstancesAsync);
        }

        if (!_destinationConfigurationInitialized)
        {
            SetStatus(
                "Migration direction changed. Review the destination configuration, then refresh readiness.",
                HiveStatusTone.Information);
            return;
        }

        SetStatus(
            "Migration direction changed. Review the destination configuration, then refresh readiness.",
            HiveStatusTone.Information);
    }

    private void UpdateRolePanels()
    {
        var sourceSql = SelectedDirection == MigrationDirection.SqlServerToEmbedded;
        var selectedSourceBackend = sourceSql
            ? HivePersistenceBackend.SqlServer
            : HivePersistenceBackend.Embedded;
        var sourceIsActive = _sourceConfiguration is null ||
            _sourceConfiguration.Backend == selectedSourceBackend;

        _sourceHeader.Text =
            $"SOURCE · {(sourceSql ? "SQL Server" : "Embedded")}" +
            (sourceIsActive ? string.Empty : " · INACTIVE");
        _destinationHeader.Text = sourceSql
            ? "DESTINATION · Embedded"
            : "DESTINATION · SQL Server";

        ReplaceBody(_sourceBody, sourceSql ? _sqlSourcePanel : _embeddedSourcePanel);
        ReplaceBody(_destinationBody, sourceSql ? _embeddedDestinationPanel : _sqlDestinationPanel);

        _destinationCard.Enabled = true;
        _sourceCard.Enabled = true;
        UpdateSqlAuthenticationState();
    }

    private static void ReplaceBody(Panel host, Control content)
    {
        if (ReferenceEquals(
            host.Controls.Count == 0 ? null : host.Controls[0],
            content))
        {
            return;
        }

        host.Controls.Clear();
        content.Dock = DockStyle.Fill;
        host.Controls.Add(content);
    }

    private MigrationDirection SelectedDirection =>
        (_directionComboBox.SelectedItem as DirectionChoice)?.Direction
        ?? MigrationDirection.SqlServerToEmbedded;

    private HivePersistenceBackend SelectedSourceBackend =>
        SelectedDirection == MigrationDirection.SqlServerToEmbedded
            ? HivePersistenceBackend.SqlServer
            : HivePersistenceBackend.Embedded;

    private async Task RefreshStatusAsync(CancellationToken cancellationToken)
    {
        var source = await _management
            .GetPersistenceConfigurationAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (source.IsFailure)
        {
            SetStatus(
                $"Source: {source.Error!.Message}",
                HiveStatusTone.Error);
            return;
        }

        _sourceConfiguration = source.Value!;

        if (!_directionUserOverride)
        {
            var expectedDirection =
                _sourceConfiguration.Backend == HivePersistenceBackend.Embedded
                    ? MigrationDirection.EmbeddedToSqlServer
                    : MigrationDirection.SqlServerToEmbedded;

            _initializingDirection = true;
            try
            {
                _directionComboBox.SelectedItem =
                    expectedDirection == MigrationDirection.EmbeddedToSqlServer
                        ? new DirectionChoice(
                            "Embedded → SQL Server",
                            expectedDirection)
                        : new DirectionChoice(
                            "SQL Server → Embedded",
                            expectedDirection);
            }
            finally
            {
                _initializingDirection = false;
            }
        }

        UpdateSourceSummary();
        await RefreshSqlServerInstancesIfNeededAsync(cancellationToken)
            .ConfigureAwait(true);

        var sourceTest = await _management
            .TestPersistenceConnectionAsync(
                _sourceConfiguration,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (sourceTest.IsFailure)
        {
            SetStatus(
                $"Source: {sourceTest.Error!.Message}",
                HiveStatusTone.Error);
            return;
        }

        var sourceState = FormatStatus(sourceTest.Value!);
        var tone = sourceTest.Value!.DatabaseState == HiveDatabaseState.Current
            ? HiveStatusTone.Success
            : HiveStatusTone.Warning;

        SetStatus(
            $"Source: {sourceState}",
            tone);

        _destinationConfigurationInitialized = true;
        await RefreshDestinationStatusAsync(cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task RefreshDestinationStatusAsync(
        CancellationToken cancellationToken)
    {
        if (_sourceConfiguration is null)
        {
            SetStatus(
                "Source configuration is not loaded.",
                HiveStatusTone.Warning);
            return;
        }

        if (SelectedSourceBackend != _sourceConfiguration.Backend)
        {
            SetStatus(
                $"Selected source is {SelectedSourceBackend}, but the active Hive backend is {_sourceConfiguration.Backend}. The destination remains editable; choose the direction that matches the active backend before refreshing readiness or running migration.",
                HiveStatusTone.Warning);
            return;
        }

        HivePersistenceConfiguration destination;
        try
        {
            destination = await BuildDestinationConfigurationAsync(
                cancellationToken,
                allowPasswordCreation: false).ConfigureAwait(true);
        }
        catch (ArgumentException exception)
        {
            SetStatus(
                $"Source ready. Destination: {exception.Message}",
                HiveStatusTone.Warning);
            return;
        }

        var result = await _management
            .TestPersistenceConnectionAsync(
                destination,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            SetStatus(
                $"Source ready. Destination: {result.Error!.Message}",
                HiveStatusTone.Error);
            return;
        }

        var tone = result.Value!.DatabaseState == HiveDatabaseState.DatabaseNotFound
            ? HiveStatusTone.Information
            : result.Value.DatabaseState == HiveDatabaseState.Current
                ? HiveStatusTone.Warning
                : HiveStatusTone.Warning;

        SetStatus(
            $"Source: ready. Destination: {FormatStatus(result.Value)}",
            tone);
    }

    private async Task RunMigrationAsync(CancellationToken cancellationToken)
    {
        if (_sourceConfiguration is null)
        {
            var source = await _management
                .GetPersistenceConfigurationAsync(
                    _accessContext,
                    cancellationToken)
                .ConfigureAwait(true);

            if (source.IsFailure)
            {
                SetStatus(
                    $"Source: {source.Error!.Message}",
                    HiveStatusTone.Error);
                return;
            }

            _sourceConfiguration = source.Value!;
        }

        var expectedSourceBackend =
            SelectedDirection == MigrationDirection.SqlServerToEmbedded
                ? HivePersistenceBackend.SqlServer
                : HivePersistenceBackend.Embedded;

        if (expectedSourceBackend != _sourceConfiguration.Backend)
        {
            throw new ArgumentException(
                $"The selected source is {expectedSourceBackend}, but the currently active Hive backend is {_sourceConfiguration.Backend}. Select the direction that matches the active backend before running migration.");
        }

        var destination = await BuildDestinationConfigurationAsync(
            cancellationToken,
            allowPasswordCreation: true).ConfigureAwait(true);

        if (destination.Backend == _sourceConfiguration.Backend)
        {
            throw new ArgumentException(
                "Migration requires the other persistence backend as its destination.");
        }

        var preflight = await _management
            .TestPersistenceConnectionAsync(
                destination,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (preflight.IsFailure)
        {
            SetStatus(
                $"Destination: {preflight.Error!.Message}",
                HiveStatusTone.Error);
            await CleanupCreatedCredentialAfterFailedMigrationAsync().ConfigureAwait(true);
            return;
        }

        SetStatus(
            "Source: ready. Destination: preflight passed. Migrating All Hive Data...",
            HiveStatusTone.Information);

        var result = await _management
            .MigratePersistenceDataAsync(
                new HivePersistenceMigrationRequest(destination),
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            SetStatus(
                $"Migration failed: {result.Error!.Message}",
                HiveStatusTone.Error);
            await CleanupCreatedCredentialAfterFailedMigrationAsync().ConfigureAwait(true);
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

    private async Task<HivePersistenceConfiguration> BuildDestinationConfigurationAsync(
        CancellationToken cancellationToken,
        bool allowPasswordCreation)
    {
        if (_sourceConfiguration is null)
        {
            throw new ArgumentException(
                "Load the active persistence configuration before preparing a migration destination.");
        }

        if (SelectedDirection == MigrationDirection.SqlServerToEmbedded)
        {
            return HivePersistenceConfiguration.Embedded(
                _embeddedPathTextBox.Text,
                createDatabaseIfMissing: _embeddedCreateDatabaseCheckBox.Checked,
                commandTimeoutSeconds: (int)_embeddedTimeoutNumeric.Value);
        }

        var authentication =
            _sqlAuthenticationComboBox.SelectedItem is HiveSqlAuthenticationMode value
                ? value
                : HiveSqlAuthenticationMode.WindowsIntegrated;

        HiveBootstrapCredentialReference? credential = null;

        if (authentication == HiveSqlAuthenticationMode.SqlPassword)
        {
            if (allowPasswordCreation &&
                !string.IsNullOrWhiteSpace(_sqlPasswordTextBox.Text))
            {
                using var material = SecretMaterial.Create(_sqlPasswordTextBox.Text);
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
                _createdDestinationCredential = credential;
                _sqlPasswordTextBox.Clear();
                _sqlCredentialStatus.Text =
                    "Destination credential stored securely for this migration.";
            }
            else if (_createdDestinationCredential is not null)
            {
                credential = _createdDestinationCredential;
            }
            else if (_sourceConfiguration.AuthenticationMode == HiveSqlAuthenticationMode.SqlPassword &&
                     _sourceConfiguration.BootstrapCredential is not null)
            {
                credential = _sourceConfiguration.BootstrapCredential;
                _sqlCredentialStatus.Text =
                    "Using the currently configured protected SQL credential.";
            }
            else
            {
                throw new ArgumentException(
                    "SQL Server Authentication requires a password. Enter it and run migration, or configure a protected SQL credential in Database Setup first.");
            }
        }

        var port = _sqlServerPicker.Port;

        return new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            _sqlServerPicker.ServerName,
            port,
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
            authentication,
            authentication == HiveSqlAuthenticationMode.SqlPassword
                ? _sqlUserNameTextBox.Text
                : null,
            credential,
            _sqlEncryptCheckBox.Checked,
            _sqlTrustServerCertificateCheckBox.Checked,
            _sqlCreateDatabaseCheckBox.Checked,
            (int)_sqlTimeoutNumeric.Value);
    }

    private void UpdateSourceSummary()
    {
        if (_sourceConfiguration is null)
            return;

        if (_sourceConfiguration.Backend == HivePersistenceBackend.Embedded)
        {
            SetSummaryValues(
                _embeddedSourcePanel,
                ("Storage location", _sourceConfiguration.EmbeddedStoragePath ?? DefaultEmbeddedPath()),
                ("Mode", "Read-only source"));
        }
        else
        {
            var authentication = _sourceConfiguration.AuthenticationMode.ToString();
            SetSummaryValues(
                _sqlSourcePanel,
                ("Server / port", FormatServerPort(
                    _sourceConfiguration.ServerName,
                    _sourceConfiguration.Port)),
                ("Database", string.IsNullOrWhiteSpace(_sourceConfiguration.DatabaseName)
                    ? HivePersistenceConfiguration.BuildDatabaseName(_applicationName)
                    : _sourceConfiguration.DatabaseName),
                ("Authentication", authentication),
                ("SQL user", _sourceConfiguration.UserName ?? "Windows identity"),
                ("Mode", "Read-only source"));
        }

        if (SelectedSourceBackend == _sourceConfiguration.Backend)
            return;

        if (SelectedSourceBackend == HivePersistenceBackend.Embedded)
        {
            SetSummaryValues(
                _embeddedSourcePanel,
                ("Storage location", "Not active"),
                ("Mode", "Inactive source — choose the other direction."));
        }
        else
        {
            SetSummaryValues(
                _sqlSourcePanel,
                ("Server / port", "Not active"),
                ("Database", "Not active"),
                ("Authentication", "Not active"),
                ("SQL user", "Not active"),
                ("Mode", "Inactive source — choose the other direction."));
        }
    }

    private static void SetSummaryValues(
        Control root,
        params (string Title, string Value)[] values)
    {
        if (root is not TableLayoutPanel table)
            return;

        var titleLabels = table.Controls
            .OfType<Label>()
            .Where(static label => label.Font.Bold)
            .ToDictionary(
                static label => label.Text,
                StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (!titleLabels.TryGetValue(value.Title, out var titleLabel))
                continue;

            var row = table.GetPositionFromControl(titleLabel).Row;
            if (table.GetControlFromPosition(1, row) is Label valueLabel)
                valueLabel.Text = value.Value;
        }
    }

    private async Task RefreshSqlServerInstancesIfNeededAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedDirection != MigrationDirection.EmbeddedToSqlServer)
            return;

        await _sqlServerPicker
            .RefreshAsync(
                _sqlServerPicker.ServerName,
                cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task RefreshSqlServerInstancesAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedDirection != MigrationDirection.EmbeddedToSqlServer)
            return;

        var preferred = _sqlServerPicker.ServerName;
        SetStatus(
            "Source: ready. Discovering visible SQL Server instances...",
            HiveStatusTone.Information);

        await _sqlServerPicker
            .RefreshAsync(preferred, cancellationToken)
            .ConfigureAwait(true);

        SetStatus(
            string.IsNullOrWhiteSpace(preferred)
                ? "Source: ready. SQL Server instance list refreshed."
                : $"Source: ready. SQL Server instance list refreshed. Selection: {preferred}.",
            HiveStatusTone.Success);
    }

    private void UpdateSqlAuthenticationState()
    {
        var active = SelectedDirection == MigrationDirection.EmbeddedToSqlServer;
        var sqlPassword =
            active &&
            _sqlAuthenticationComboBox.SelectedItem is HiveSqlAuthenticationMode.SqlPassword;

        _sqlAuthenticationField.Visible = active;

        if (_sqlCredentialField is not null)
            _sqlCredentialField.Visible = sqlPassword;

        _sqlUserNameTextBox.Visible = sqlPassword;
        _sqlPasswordTextBox.Visible = sqlPassword;
        _sqlCredentialStatus.Visible = sqlPassword;

        if (!sqlPassword && active)
        {
            _sqlUserNameTextBox.Clear();
            _sqlPasswordTextBox.Clear();
            _sqlCredentialStatus.Text =
                "Windows integrated authentication uses the current Windows identity.";
        }
        else if (sqlPassword && _createdDestinationCredential is not null)
        {
            _sqlCredentialStatus.Text =
                "Destination credential stored securely for this migration.";
        }
        else if (sqlPassword)
        {
            _sqlCredentialStatus.Text =
                "Enter a password for the destination SQL login.";
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
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private string DefaultEmbeddedPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Hive",
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
            "hive.db");

    private static string FormatServerPort(string server, int? port) =>
        port is > 0
            ? $"{server}, {port.Value}"
            : server;

    private static string FormatStatus(
        HivePersistenceConnectionTest status) =>
        $"{status.Message} State: {status.DatabaseState}; schema {status.CurrentSchemaVersion?.ToString() ?? "not initialized"} of supported {status.SupportedSchemaVersion}.";

    private async Task CleanupCreatedCredentialAfterFailedMigrationAsync()
    {
        if (_createdDestinationCredential is not { } reference)
            return;

        _createdDestinationCredential = null;

        try
        {
            await _management
                .RemoveBootstrapCredentialAsync(
                    reference,
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
                "The temporary destination SQL credential could not be removed after migration failure.",
                _output,
                _themeManager);
        }
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

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation)
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
                "Migration operation failed. See technical details.",
                HiveStatusTone.Error);
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Hive Persistence",
                "The persistence migration could not be completed.",
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

            if (!IsDisposed && !Disposing)
                SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _migrateButton.Enabled = !busy;
        _directionComboBox.Enabled = !busy;
        _destinationCard.Enabled = !busy;
        if (!busy)
            UpdateRolePanels();
    }

    private static HiveButton CreateButton(
        string text,
        HiveButtonStyle style,
        int width) =>
        new()
        {
            Text = text,
            Style = style,
            Width = width,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0)
        };

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 36,
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty
        };

    private static Label CreateReadOnlyLabel(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

    private static Label CreateStatusLabel() =>
        new()
        {
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

    private void SetReadOnlyVisualState(TextBox textBox)
    {
        textBox.ReadOnly = true;
        textBox.TabStop = false;
        textBox.Cursor = Cursors.Arrow;
        textBox.BackColor = _themeManager.Theme.Palette.ElevatedSurface;
        textBox.ForeColor = _themeManager.Theme.Palette.MutedText;
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
        }

        base.Dispose(disposing);
    }
}
