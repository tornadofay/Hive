using Hive.Core;
using Hive.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Hive.Tests;

public sealed class EmbeddedPersistenceFoundationTests
{
    [Fact]
    public async Task InitializeAsync_CreatesFoundationSchemaAndIsIdempotent()
    {
        var path = CreatePath("initialize");

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var first = await database.InitializeAsync();

            Assert.True(first.IsSuccess, first.Error?.Message);
            var firstValue = RequireValue(first);

            Assert.Equal(HiveDatabaseMigrationStatus.Applied, firstValue.Status);
            Assert.Equal(0, firstValue.PreviousSchemaVersion);
            Assert.Equal(
                EmbeddedPersistenceSchema.CurrentSchemaVersion,
                firstValue.CurrentSchemaVersion);
            Assert.Equal(1, firstValue.AppliedMigrationCount);

            await using (var connection = await database.OpenConnectionAsync())
            {
                Assert.Equal(
                    "wal",
                    Convert.ToString(
                        await ExecuteScalarAsync(
                            connection,
                            "PRAGMA journal_mode;"),
                        System.Globalization.CultureInfo.InvariantCulture));

                Assert.Equal(
                    2L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            "PRAGMA synchronous;"),
                        System.Globalization.CultureInfo.InvariantCulture));

                Assert.Equal(
                    1L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            "PRAGMA foreign_keys;"),
                        System.Globalization.CultureInfo.InvariantCulture));

                Assert.Equal(
                    1L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            """
                            SELECT COUNT(*)
                            FROM [HiveSchemaVersion]
                            WHERE [SchemaRowId] = 1
                              AND [SchemaVersion] = 1;
                            """),
                        System.Globalization.CultureInfo.InvariantCulture));

                Assert.Equal(
                    1L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            """
                            SELECT COUNT(*)
                            FROM [HiveMigrationJournal]
                            WHERE [MigrationVersion] = 1
                              AND [ScriptName] = '001_PersistenceBootstrap.sql';
                            """),
                        System.Globalization.CultureInfo.InvariantCulture));
            }

            var second = await database.InitializeAsync();

            Assert.True(second.IsSuccess, second.Error?.Message);
            var secondValue = RequireValue(second);

            Assert.Equal(
                HiveDatabaseMigrationStatus.AlreadyCurrent,
                secondValue.Status);
            Assert.Equal(
                EmbeddedPersistenceSchema.CurrentSchemaVersion,
                secondValue.CurrentSchemaVersion);
            Assert.Equal(0, secondValue.AppliedMigrationCount);

            var status = await database.InspectAsync();

            Assert.True(status.IsSuccess, status.Error?.Message);
            var statusValue = RequireValue(status);

            Assert.Equal(HiveDatabaseState.Current, statusValue.DatabaseState);
            Assert.Equal(
                EmbeddedPersistenceSchema.CurrentSchemaVersion,
                statusValue.SchemaVersion);
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesParentDirectoryAndNormalizesAbsolutePath()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hive-embedded-foundation",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "store", "hive.db");
        var relativePath = Path.GetRelativePath(
            Environment.CurrentDirectory,
            path);

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(relativePath));

            Assert.Equal(
                Path.GetFullPath(relativePath),
                database.StoragePath);

            var result = await database.InitializeAsync();

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.True(File.Exists(database.StoragePath));
            Assert.True(
                Directory.Exists(
                    Path.GetDirectoryName(database.StoragePath)!));
        }
        finally
        {
            DeleteDatabaseFiles(path);

            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeAsync_HonorsCreateDatabaseIfMissingFalse()
    {
        var directory = CreateDirectory("no-create");
        var path = Path.Combine(directory, "hive.db");

        try
        {
            var configuration = HivePersistenceConfiguration.Embedded(
                path,
                createDatabaseIfMissing: false);

            await using var database = new EmbeddedPersistenceDatabase(
                configuration);

            var result = await database.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.storage-not-found",
                result.Error!.Code);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task InitializeAsync_RejectsStorageWhenParentPathIsAFile()
    {
        var directory = CreateDirectory("parent-file");
        var parentPath = Path.Combine(directory, "not-a-directory");
        var path = Path.Combine(parentPath, "hive.db");

        try
        {
            await File.WriteAllTextAsync(
                parentPath,
                "This path is intentionally a file.");

            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var result = await database.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.initialize.storage-access",
                result.Error!.Code);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task InspectAsync_ReturnsDatabaseNotFoundWithoutCreatingStorage()
    {
        var path = CreatePath("inspect-missing");

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var result = await database.InspectAsync();

            Assert.True(result.IsSuccess, result.Error?.Message);
            var resultValue = RequireValue(result);

            Assert.Equal(
                HiveDatabaseState.DatabaseNotFound,
                resultValue.DatabaseState);
            Assert.Null(resultValue.SchemaVersion);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task InspectAsync_ReturnsCorruptionAsStablePersistenceError()
    {
        var path = CreatePath("inspect-corrupt");

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);

            await File.WriteAllBytesAsync(
                path,
                [0x48, 0x69, 0x76, 0x65, 0x2D, 0x4E, 0x6F, 0x74]);

            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var result = await database.InspectAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.inspect.storage-corrupt",
                result.Error!.Code);
            Assert.Equal(
                ErrorCategory.External,
                result.Error.Category);
            Assert.DoesNotContain(
                "SQLite",
                result.Error.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task ReopenAfterAbandonedTransaction_PreservesCommittedFoundationState()
    {
        var path = CreatePath("reopen");

        try
        {
            await using (var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path)))
            {
                var initialized = await database.InitializeAsync();
                Assert.True(initialized.IsSuccess, initialized.Error?.Message);

                await using var connection = await database.OpenConnectionAsync();

                await using (var beginCommand = connection.CreateCommand())
                {
                    beginCommand.CommandText = "BEGIN IMMEDIATE;";
                    await beginCommand.ExecuteNonQueryAsync();
                }

                await using var transientCommand = connection.CreateCommand();
                transientCommand.CommandText =
                    """
                    CREATE TABLE [TransientRecoveryProbe]
                    (
                        [Value] INTEGER NOT NULL
                    );
                    """;
                await transientCommand.ExecuteNonQueryAsync();

                await connection.DisposeAsync();
            }

            await using var reopened = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var status = await reopened.InspectAsync();

            Assert.True(status.IsSuccess, status.Error?.Message);
            var statusValue = RequireValue(status);

            Assert.Equal(HiveDatabaseState.Current, statusValue.DatabaseState);
            Assert.Equal(
                EmbeddedPersistenceSchema.CurrentSchemaVersion,
                statusValue.SchemaVersion);

            await using var connectionAfterReopen =
                await reopened.OpenConnectionAsync();

            Assert.Equal(
                0L,
                Convert.ToInt64(
                    await ExecuteScalarAsync(
                        connectionAfterReopen,
                        """
                        SELECT COUNT(*)
                        FROM sqlite_master
                        WHERE type = 'table'
                          AND name = 'TransientRecoveryProbe';
                        """),
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_RejectsPreexistingNonHiveTables()
    {
        var path = CreatePath("non-hive");

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);

            await using (var connection = new SqliteConnection(
                $"Data Source={path};Mode=ReadWriteCreate;Pooling=False"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE [ForeignApplicationTable]
                    (
                        [Id] INTEGER NOT NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var result = await database.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.storage-not-empty",
                result.Error!.Code);

            var status = await database.InspectAsync();

            Assert.True(status.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.storage-not-empty",
                status.Error!.Code);

            await using (var verificationConnection = new SqliteConnection(
                $"Data Source={path};Mode=ReadWrite;Pooling=False"))
            {
                await verificationConnection.OpenAsync();

                Assert.Equal(
                    "delete",
                    Convert.ToString(
                        await ExecuteScalarAsync(
                            verificationConnection,
                            "PRAGMA journal_mode;"),
                        System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task FutureSchema_IsReportedAndInitializationIsRejected()
    {
        var path = CreatePath("future");

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var initialized = await database.InitializeAsync();
            Assert.True(initialized.IsSuccess, initialized.Error?.Message);

            await using (var connection = await database.OpenConnectionAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE [HiveSchemaVersion]
                    SET [SchemaVersion] = 99
                    WHERE [SchemaRowId] = 1;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var status = await database.InspectAsync();

            Assert.True(status.IsSuccess, status.Error?.Message);
            var statusValue = RequireValue(status);

            Assert.Equal(
                HiveDatabaseState.FutureSchema,
                statusValue.DatabaseState);
            Assert.Equal(99, statusValue.SchemaVersion);

            var result = await database.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.future-schema",
                result.Error!.Code);
            Assert.Equal(
                ErrorCategory.Unsupported,
                result.Error.Category);
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task InconsistentMetadata_IsRejected()
    {
        var path = CreatePath("inconsistent");

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var initialized = await database.InitializeAsync();
            Assert.True(initialized.IsSuccess, initialized.Error?.Message);

            await using (var connection = await database.OpenConnectionAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "DROP TABLE [HiveMigrationJournal];";
                await command.ExecuteNonQueryAsync();
            }

            var status = await database.InspectAsync();

            Assert.True(status.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.metadata-inconsistent",
                status.Error!.Code);

            var result = await database.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.metadata-inconsistent",
                result.Error!.Code);
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task FailedMigration_RollsBackScriptAndSchemaVersion()
    {
        var path = CreatePath("rollback");

        var migrations = new[]
        {
            new EmbeddedPersistenceMigration(
                1,
                "001_FailingFoundation.sql",
                """
                CREATE TABLE [HiveSchemaVersion]
                (
                    [SchemaRowId] INTEGER NOT NULL PRIMARY KEY,
                    [SchemaVersion] INTEGER NOT NULL,
                    [RecordedAtUtc] TEXT NOT NULL
                );

                CREATE TABLE [TransientMigrationProbe]
                (
                    [Value] INTEGER NOT NULL
                );

                THIS IS NOT VALID SQL;
                """)
        };

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path),
                clock: null,
                migrations);

            var result = await database.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.persistence.embedded.migration.sqlite-failure",
                result.Error!.Code);

            await using (var connection = new SqliteConnection(
                $"Data Source={path};Mode=ReadWrite;Pooling=False"))
            {
                await connection.OpenAsync();

                Assert.Equal(
                    0L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            """
                            SELECT COUNT(*)
                            FROM sqlite_master
                            WHERE type = 'table'
                              AND name = 'TransientMigrationProbe';
                            """),
                        System.Globalization.CultureInfo.InvariantCulture));

                Assert.Equal(
                    0L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            """
                            SELECT COUNT(*)
                            FROM sqlite_master
                            WHERE type = 'table'
                              AND name = 'HiveSchemaVersion';
                            """),
                        System.Globalization.CultureInfo.InvariantCulture));

                Assert.Equal(
                    0L,
                    Convert.ToInt64(
                        await ExecuteScalarAsync(
                            connection,
                            """
                            SELECT COUNT(*)
                            FROM sqlite_master
                            WHERE type = 'table'
                              AND name = 'HiveMigrationJournal';
                            """),
                        System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task LockedStorage_ProducesBoundedBusyFailure()
    {
        var path = CreatePath("busy");

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);

            await using (var firstConnection = new SqliteConnection(
                $"Data Source={path};Mode=ReadWriteCreate;Pooling=False"))
            {
                await firstConnection.OpenAsync();

                await using (var beginCommand = firstConnection.CreateCommand())
                {
                    beginCommand.CommandText = "BEGIN IMMEDIATE;";
                    await beginCommand.ExecuteNonQueryAsync();
                }

                await using var secondDatabase = new EmbeddedPersistenceDatabase(
                    HivePersistenceConfiguration.Embedded(
                        path,
                        commandTimeoutSeconds: 1));

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var result = await secondDatabase.InitializeAsync();

                stopwatch.Stop();

                Assert.True(result.IsFailure);
                Assert.Equal(
                    "hive.persistence.embedded.initialize.storage-busy",
                    result.Error!.Code);
                Assert.InRange(
                    stopwatch.Elapsed,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task CancellationBeforeInitialization_IsHonoredWithoutCreatingStorage()
    {
        var path = CreateUncreatedPath("cancel");
        var parent = Path.GetDirectoryName(path)!;

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => database.InitializeAsync(
                    cancellationSource.Token));

            Assert.False(File.Exists(path));
            Assert.False(Directory.Exists(parent));
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task DisposePreventsFurtherStorageOperations()
    {
        var path = CreatePath("dispose");

        try
        {
            await using var database = new EmbeddedPersistenceDatabase(
                HivePersistenceConfiguration.Embedded(path));

            var initialized = await database.InitializeAsync();
            Assert.True(initialized.IsSuccess, initialized.Error?.Message);

            await database.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => database.InspectAsync());

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => database.InitializeAsync());
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public void MigrationCatalog_RejectsGapsAndOutOfOrderVersions()
    {
        var migrations = new[]
        {
            new EmbeddedPersistenceMigration(1, "001_One.sql", "SELECT 1;"),
            new EmbeddedPersistenceMigration(3, "003_Three.sql", "SELECT 3;")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => EmbeddedPersistenceMigrationCatalog.Validate(migrations));

        Assert.Contains(
            "contiguous",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static T RequireValue<T>(Result<T> result)
    {
        return result.Value
            ?? throw new InvalidOperationException(
                "Expected a successful result with a value.");
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqliteConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync();
    }

    private static string CreatePath(string name)
    {
        var directory = CreateDirectory(name);
        return Path.Combine(directory, "hive.db");
    }

    private static string CreateUncreatedPath(string name)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "hive-embedded-foundation",
            $"{name}-{Guid.NewGuid():N}");

        return Path.Combine(directory, "hive.db");
    }

    private static string CreateDirectory(string name)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "hive-embedded-foundation",
            $"{name}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDatabaseFiles(string path)
    {
        foreach (var file in new[]
        {
            path,
            $"{path}-wal",
            $"{path}-shm"
        })
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory) &&
            Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
