using System.Reflection;
using Hive.Core;
using Microsoft.Data.Sqlite;

namespace Hive.Persistence;

internal sealed class EmbeddedPersistenceMigrator
{
    private readonly IReadOnlyList<EmbeddedPersistenceMigration> _migrations;
    private readonly int _commandTimeoutSeconds;
    private readonly IClock _clock;

    public EmbeddedPersistenceMigrator(
        int commandTimeoutSeconds,
        Assembly migrationAssembly,
        IClock? clock = null)
        : this(
            commandTimeoutSeconds,
            EmbeddedPersistenceMigrationCatalog.Load(migrationAssembly),
            clock)
    {
    }

    internal EmbeddedPersistenceMigrator(
        int commandTimeoutSeconds,
        IReadOnlyList<EmbeddedPersistenceMigration> migrations,
        IClock? clock = null)
    {
        if (commandTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));

        _migrations = migrations ?? throw new ArgumentNullException(nameof(migrations));
        EmbeddedPersistenceMigrationCatalog.Validate(_migrations);
        _commandTimeoutSeconds = commandTimeoutSeconds;
        _clock = clock ?? SystemClock.Instance;
    }

    public async Task<Result<EmbeddedPersistenceDatabaseStatus>> InspectAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            var state = await ReadStateAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

            return Result<EmbeddedPersistenceDatabaseStatus>.Success(state);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EmbeddedPersistenceMetadataException)
        {
            return Result<EmbeddedPersistenceDatabaseStatus>.Failure(
                EmbeddedPersistenceError.InvalidMetadata());
        }
        catch (EmbeddedPersistenceStorageNotEmptyException)
        {
            return Result<EmbeddedPersistenceDatabaseStatus>.Failure(
                EmbeddedPersistenceError.StorageNotEmpty());
        }
        catch (Exception exception)
        {
            return Result<EmbeddedPersistenceDatabaseStatus>.Failure(
                EmbeddedPersistenceError.From(
                    "hive.persistence.embedded.inspect",
                    "storage inspection",
                    exception));
        }
    }

    public async Task<Result<HiveDatabaseMigrationOutcome>> MigrateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            var initialState = await ReadStateAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

            if (initialState.DatabaseState == HiveDatabaseState.FutureSchema)
            {
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    EmbeddedPersistenceError.FutureSchema(
                        initialState.SchemaVersion!.Value));
            }

            if (initialState.DatabaseState == HiveDatabaseState.Current)
            {
                return Result<HiveDatabaseMigrationOutcome>.Success(
                    new HiveDatabaseMigrationOutcome(
                        HiveDatabaseMigrationStatus.AlreadyCurrent,
                        initialState.SchemaVersion!.Value,
                        initialState.SchemaVersion.Value,
                        0));
            }

            var previousVersion = initialState.SchemaVersion ?? 0;
            var initialVersion = previousVersion;
            var appliedCount = 0;

            while (previousVersion < EmbeddedPersistenceSchema.CurrentSchemaVersion)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await ExecuteCommandAsync(
                        connection,
                        "BEGIN IMMEDIATE;",
                        cancellationToken)
                    .ConfigureAwait(false);

                var transactionStarted = true;

                try
                {
                    var state = await ReadStateAsync(
                            connection,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (state.DatabaseState == HiveDatabaseState.FutureSchema)
                    {
                        await RollbackAsync(connection).ConfigureAwait(false);
                        transactionStarted = false;

                        return Result<HiveDatabaseMigrationOutcome>.Failure(
                            EmbeddedPersistenceError.FutureSchema(
                                state.SchemaVersion!.Value));
                    }

                    if (state.DatabaseState == HiveDatabaseState.Current)
                    {
                        await CommitAsync(connection).ConfigureAwait(false);
                        transactionStarted = false;

                        return Result<HiveDatabaseMigrationOutcome>.Success(
                            new HiveDatabaseMigrationOutcome(
                                HiveDatabaseMigrationStatus.AlreadyCurrent,
                                state.SchemaVersion!.Value,
                                state.SchemaVersion.Value,
                                appliedCount));
                    }

                    var nextVersion = (state.SchemaVersion ?? 0) + 1;
                    var migration = _migrations
                        .FirstOrDefault(candidate =>
                            candidate.Version == nextVersion)
                        ?? throw new InvalidOperationException(
                            "The next Embedded persistence migration is missing.");

                    await ExecuteCommandAsync(
                            connection,
                            migration.Sql,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await InsertJournalRowAsync(
                            connection,
                            migration,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await UpsertSchemaVersionAsync(
                            connection,
                            migration.Version,
                            cancellationToken)
                        .ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    await CommitAsync(connection).ConfigureAwait(false);
                    transactionStarted = false;

                    previousVersion = migration.Version;
                    appliedCount++;
                }
                catch
                {
                    if (transactionStarted)
                        await RollbackAsync(connection).ConfigureAwait(false);

                    throw;
                }
            }

            return Result<HiveDatabaseMigrationOutcome>.Success(
                new HiveDatabaseMigrationOutcome(
                    appliedCount == 0
                        ? HiveDatabaseMigrationStatus.AlreadyCurrent
                        : HiveDatabaseMigrationStatus.Applied,
                    initialVersion,
                    previousVersion,
                    appliedCount));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EmbeddedPersistenceMetadataException)
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                EmbeddedPersistenceError.InvalidMetadata());
        }
        catch (EmbeddedPersistenceStorageNotEmptyException)
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                EmbeddedPersistenceError.StorageNotEmpty());
        }
        catch (Exception exception)
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                EmbeddedPersistenceError.From(
                    "hive.persistence.embedded.migration",
                    "migration",
                    exception));
        }
    }

    private async Task<EmbeddedPersistenceDatabaseStatus> ReadStateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var schemaVersionTableExists = await TableExistsAsync(
                connection,
                EmbeddedPersistenceSchema.SchemaVersionTableName,
                cancellationToken)
            .ConfigureAwait(false);

        var journalTableExists = await TableExistsAsync(
                connection,
                EmbeddedPersistenceSchema.MigrationJournalTableName,
                cancellationToken)
            .ConfigureAwait(false);

        if (!schemaVersionTableExists && !journalTableExists)
        {
            if (await HasUnexpectedTablesAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                throw new EmbeddedPersistenceStorageNotEmptyException();
            }

            return new EmbeddedPersistenceDatabaseStatus(
                HiveDatabaseState.SchemaNotInitialized,
                null);
        }

        if (schemaVersionTableExists != journalTableExists)
        {
            throw new EmbeddedPersistenceMetadataException();
        }

        var schemaVersion = await ReadSchemaVersionAsync(
                connection,
                cancellationToken)
            .ConfigureAwait(false);

        if (schemaVersion is null ||
            schemaVersion < EmbeddedPersistenceSchema.MinimumSupportedSchemaVersion)
            throw new EmbeddedPersistenceMetadataException();

        if (schemaVersion > EmbeddedPersistenceSchema.CurrentSchemaVersion)
        {
            return new EmbeddedPersistenceDatabaseStatus(
                HiveDatabaseState.FutureSchema,
                schemaVersion);
        }

        var journalEntries = await ReadJournalEntriesAsync(
                connection,
                cancellationToken)
            .ConfigureAwait(false);

        if (journalEntries.Count != schemaVersion)
            throw new EmbeddedPersistenceMetadataException();

        for (var index = 0; index < journalEntries.Count; index++)
        {
            var expectedMigration = _migrations[index];
            var actual = journalEntries[index];

            if (actual.Version != expectedMigration.Version ||
                !string.Equals(
                    actual.Name,
                    expectedMigration.Name,
                    StringComparison.Ordinal))
            {
                throw new EmbeddedPersistenceMetadataException();
            }
        }

        return new EmbeddedPersistenceDatabaseStatus(
            schemaVersion == EmbeddedPersistenceSchema.CurrentSchemaVersion
                ? HiveDatabaseState.Current
                : HiveDatabaseState.NeedsMigration,
            schemaVersion);
    }

    private async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = $name
            );
            """);

        command.Parameters.AddWithValue("$name", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);

        return Convert.ToInt32(result) == 1;
    }

    private async Task<bool> HasUnexpectedTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
                  AND name NOT IN ($schemaTable, $journalTable)
            );
            """);

        command.Parameters.AddWithValue(
            "$schemaTable",
            EmbeddedPersistenceSchema.SchemaVersionTableName);
        command.Parameters.AddWithValue(
            "$journalTable",
            EmbeddedPersistenceSchema.MigrationJournalTableName);

        var result = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);

        return Convert.ToInt32(result) == 1;
    }

    private async Task<int?> ReadSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT [SchemaVersion]
            FROM [HiveSchemaVersion]
            WHERE [SchemaRowId] = $schemaRowId;
            """);

        command.Parameters.AddWithValue(
            "$schemaRowId",
            EmbeddedPersistenceSchema.SchemaRowId);

        var result = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null or DBNull
            ? null
            : Convert.ToInt32(result);
    }

    private async Task<IReadOnlyList<EmbeddedPersistenceJournalEntry>>
        ReadJournalEntriesAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT
                [MigrationVersion],
                [ScriptName]
            FROM [HiveMigrationJournal]
            ORDER BY [MigrationVersion];
            """);

        var entries = new List<EmbeddedPersistenceJournalEntry>();

        await using var reader = await command.ExecuteReaderAsync(
                cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(
                new EmbeddedPersistenceJournalEntry(
                    reader.GetInt32(0),
                    reader.GetString(1)));
        }

        return entries;
    }

    private async Task InsertJournalRowAsync(
        SqliteConnection connection,
        EmbeddedPersistenceMigration migration,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            INSERT INTO [HiveMigrationJournal]
            (
                [MigrationVersion],
                [ScriptName],
                [AppliedAtUtc]
            )
            VALUES
            (
                $migrationVersion,
                $scriptName,
                $appliedAtUtc
            );
            """);

        command.Parameters.AddWithValue(
            "$migrationVersion",
            migration.Version);
        command.Parameters.AddWithValue(
            "$scriptName",
            migration.Name);
        command.Parameters.AddWithValue(
            "$appliedAtUtc",
            _clock.UtcNow.UtcDateTime.ToString(
                "O",
                System.Globalization.CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpsertSchemaVersionAsync(
        SqliteConnection connection,
        int version,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [HiveSchemaVersion]
            SET
                [SchemaVersion] = $schemaVersion,
                [RecordedAtUtc] = $recordedAtUtc
            WHERE [SchemaRowId] = $schemaRowId;

            INSERT INTO [HiveSchemaVersion]
            (
                [SchemaRowId],
                [SchemaVersion],
                [RecordedAtUtc]
            )
            SELECT
                $schemaRowId,
                $schemaVersion,
                $recordedAtUtc
            WHERE changes() = 0;
            """);

        var recordedAtUtc = _clock.UtcNow.UtcDateTime.ToString(
            "O",
            System.Globalization.CultureInfo.InvariantCulture);

        command.Parameters.AddWithValue(
            "$schemaRowId",
            EmbeddedPersistenceSchema.SchemaRowId);
        command.Parameters.AddWithValue("$schemaVersion", version);
        command.Parameters.AddWithValue("$recordedAtUtc", recordedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private SqliteCommand CreateCommand(
        SqliteConnection connection,
        string commandText) =>
        new(commandText, connection)
        {
            CommandTimeout = _commandTimeoutSeconds
        };

    private Task ExecuteCommandAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken) =>
        ExecuteNonQueryAsync(
            connection,
            commandText,
            cancellationToken);

    private async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            commandText);

        await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private Task CommitAsync(SqliteConnection connection) =>
        ExecuteNonQueryAsync(
            connection,
            "COMMIT;",
            CancellationToken.None);

    private Task RollbackAsync(SqliteConnection connection) =>
        ExecuteNonQueryAsync(
            connection,
            "ROLLBACK;",
            CancellationToken.None);

    private sealed record EmbeddedPersistenceJournalEntry(
        int Version,
        string Name);

    private sealed class EmbeddedPersistenceMetadataException : Exception
    {
    }

    private sealed class EmbeddedPersistenceStorageNotEmptyException : Exception
    {
    }
}
