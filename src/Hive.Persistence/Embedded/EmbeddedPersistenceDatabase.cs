using System.Reflection;
using Hive.Core;
using Microsoft.Data.Sqlite;

namespace Hive.Persistence;

internal sealed class EmbeddedPersistenceDatabase : IAsyncDisposable
{
    private readonly string _storagePath;
    private readonly string _connectionString;
    private readonly string _createConnectionString;
    private readonly int _commandTimeoutSeconds;
    private readonly HivePersistenceConfiguration _configuration;
    private readonly EmbeddedPersistenceMigrator _migrator;
    private int _disposed;

    public EmbeddedPersistenceDatabase(
        HivePersistenceConfiguration configuration,
        IClock? clock = null)
        : this(
            configuration,
            clock,
            EmbeddedPersistenceMigrationCatalog.Load(
                typeof(EmbeddedPersistenceDatabase).Assembly))
    {
    }

    internal EmbeddedPersistenceDatabase(
        HivePersistenceConfiguration configuration,
        IClock? clock,
        IReadOnlyList<EmbeddedPersistenceMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Backend != HivePersistenceBackend.Embedded)
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Embedded persistence configuration is required.");

        _storagePath = NormalizeStoragePath(
            configuration.EmbeddedStoragePath!);
        _commandTimeoutSeconds = configuration.CommandTimeoutSeconds;
        _configuration = configuration;

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _storagePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            Pooling = true,
            DefaultTimeout = _commandTimeoutSeconds
        };

        _connectionString = builder.ToString();
        builder.Mode = SqliteOpenMode.ReadWriteCreate;
        _createConnectionString = builder.ToString();

        _migrator = new EmbeddedPersistenceMigrator(
            _commandTimeoutSeconds,
            migrations,
            clock);
    }

    internal string StoragePath => _storagePath;

    public async Task<Result<EmbeddedPersistenceDatabaseStatus>> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(_storagePath))
        {
            return Result<EmbeddedPersistenceDatabaseStatus>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.embedded.invalid-storage-path",
                    "The Embedded storage path points to a directory, not a database file.",
                    new InvalidOperationException()));
        }

        if (!File.Exists(_storagePath))
        {
            return Result<EmbeddedPersistenceDatabaseStatus>.Success(
                new EmbeddedPersistenceDatabaseStatus(
                    HiveDatabaseState.DatabaseNotFound,
                    null));
        }

        try
        {
            await using var connection = await OpenConnectionAsync(
                    createIfMissing: false,
                    cancellationToken)
                .ConfigureAwait(false);

            return await _migrator.InspectAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
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

    public async Task<Result<HiveDatabaseMigrationOutcome>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(_storagePath))
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.embedded.invalid-storage-path",
                    "The Embedded storage path points to a directory, not a database file.",
                    new InvalidOperationException()));
        }

        if (!File.Exists(_storagePath) &&
            !_configuration.CreateDatabaseIfMissing)
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                EmbeddedPersistenceError.StorageNotFound());
        }

        try
        {
            if (_configuration.CreateDatabaseIfMissing)
                EnsureParentDirectory();

            await using var connection = await OpenConnectionAsync(
                    createIfMissing: _configuration.CreateDatabaseIfMissing,
                    cancellationToken)
                .ConfigureAwait(false);

            var state = await _migrator.InspectAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

            if (state.IsFailure)
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    state.Error!);

            if (state.Value.DatabaseState ==
                HiveDatabaseState.FutureSchema)
            {
                return Result<HiveDatabaseMigrationOutcome>.Failure(
                    EmbeddedPersistenceError.FutureSchema(
                        state.Value.SchemaVersion!.Value));
            }

            await ConfigureDatabaseAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

            return await _migrator.MigrateAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<HiveDatabaseMigrationOutcome>.Failure(
                EmbeddedPersistenceError.From(
                    "hive.persistence.embedded.initialize",
                    "initialization",
                    exception));
        }
    }

    internal async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_storagePath))
            throw new FileNotFoundException(
                "The configured Embedded storage file does not exist.",
                _storagePath);

        return await OpenConnectionAsync(
                createIfMissing: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        bool createIfMissing,
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(
            createIfMissing
                ? _createConnectionString
                : _connectionString);

        connection.DefaultTimeout = _commandTimeoutSeconds;

        try
        {
            await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            await ConfigureConnectionAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task ConfigureConnectionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var synchronousCommand = connection.CreateCommand();
        synchronousCommand.CommandText = "PRAGMA synchronous = FULL;";
        synchronousCommand.CommandTimeout = _commandTimeoutSeconds;

        await synchronousCommand.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ConfigureDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var journalCommand = connection.CreateCommand();
        journalCommand.CommandTimeout = _commandTimeoutSeconds;
        journalCommand.CommandText = "PRAGMA journal_mode = WAL;";

        await journalCommand.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var synchronousCommand = connection.CreateCommand();
        synchronousCommand.CommandTimeout = _commandTimeoutSeconds;
        synchronousCommand.CommandText = "PRAGMA synchronous = FULL;";

        await synchronousCommand.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private void EnsureParentDirectory()
    {
        var parentDirectory = Directory.GetParent(_storagePath)?.FullName;

        if (string.IsNullOrWhiteSpace(parentDirectory))
            throw new IOException(
                "The Embedded storage path does not have a usable parent directory.");

        Directory.CreateDirectory(parentDirectory);
    }

    private static string NormalizeStoragePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException(
                "Embedded storage path is required.",
                nameof(storagePath));

        string normalizedPath;

        try
        {
            normalizedPath = Path.GetFullPath(storagePath.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            exception is NotSupportedException or
            exception is PathTooLongException)
        {
            throw new ArgumentException(
                "Embedded storage path is invalid.",
                nameof(storagePath),
                exception);
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(normalizedPath)))
            throw new ArgumentException(
                "Embedded storage path must identify a database file.",
                nameof(storagePath));

        return normalizedPath;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
