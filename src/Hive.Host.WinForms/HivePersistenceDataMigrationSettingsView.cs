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

    private readonly HiveButton _refreshButton;
    private readonly HiveButton _migrateButton;
    private readonly Label _statusLabel;

    private HiveStatusTone _statusTone = HiveStatusTone.Neutral;
    private CancellationTokenSource? _operationCts;
    private HivePersistenceConfiguration? _sourceConfiguration;
    private HiveBootstrapCredentialReference? _createdDestinationCredential;
    private bool _destinationConfigurationInitialized;

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

        _editor = new HiveEditorLayout
        {
            LabelColumnWidth = 0
        };

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
        _sqlTimeoutNumeric = new NumericUpDown
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
        var roleColumns = CreateRoleColumns();

        _editor.FieldsPanel.Controls.Add(top, 0, 0);
        _editor.FieldsPanel.Controls.Add(roleColumns, 0, 1);
        _editor.FieldsPanel.ColumnStyles.Clear();
        _editor.FieldsPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));
        _editor.FieldsPanel.ColumnCount = 1;
        _editor.FieldsPanel.RowStyles.Clear();
        _editor.FieldsPanel.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 66f));
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

        roleColumns.Controls.Add(_sourceCard, 0, 0);
        roleColumns.Controls.Add(_destinationCard, 1, 0);

        _refreshButton.Click += async (_, _) =>
            await RunOperationAsync(RefreshStatusAsync).ConfigureAwait(true);
        _migrateButton.Click += async (_, _) =>
            await RunOperationAsync(RunMigrationAsync).ConfigureAwait(true);

        _directionComboBox.SelectedIndex = 0;
        _embeddedPathTextBox.Text = DefaultEmbeddedPath();
        _sqlServerPicker.SetValue("localhost", null);
        _sqlDatabaseTextBox.Text =
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
        _sqlAuthenticationComboBox.SelectedItem =
            HiveSqlAuthenticationMode.WindowsIntegrated;

        Controls.Add(_editor);
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

    private Control CreateTopConfigurationRow()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 8),
            Padding = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152f));

        table.Controls.Add(
            new Label
            {
                Text = "Direction",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 8, 0)
            },
            0,
            0);
        table.Controls.Add(_directionComboBox, 1, 0);

        var scope = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(18, 0, 0, 0),
            Padding = Padding.Empty
        };
        scope.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52f));
        scope.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        scope.Controls.Add(
            new Label
            {
                Text = "Scope",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 8, 0)
            },
            0,
            0);
        scope.Controls.Add(_scopeLabel, 1, 0);

        table.Controls.Add(scope, 2, 0);
        return table;
    }

    private static TableLayoutPanel CreateRoleColumns()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        return table;
    }

    private static Panel CreateBackendCard(
        string title,
        out Label header,
        out Panel body)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(6),
            BorderStyle = BorderStyle.FixedSingle
        };

        header = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font(
                SystemFonts.MessageBoxFont,
                FontStyle.Bold),
            Margin = Padding.Empty
        };

        body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            Margin = Padding.Empty
        };

        card.Controls.Add(body);
        card.Controls.Add(header);
        return card;
    }

    private Control CreateEmbeddedSourcePanel() =>
        CreateSummaryPanel(
            "Embedded",
            "Application-owned Hive database.",
            ("Storage location", DefaultEmbeddedPath()),
            ("Mode", "Read-only source"));

    private Control CreateSqlSourcePanel() =>
        CreateSummaryPanel(
            "SQL Server",
            "Current active persistence backend.",
            ("Server / port", "—"),
            ("Database", HivePersistenceConfiguration.BuildDatabaseName(_applicationName)),
            ("Authentication", "—"),
            ("Mode", "Read-only source"));

    private static Control CreateSummaryPanel(
        string backend,
        string subtitle,
        params (string Title, string Value)[] fields)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = fields.Length + 2,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        panel.Controls.Add(
            new Label
            {
                Text = backend,
                Dock = DockStyle.Fill,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6)
            },
            0,
            0);
        panel.SetColumnSpan(panel.Controls[^1], 2);
        panel.Controls.Add(
            new Label
            {
                Text = subtitle,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 12)
            },
            0,
            1);
        panel.SetColumnSpan(panel.Controls[^1], 2);

        for (var i = 0; i < fields.Length; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            panel.Controls.Add(
                new Label
                {
                    Text = fields[i].Title,
                    Dock = DockStyle.Fill,
                    Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                },
                0,
                i + 2);
            panel.Controls.Add(
                new Label
                {
                    Text = fields[i].Value,
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                },
                1,
                i + 2);
        }

        panel.RowStyles[0] = new RowStyle(SizeType.Absolute, 26f);
        panel.RowStyles[1] = new RowStyle(SizeType.Absolute, 34f);
        return panel;
    }

    private Control CreateEmbeddedDestinationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        panel.Controls.Add(CreateSectionDescription(
            "Embedded destination",
            "The destination is verified before migration and is never activated automatically."), 0, 0);

        var path = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        path.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        path.Controls.Add(_embeddedPathTextBox, 0, 0);
        path.Controls.Add(_browseEmbeddedButton, 1, 0);
        panel.Controls.Add(path, 0, 1);

        panel.Controls.Add(
            CreateSecurityTimeoutPanel(
                includeSecurity: false,
                _sqlCreateDatabaseCheckBox,
                secondarySecurityControl: null,
                _sqlTimeoutNumeric,
                "Create/initialize"),
            0,
            2);

        panel.Controls.Add(
            new Label
            {
                Text = "Schema and data migration are controlled by the Management migration boundary.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 8, 0, 0)
            },
            0,
            3);

        return panel;
    }

    private Control CreateSqlDestinationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        panel.Controls.Add(
            CreateSectionDescription(
                "SQL Server destination",
                "The destination SQL Server is initialized only by the explicit migration operation."),
            0,
            0);
        panel.Controls.Add(
            CreateLabeledField(
                "Server / port",
                _sqlServerPicker),
            0,
            1);
        panel.Controls.Add(
            CreateLabeledField(
                "Database",
                _sqlDatabaseTextBox),
            0,
            2);
        panel.Controls.Add(
            CreateCredentialField(),
            0,
            3);
        panel.Controls.Add(
            CreateLabeledField(
                "Security / timeout",
                CreateSecurityTimeoutPanel(
                    includeSecurity: true,
                    _sqlEncryptCheckBox,
                    _sqlTrustServerCertificateCheckBox,
                    _sqlTimeoutNumeric,
                    null)),
            0,
            4);
        panel.Controls.Add(
            CreateLabeledField(
                "Initialization",
                _sqlCreateDatabaseCheckBox),
            0,
            5);
        panel.Controls.Add(
            new Label
            {
                Text = "All Hive Data is migrated as one governed operation. Existing non-empty destinations are rejected.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 8, 0, 0)
            },
            0,
            6);

        return panel;
    }

    private Control CreateCredentialField()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        outer.Controls.Add(
            new Label
            {
                Text = "Authentication",
                Dock = DockStyle.Fill,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            },
            0,
            0);
        outer.Controls.Add(_sqlAuthenticationComboBox, 1, 0);

        var credentialRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 0),
            Padding = Padding.Empty
        };
        credentialRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        credentialRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        credentialRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
        credentialRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
        credentialRow.Controls.Add(_sqlUserNameTextBox, 0, 0);
        credentialRow.Controls.Add(_sqlPasswordTextBox, 1, 0);
        credentialRow.Controls.Add(_sqlCredentialStatus, 0, 1);
        credentialRow.SetColumnSpan(_sqlCredentialStatus, 2);

        outer.Controls.Add(
            new Label
            {
                Text = "SQL credentials",
                Dock = DockStyle.Top,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                Margin = new Padding(0, 7, 0, 0)
            },
            0,
            1);
        outer.Controls.Add(credentialRow, 1, 1);
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

        return outer;
    }

    private static Control CreateLabeledField(
        string title,
        Control control)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        host.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            },
            0,
            0);
        host.Controls.Add(control, 1, 0);
        return host;
    }

    private static Control CreateSecurityTimeoutPanel(
        bool includeSecurity,
        CheckBox firstSecurityControl,
        CheckBox? secondarySecurityControl,
        NumericUpDown timeout,
        string? secondaryCaption)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = includeSecurity ? 3 : 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        if (includeSecurity)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
            panel.Controls.Add(firstSecurityControl, 0, 0);
            panel.Controls.Add(
                secondarySecurityControl
                    ?? throw new InvalidOperationException(
                        "A secondary SQL security control is required."),
                1,
                0);
            panel.Controls.Add(CreateTimeoutHost(timeout), 2, 0);
        }
        else
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            panel.Controls.Add(firstSecurityControl, 0, 0);
            panel.Controls.Add(CreateTimeoutHost(timeout), 1, 0);
        }

        if (!string.IsNullOrWhiteSpace(secondaryCaption))
            firstSecurityControl.Text = secondaryCaption;

        return panel;
    }

    private static Control CreateTimeoutHost(NumericUpDown timeout)
    {
        var host = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        host.Controls.Add(
            new Label
            {
                Text = "Timeout (s)",
                AutoSize = true,
                Margin = new Padding(0, 7, 6, 0)
            });
        host.Controls.Add(timeout);
        return host;
    }

    private static Label CreateSectionDescription(
        string title,
        string description)
    {
        return new Label
        {
            Text = $"{title} — {description}",
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };
    }

    private void DirectionChanged()
    {
        UpdateRolePanels();

        if (!_destinationConfigurationInitialized)
        {
            SetStatus(
                "Migration direction changed. Review the destination configuration, then refresh readiness.",
                HiveStatusTone.Information);
            return;
        }

        SetStatus(
            "Migration direction changed. Refresh readiness before running migration.",
            HiveStatusTone.Information);
    }

    private void UpdateRolePanels()
    {
        var direction = SelectedDirection;
        var sourceSql = direction == MigrationDirection.EmbeddedToSqlServer;
        _sourceHeader.Text = sourceSql
            ? "SOURCE · Embedded"
            : "SOURCE · SQL Server";
        _destinationHeader.Text = sourceSql
            ? "DESTINATION · SQL Server"
            : "DESTINATION · Embedded";

        ReplaceBody(_sourceBody, sourceSql ? _embeddedSourcePanel : _sqlSourcePanel);
        ReplaceBody(_destinationBody, sourceSql ? _sqlDestinationPanel : _embeddedDestinationPanel);

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
        var expectedDirection =
            _sourceConfiguration.Backend == HivePersistenceBackend.Embedded
                ? MigrationDirection.EmbeddedToSqlServer
                : MigrationDirection.SqlServerToEmbedded;
        if (SelectedDirection != expectedDirection)
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
                createDatabaseIfMissing: true,
                commandTimeoutSeconds: (int)_sqlTimeoutNumeric.Value);
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
            return;
        }

        var authentication = _sourceConfiguration.AuthenticationMode.ToString();
        SetSummaryValues(
            _sqlSourcePanel,
            ("Server / port", FormatServerPort(
                _sourceConfiguration.ServerName,
                _sourceConfiguration.Port)),
            ("Database", _sourceConfiguration.DatabaseName),
            ("Authentication", authentication),
            ("SQL user", _sourceConfiguration.UserName ?? "Windows identity"),
            ("Mode", "Read-only source"));
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

        _sqlAuthenticationComboBox.Visible = active;
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
            Height = 32,
            Margin = new Padding(8, 0, 0, 0)
        };

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
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
