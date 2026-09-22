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

            var schemaVersion = schemaValue is null or DBNull
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
        catch (ArgumentException exception)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                Error.Validation(
                    "hive.persistence.configuration-invalid",
                    exception.Message));
        }
        catch (SqlException exception)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                new Error(
                    "hive.persistence.connection-failed",
                    ErrorCategory.External,
                    $"SQL Server connection test failed: {exception.Message}"));
        }
        catch (Exception exception)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                new Error(
                    "hive.persistence.connection-test-failed",
                    ErrorCategory.External,
                    $"Persistence connection test failed: {exception.Message}"));
        }
    }
}
