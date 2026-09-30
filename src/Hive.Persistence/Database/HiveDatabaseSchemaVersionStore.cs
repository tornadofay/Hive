using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal sealed class HiveDatabaseSchemaVersionStore
{
    public async Task<int?> GetVersionAsync(
        HiveDatabaseOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = options.CommandTimeoutSeconds;
        command.CommandText = $"""
            IF OBJECT_ID(N'[{HiveDatabaseSchema.SchemaName}].[{HiveDatabaseSchema.SchemaVersionTableName}]', N'U') IS NULL
            BEGIN
                SELECT CAST(NULL AS int);
                RETURN;
            END;

            SELECT [SchemaVersion]
            FROM [{HiveDatabaseSchema.SchemaName}].[{HiveDatabaseSchema.SchemaVersionTableName}]
            WHERE [SchemaRowId] = @SchemaRowId;
            """;
        command.Parameters.Add(new SqlParameter("@SchemaRowId", System.Data.SqlDbType.TinyInt)
        {
            Value = HiveDatabaseSchema.SchemaRowId
        });

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }
}