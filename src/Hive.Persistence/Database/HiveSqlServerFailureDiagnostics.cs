using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal static class HiveSqlServerFailureDiagnostics
{
    public static string DescribeMigrationFailure(
        string operation,
        string endpointRole,
        string serverName,
        string databaseName,
        SqlException exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentNullException.ThrowIfNull(exception);

        var firstError = exception.Errors
            .Cast<SqlError>()
            .FirstOrDefault(static error => error.Number != 0)
            ?? (exception.Errors.Count > 0 ? exception.Errors[0] : null);

        return DescribeMigrationFailure(
            operation,
            endpointRole,
            serverName,
            databaseName,
            firstError?.Number ?? exception.Number,
            firstError?.State ?? 0,
            firstError?.Class ?? 0);
    }

    public static string DescribeMigrationFailure(
        string operation,
        string endpointRole,
        string serverName,
        string databaseName,
        int errorNumber,
        byte state,
        byte errorClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var guidance = errorNumber switch
        {
            911 =>
                "SQL Server could not find the configured database. If the database was renamed, verify that this endpoint uses its exact current name.",
            4060 or 916 =>
                "SQL Server reached the instance but could not open the configured database. Verify its name and the migration account's database access, especially if the database was renamed.",
            18456 =>
                "SQL Server rejected authentication. Verify Windows-integrated access or the configured SQL login without sharing credential material.",
            1801 =>
                "SQL Server reports that a database with this name already exists. Check the selected destination name and whether the intended destination is already present.",
            229 or 262 =>
                "SQL Server denied a required operation. The migration account may need database creation, schema initialization, and read/write permissions for the selected migration direction.",
            2627 or 2601 =>
                "SQL Server rejected a duplicate key during transfer. The destination must be empty and compatible; do not retry against an existing Hive dataset.",
            2714 =>
                "A required schema object already exists while preparing the SQL Server database. Inspect the destination state before retrying; do not drop a database that may contain useful Hive data.",
            -2 =>
                "The SQL Server operation timed out. Verify server responsiveness and the configured command timeout.",
            -1 or 2 or 26 or 40 or 53 or 11001 or 11004 =>
                "The SQL Server endpoint could not be reached. Verify the instance name, SQL Server service, and named-instance or port resolution.",
            _ =>
                "Verify the SQL Server endpoint and database name, database permissions, and schema compatibility for the selected migration direction."
        };

        return $"The {operation} failed for the {endpointRole} SQL Server endpoint '{serverName}', database '{databaseName}' (SQL error {errorNumber}, state {state}, class {errorClass}). {guidance} Raw SQL error text, SQL statements, connection strings, and credentials were omitted from this diagnostic.";
    }
}
