using System.Data;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class HivePersistenceConnectionTester : IHivePersistenceConnectionTester
{
    public async Task<Result<HivePersistenceConnectionTest>> TestAsync(
        HivePersistenceConfiguration configuration,
        SecretMaterial? credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Backend == HivePersistenceBackend.Embedded)
        {
            return await TestEmbeddedAsync(
                configuration,
                cancellationToken).ConfigureAwait(false);
        }

        if (configuration.Backend != HivePersistenceBackend.SqlServer)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                Error.Unsupported(
                    "hive.persistence.backend-not-supported",
                    "The selected persistence backend is not supported by this connection tester."));
        }

        try
        {
            var options = HiveDatabaseOptions.FromConfiguration(
                configuration,
                credential);

            var builder = new SqlConnectionStringBuilder(options.ConnectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = 15
            };

            await using var masterConnection = new SqlConnection(
                builder.ConnectionString);

            await masterConnection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            await using var existsCommand = masterConnection.CreateCommand();
            existsCommand.CommandTimeout = configuration.CommandTimeoutSeconds;
            existsCommand.CommandText = """
                SELECT COUNT_BIG(1)
                FROM [sys].[databases]
                WHERE [name] = @DatabaseName;
                """;
            existsCommand.Parameters.Add(
                new SqlParameter(
                    "@DatabaseName",
                    SqlDbType.NVarChar,
                    128)
                {
                    Value = configuration.DatabaseName
                });

            var exists = Convert.ToInt64(
                await existsCommand.ExecuteScalarAsync(cancellationToken)
                    .ConfigureAwait(false)) > 0;

            if (!exists)
            {
                return Result<HivePersistenceConnectionTest>.Success(
                    new HivePersistenceConnectionTest(
                        true,
                        HiveDatabaseState.DatabaseNotFound,
                        null,
                        HiveDatabaseSchema.CurrentSchemaVersion,
                        "SQL Server connection succeeded, but the Hive database does not exist."));
            }

            var databaseBuilder = new SqlConnectionStringBuilder(
                options.ConnectionString);

            await using var databaseConnection = new SqlConnection(
                databaseBuilder.ConnectionString);

            await databaseConnection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            await using var schemaCommand = databaseConnection.CreateCommand();
            schemaCommand.CommandTimeout = configuration.CommandTimeoutSeconds;
            schemaCommand.CommandText = $"""
                IF OBJECT_ID(
                    N'[{HiveDatabaseSchema.SchemaName}].[{HiveDatabaseSchema.SchemaVersionTableName}]',
                    N'U') IS NULL
                BEGIN
                    SELECT CAST(NULL AS INT);
                    RETURN;
                END;

                SELECT [SchemaVersion]
                FROM [{HiveDatabaseSchema.SchemaName}].[{HiveDatabaseSchema.SchemaVersionTableName}]
                WHERE [SchemaRowId] = @SchemaRowId;
                """;
            schemaCommand.Parameters.Add(
                new SqlParameter(
                    "@SchemaRowId",
                    SqlDbType.TinyInt)
                {
                    Value = HiveDatabaseSchema.SchemaRowId
                });

            var schemaValue = await schemaCommand
                .ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false);

            int? schemaVersion = schemaValue is null or DBNull
                ? null
                : Convert.ToInt32(schemaValue);

            var state = schemaVersion switch
            {
                null => HiveDatabaseState.SchemaNotInitialized,
                > HiveDatabaseSchema.CurrentSchemaVersion => HiveDatabaseState.FutureSchema,
                < HiveDatabaseSchema.CurrentSchemaVersion => HiveDatabaseState.NeedsMigration,
                _ => HiveDatabaseState.Current
            };

            var message = state switch
            {
                HiveDatabaseState.Current =>
                    "SQL Server connection succeeded and the Hive schema is current.",
                HiveDatabaseState.NeedsMigration =>
                    "SQL Server connection succeeded and the Hive database requires migration.",
                HiveDatabaseState.FutureSchema =>
                    "SQL Server connection succeeded, but the Hive database schema is newer than this Hive build supports.",
                HiveDatabaseState.SchemaNotInitialized =>
                    "SQL Server connection succeeded and the database exists, but Hive schema metadata is not initialized.",
                _ => "SQL Server connection succeeded."
            };

            return Result<HivePersistenceConnectionTest>.Success(
                new HivePersistenceConnectionTest(
                    true,
                    state,
                    schemaVersion,
                    HiveDatabaseSchema.CurrentSchemaVersion,
                    message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                Error.Validation(
                    "hive.persistence.configuration-invalid",
                    "The persistence configuration is invalid."));
        }
        catch (SqlException exception)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.connection-failed",
                    "SQL Server connection test failed.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.connection-test-failed",
                    "Persistence connection test failed unexpectedly.",
                    exception));
        }
    }

    private static async Task<Result<HivePersistenceConnectionTest>> TestEmbeddedAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var database = new EmbeddedPersistenceDatabase(configuration);
            var inspection = await database
                .InspectAsync(cancellationToken)
                .ConfigureAwait(false);

            if (inspection.IsFailure)
                return Result<HivePersistenceConnectionTest>.Failure(inspection.Error!);

            var state = inspection.Value
                ?? throw new InvalidOperationException(
                    "Embedded persistence inspection returned no state.");

            var message = state.DatabaseState switch
            {
                HiveDatabaseState.DatabaseNotFound =>
                    "Embedded storage is available, but the Hive database does not exist yet.",
                HiveDatabaseState.SchemaNotInitialized =>
                    "Embedded storage is available and the Hive database exists, but its schema is not initialized.",
                HiveDatabaseState.NeedsMigration =>
                    "Embedded storage is available and the Hive database requires migration.",
                HiveDatabaseState.FutureSchema =>
                    "Embedded storage is available, but the Hive database schema is newer than this Hive build supports.",
                HiveDatabaseState.Current =>
                    "Embedded storage is available and the Hive schema is current.",
                _ => "Embedded persistence storage inspection succeeded."
            };

            return Result<HivePersistenceConnectionTest>.Success(
                new HivePersistenceConnectionTest(
                    true,
                    state.DatabaseState,
                    state.SchemaVersion,
                    EmbeddedPersistenceSchema.CurrentSchemaVersion,
                    message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                Error.Validation(
                    "hive.persistence.configuration-invalid",
                    "The persistence configuration is invalid."));
        }
        catch (Exception exception)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.embedded.inspect-failed",
                    "Embedded persistence readiness test failed.",
                    exception));
        }
    }
}
