using System.Reflection;
using DbUp;
using DbUp.Engine.Output;
using Hive.Core;

namespace Hive.Persistence;

public sealed class HiveDatabaseMigrator
{
    private readonly HiveDatabaseOptions _options;
    private readonly HiveDatabaseSchemaVersionStore _schemaVersionStore;
    private readonly Assembly _migrationAssembly;

    public HiveDatabaseMigrator(HiveDatabaseOptions options)
        : this(options, typeof(HiveDatabaseMigrator).Assembly)
    {
    }

    internal HiveDatabaseMigrator(
        HiveDatabaseOptions options,
        Assembly migrationAssembly,
        HiveDatabaseSchemaVersionStore? schemaVersionStore = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _migrationAssembly = migrationAssembly ?? throw new ArgumentNullException(nameof(migrationAssembly));
        _schemaVersionStore = schemaVersionStore ?? new HiveDatabaseSchemaVersionStore();
    }

    public async Task<Result<HiveDatabaseMigrationOutcome>> MigrateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (_options.CreateDatabaseIfMissing)
            {
                EnsureDatabase.For.SqlDatabase(
                    _options.ConnectionString,
                    new NoOpUpgradeLog());
            }

            var previousVersion =
                await _schemaVersionStore
                    .GetVersionAsync(_options, cancellationToken)
                    .ConfigureAwait(false);

            if (previousVersion is > HiveDatabaseSchema.CurrentSchemaVersion)
            {
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    Error.Unsupported(
                        "hive.persistence.future-schema",
                        $"The Hive database schema version {previousVersion.Value} is newer than the supported version {HiveDatabaseSchema.CurrentSchemaVersion}."));
            }

            if (previousVersion is < HiveDatabaseSchema.MinimumSupportedSchemaVersion)
            {
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    Error.Unsupported(
                        "hive.persistence.unsupported-schema",
                        $"The Hive database schema version {previousVersion.Value} is below the minimum supported schema version {HiveDatabaseSchema.MinimumSupportedSchemaVersion}."));
            }

            var upgrader = DeployChanges
                .To.SqlDatabase(_options.ConnectionString)
                .JournalToSqlTable(
                    HiveDatabaseSchema.SchemaName,
                    HiveDatabaseSchema.MigrationJournalTableName)
                .WithScriptsEmbeddedInAssembly(_migrationAssembly)
                .WithTransactionPerScript()
                .LogToNowhere()
                .Build();

            var upgradeRequired = upgrader.IsUpgradeRequired();
            var upgradeResult = upgrader.PerformUpgrade();

            if (!upgradeResult.Successful)
            {
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    new Error(
                        "hive.persistence.migration-failed",
                        ErrorCategory.External,
                        $"Hive database migration failed: {upgradeResult.Error?.Message ?? "DbUp did not provide a migration error message."}"));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var currentVersion =
                await _schemaVersionStore
                    .GetVersionAsync(_options, cancellationToken)
                    .ConfigureAwait(false);

            if (currentVersion != HiveDatabaseSchema.CurrentSchemaVersion)
            {
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    Error.Conflict(
                        "hive.persistence.schema-version-mismatch",
                        $"Hive migration completed without reaching schema version {HiveDatabaseSchema.CurrentSchemaVersion}. Stored version: {currentVersion?.ToString() ?? "none"}."));
            }

            var appliedCount = upgradeResult.Scripts?.Count() ?? 0;
            var status = upgradeRequired
                ? HiveDatabaseMigrationStatus.Applied
                : HiveDatabaseMigrationStatus.AlreadyCurrent;

            return Result<HiveDatabaseMigrationOutcome>.Success(
                new HiveDatabaseMigrationOutcome(
                    status,
                    previousVersion ?? 0,
                    currentVersion.Value,
                    appliedCount));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                new Error(
                    "hive.persistence.migration-unexpected",
                    ErrorCategory.External,
                    $"Hive database migration failed unexpectedly: {exception.Message}"));
        }
    }
}
