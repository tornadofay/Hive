using System.Drawing;
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
    private readonly TextBox _embeddedPathTextBox;
    private readonly TextBox _sqlServerTextBox;
    private readonly TextBox _sqlPortTextBox;
    private readonly TextBox _sqlDatabaseTextBox;
    private readonly CheckBox _sqlEncryptCheckBox;
    private readonly CheckBox _sqlTrustServerCertificateCheckBox;
    private readonly CheckBox _sqlCreateDatabaseCheckBox;
    private readonly NumericUpDown _timeoutNumeric;
    private readonly Label _sourceStatusLabel;
    private readonly Label _destinationStatusLabel;
    private readonly Label _migrationStatusLabel;
    private readonly HiveButton _refreshButton;
    private readonly HiveButton _migrateButton;

    private HiveStatusTone _sourceTone = HiveStatusTone.Neutral;
    private HiveStatusTone _destinationTone = HiveStatusTone.Neutral;
    private HiveStatusTone _migrationTone = HiveStatusTone.Neutral;
    private CancellationTokenSource? _operationCts;
    private HivePersistenceConfiguration? _sourceConfiguration;

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

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

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
        _directionComboBox.SelectedIndexChanged += (_, _) =>
            UpdateDestinationControls();

        _embeddedPathTextBox = CreateTextBox();
        _sqlServerTextBox = CreateTextBox();
        _sqlPortTextBox = CreateTextBox();
        _sqlDatabaseTextBox = CreateTextBox();
        _sqlEncryptCheckBox = new CheckBox
        {
            Text = "Encrypt SQL connection",
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
            Text = "Allow database creation during migration",
            AutoSize = true,
            Checked = true
        };
        _timeoutNumeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 600,
            Value = 30,
            DecimalPlaces = 0
        };

        _sourceStatusLabel = CreateStatusLabel();
        _destinationStatusLabel = CreateStatusLabel();
        _migrationStatusLabel = CreateStatusLabel();

        _refreshButton = _editor.AddActionButton(
            "Refresh status",
            HiveButtonStyle.Secondary,
            112);
        _migrateButton = _editor.AddActionButton(
            "Run migration",
            HiveButtonStyle.Primary,
            112);

        _refreshButton.Click += async (_, _) =>
            await RunOperationAsync(RefreshStatusAsync).ConfigureAwait(true);
        _migrateButton.Click += async (_, _) =>
            await RunOperationAsync(RunMigrationAsync).ConfigureAwait(true);

        _editor.AddField(
            "Direction",
            "Migration is always the complete All Hive Data transfer between the two persistence backends. Selecting a direction does not activate or migrate anything by itself.",
            _directionComboBox,
            84);

        _editor.AddField(
            "Embedded storage",
            "Destination path used only for SQL Server → Embedded migration. The path is application-owned and contains one Hive database file.",
            _embeddedPathTextBox,
            84);

        SetReadOnlyVisualState(
            _sqlDatabaseTextBox,
            _themeManager);

        _editor.AddField(
            "SQL Server / instance",
            "Destination host or instance used only for Embedded → SQL Server migration.",
            _sqlServerTextBox,
            84);

        _editor.AddField(
            "SQL port",
            "Optional TCP port for the SQL Server destination.",
            _sqlPortTextBox,
            72);

        _editor.AddField(
            "SQL database",
            "Hive assigns this destination database name from the application name.",
            _sqlDatabaseTextBox,
            72);

        var securityPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        securityPanel.Controls.Add(_sqlEncryptCheckBox);
        securityPanel.Controls.Add(_sqlTrustServerCertificateCheckBox);

        _editor.AddField(
            "SQL security",
            "SQL password authentication is intentionally not handled by the migration page. Configure SQL-password bootstrap credentials explicitly in Database Setup after migration.",
            securityPanel,
            88);

        _editor.AddField(
            "SQL initialization",
            "The destination must be initialized as part of the explicit migration operation. An existing Hive database is rejected by the migration boundary; this is not merge or overwrite.",
            _sqlCreateDatabaseCheckBox,
            84);

        _editor.AddField(
            "Command timeout",
            "Timeout used for destination status checks and migration operations.",
            _timeoutNumeric,
            72);

        _editor.AddField(
            "Source status",
            "Read-only status of the currently active persistence backend. Migration never changes this source.",
            _sourceStatusLabel,
            84);

        _editor.AddField(
            "Destination status",
            "Read-only destination readiness/preflight status. A successful migration never activates the destination.",
            _destinationStatusLabel,
            96);

        _editor.AddField(
            "Migration status",
            "The final result includes source immutability, destination verification, record count, and explicit non-activation evidence.",
            _migrationStatusLabel,
            96);

        var databaseName = HivePersistenceConfiguration.BuildDatabaseName(_applicationName);
        _sqlDatabaseTextBox.Text = databaseName;
        _embeddedPathTextBox.Text = DefaultEmbeddedPath();

        Controls.Add(_editor);

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);

        _directionComboBox.SelectedIndex = 0;
        UpdateDestinationControls();
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        RefreshStatusAsync(cancellationToken);

    private async Task RefreshStatusAsync(
        CancellationToken cancellationToken)
    {
        var source = await _management
            .GetPersistenceConfigurationAsync(
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (source.IsFailure)
        {
            SetStatus(
                _sourceStatusLabel,
                source.Error!.Message,
                HiveStatusTone.Error);
            return;
        }

        _sourceConfiguration = source.Value!;

        SetStatus(
            _sourceStatusLabel,
            $"Active backend: {_sourceConfiguration.Backend}. Checking readiness...",
            HiveStatusTone.Information);

        var sourceTest = await _management
            .TestPersistenceConnectionAsync(
                _sourceConfiguration,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (sourceTest.IsFailure)
        {
            SetStatus(
                _sourceStatusLabel,
                sourceTest.Error!.Message,
                HiveStatusTone.Error);
        }
        else
        {
            var value = sourceTest.Value!;
            SetStatus(
                _sourceStatusLabel,
                FormatStatus(value),
                value.DatabaseState == HiveDatabaseState.Current
                    ? HiveStatusTone.Success
                    : HiveStatusTone.Warning);
        }

        AlignDirectionWithSource();
        await RefreshDestinationStatusAsync(
            cancellationToken).ConfigureAwait(true);
    }

    private async Task RefreshDestinationStatusAsync(
        CancellationToken cancellationToken)
    {
        if (_sourceConfiguration is null)
        {
            SetStatus(
                _destinationStatusLabel,
                "Load the active persistence configuration before checking the destination.",
                HiveStatusTone.Warning);
            return;
        }

        HivePersistenceConfiguration destination;

        try
        {
            destination = BuildDestinationConfiguration();
        }
        catch (ArgumentException exception)
        {
            SetStatus(
                _destinationStatusLabel,
                exception.Message,
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
                _destinationStatusLabel,
                result.Error!.Message,
                HiveStatusTone.Error);
            return;
        }

        SetStatus(
            _destinationStatusLabel,
            FormatStatus(result.Value!),
            result.Value!.DatabaseState == HiveDatabaseState.DatabaseNotFound
                ? HiveStatusTone.Information
                : HiveStatusTone.Warning);
    }

    private async Task RunMigrationAsync(
        CancellationToken cancellationToken)
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
                    _migrationStatusLabel,
                    source.Error!.Message,
                    HiveStatusTone.Error);
                return;
            }

            _sourceConfiguration = source.Value!;
        }

        var destination = BuildDestinationConfiguration();

        if (destination.Backend == _sourceConfiguration.Backend)
        {
            throw new ArgumentException(
                "Migration destination must use the other persistence backend.");
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
                _destinationStatusLabel,
                preflight.Error!.Message,
                HiveStatusTone.Error);
            return;
        }

        SetStatus(
            _destinationStatusLabel,
            "Destination preflight completed. The migration boundary will still reject any non-empty Hive destination.",
            HiveStatusTone.Information);

        SetStatus(
            _migrationStatusLabel,
            "Quiescing the active Hive graph and migrating All Hive Data...",
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
                _migrationStatusLabel,
                result.Error!.Message,
                HiveStatusTone.Error);
            return;
        }

        var value = result.Value!;
        SetStatus(
            _migrationStatusLabel,
            $"Migration {value.MigrationId} completed. {value.TotalRecordsMigrated} records migrated. " +
            $"Source unchanged: {value.SourceVerifiedUnchanged}. " +
            $"Destination verified: {value.DestinationVerified}. " +
            $"Destination activated: {value.DestinationActivated}.",
            HiveStatusTone.Success);

        if (IsDisposed || Disposing)
            return;

        HiveMessageBox.ShowInformation(
            FindForm(),
            $"Full-data migration completed successfully. {value.TotalRecordsMigrated} records were migrated. " +
            "The destination was verified but was not activated. Select and save the destination explicitly in Database Setup.",
            "Hive Persistence");
    }

    private HivePersistenceConfiguration BuildDestinationConfiguration()
    {
        if (_sourceConfiguration is null)
        {
            throw new ArgumentException(
                "Load the active persistence configuration before preparing a migration destination.");
        }

        var direction = (_directionComboBox.SelectedItem as DirectionChoice)?.Direction
            ?? throw new ArgumentException("Choose a migration direction.");

        if (direction == MigrationDirection.SqlServerToEmbedded)
        {
            return HivePersistenceConfiguration.Embedded(
                _embeddedPathTextBox.Text,
                createDatabaseIfMissing: true,
                commandTimeoutSeconds: (int)_timeoutNumeric.Value);
        }

        if (!int.TryParse(
                _sqlPortTextBox.Text.Trim(),
                out var port))
        {
            port = 0;
        }

        int? resolvedPort = port <= 0 ? null : port;

        return new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            _sqlServerTextBox.Text,
            resolvedPort,
            HivePersistenceConfiguration.BuildDatabaseName(_applicationName),
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            _sqlEncryptCheckBox.Checked,
            _sqlTrustServerCertificateCheckBox.Checked,
            _sqlCreateDatabaseCheckBox.Checked,
            (int)_timeoutNumeric.Value);
    }

    private void AlignDirectionWithSource()
    {
        if (_sourceConfiguration?.Backend == HivePersistenceBackend.Embedded)
        {
            _directionComboBox.SelectedIndex = 1;
            return;
        }

        _directionComboBox.SelectedIndex = 0;
    }

    private void UpdateDestinationControls()
    {
        var sqlDestination =
            (_directionComboBox.SelectedItem as DirectionChoice)?.Direction ==
            MigrationDirection.EmbeddedToSqlServer;

        _embeddedPathTextBox.Enabled = !sqlDestination;
        _sqlServerTextBox.Enabled = sqlDestination;
        _sqlPortTextBox.Enabled = sqlDestination;
        _sqlEncryptCheckBox.Enabled = sqlDestination;
        _sqlTrustServerCertificateCheckBox.Enabled = sqlDestination;
        _sqlCreateDatabaseCheckBox.Enabled = sqlDestination;
        _timeoutNumeric.Enabled = true;

        if (sqlDestination &&
            string.IsNullOrWhiteSpace(_sqlServerTextBox.Text))
        {
            _sqlServerTextBox.Text = "localhost";
        }
    }

    private static string DefaultEmbeddedPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Hive",
            "hive.db");

    private static string FormatStatus(
        HivePersistenceConnectionTest status) =>
        $"{status.Message} State: {status.DatabaseState}; " +
        $"schema {status.CurrentSchemaVersion?.ToString() ?? "not initialized"} " +
        $"of supported {status.SupportedSchemaVersion}.";

    private void SetStatus(
        Label label,
        string text,
        HiveStatusTone tone)
    {
        label.Text = text;

        if (ReferenceEquals(label, _sourceStatusLabel))
            _sourceTone = tone;
        else if (ReferenceEquals(label, _destinationStatusLabel))
            _destinationTone = tone;
        else if (ReferenceEquals(label, _migrationStatusLabel))
            _migrationTone = tone;

        ApplyStatusVisuals();
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        ApplyStatusVisuals();
        UpdateDestinationControls();
    }

    private void ApplyStatusVisuals()
    {
        ApplyStatusVisual(_sourceStatusLabel, _sourceTone);
        ApplyStatusVisual(_destinationStatusLabel, _destinationTone);
        ApplyStatusVisual(_migrationStatusLabel, _migrationTone);
    }

    private void ApplyStatusVisual(
        Label label,
        HiveStatusTone tone)
    {
        label.ForeColor = tone switch
        {
            HiveStatusTone.Information => _themeManager.Theme.VisualStates.Information,
            HiveStatusTone.Success => _themeManager.Theme.VisualStates.Success,
            HiveStatusTone.Warning => _themeManager.Theme.VisualStates.Warning,
            HiveStatusTone.Error => _themeManager.Theme.VisualStates.Error,
            _ => _themeManager.Theme.Palette.MutedText
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
            SetStatus(
                _migrationStatusLabel,
                "Operation cancelled.",
                HiveStatusTone.Warning);
        }
        catch (ArgumentException exception)
        {
            SetStatus(
                _migrationStatusLabel,
                exception.Message,
                HiveStatusTone.Error);
            HiveUiErrorReporter.Report(
                FindForm(),
                exception,
                "Hive Persistence",
                "The migration destination configuration is invalid.",
                _output,
                _themeManager);
        }
        catch (Exception exception)
        {
            SetStatus(
                _migrationStatusLabel,
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
        UpdateDestinationControls();

        if (busy)
        {
            _embeddedPathTextBox.Enabled = false;
            _sqlServerTextBox.Enabled = false;
            _sqlPortTextBox.Enabled = false;
            _sqlEncryptCheckBox.Enabled = false;
            _sqlTrustServerCertificateCheckBox.Enabled = false;
            _sqlCreateDatabaseCheckBox.Enabled = false;
            _timeoutNumeric.Enabled = false;
        }
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
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
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
            operationCts?.Dispose();
        }

        base.Dispose(disposing);
    }
}
