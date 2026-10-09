using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Hive.Tests.TestInfrastructure;

/// <summary>
/// Owns temporary SQL Server databases created by Hive's integration tests.
/// Only names generated in the reserved Hive_TestOwned_ namespace are eligible
/// for stale recovery, and a database-level extended property proves ownership.
/// </summary>
internal static class SqlTestDatabaseLifecycle
{
    private const string ReservedPrefix = "Hive_TestOwned_";
    private const string OwnershipPropertyName = "Hive.Tests.Ownership";
    private static readonly TimeSpan StaleDatabaseAge = TimeSpan.FromHours(24);
    private static readonly Regex OwnedDatabaseNamePattern = new(
        @"^Hive_TestOwned_(?<created>\d{14})_(?<run>[0-9A-F]{8})_(?<token>[0-9A-F]{8})_(?<label>[A-Za-z0-9_]{1,40})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Guid CurrentRunId = Guid.NewGuid();
    private static readonly object RecoveryLock = new();
    private static bool _staleRecoveryAttempted;
    private static SqlConnection? _runLeaseConnection;

    public static OwnedDatabase Create(string logicalName)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException(
                "A logical SQL test database name is required.",
                nameof(logicalName));
        }

        EnsureStaleRecoveryAttempted();
        EnsureCurrentRunLease();

        var label = SanitizeLabel(logicalName);
        var baseConnectionString = CreateMasterConnectionString();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var ownershipToken = Guid.NewGuid();
            var createdAtUtc = DateTimeOffset.UtcNow;
            var timestamp = createdAtUtc.ToString(
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture);
            var runToken = CurrentRunId.ToString("N")[..8].ToUpperInvariant();
            var databaseToken = ownershipToken.ToString("N")[..8].ToUpperInvariant();
            var databaseName = $"{ReservedPrefix}{timestamp}_{runToken}_{databaseToken}_{label}";
            var marker = CreateOwnershipMarker(CurrentRunId, ownershipToken, timestamp);

            try
            {
                CreateDatabase(baseConnectionString, databaseName);
            }
            catch (SqlException exception) when (exception.Number == 1801)
            {
                // A name collision is extraordinarily unlikely, but a collision
                // must never cause reuse or deletion of an existing database.
                if (attempt < 4)
                    continue;

                throw new InvalidOperationException(
                    "Hive.Tests could not reserve a unique SQL test database name after five attempts.");
            }

            try
            {
                WriteOwnershipMarker(
                    baseConnectionString,
                    databaseName,
                    marker);

                var databaseBuilder = new SqlConnectionStringBuilder(
                    HivePersistenceTestConfiguration.ConnectionString)
                {
                    InitialCatalog = databaseName,
                    ApplicationName = "Hive.Tests",
                    Pooling = false
                };

                return new OwnedDatabase(
                    databaseName,
                    logicalName,
                    CurrentRunId,
                    ownershipToken,
                    timestamp,
                    marker,
                    databaseBuilder.ConnectionString);
            }
            catch (Exception creationFailure)
            {
                try
                {
                    // This code path can only reach here after this invocation
                    // successfully executed CREATE DATABASE for this random name.
                    DropDatabaseWithoutMarkerCheck(baseConnectionString, databaseName);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        $"Hive.Tests created '{databaseName}' but failed to register its ownership marker and could not roll back its own database.",
                        creationFailure,
                        cleanupFailure);
                }

                throw;
            }
        }
    }

    public static async ValueTask DropOwnedAsync(
        OwnedDatabase database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var masterConnectionString = CreateMasterConnectionString();
        if (!await DatabaseExistsAsync(
                masterConnectionString,
                database.DatabaseName,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var observedMarker = await ReadOwnershipMarkerAsync(
            database.ConnectionString,
            cancellationToken).ConfigureAwait(false);

        if (!string.Equals(
                observedMarker,
                database.OwnershipMarker,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to drop SQL database '{database.DatabaseName}': its Hive.Tests ownership marker is missing or does not match the creating lease.");
        }

        await DropDatabaseAsync(
            masterConnectionString,
            database.DatabaseName,
            cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsStrictOwnedName(string databaseName)
    {
        return OwnedDatabaseNamePattern.IsMatch(databaseName);
    }

    internal static bool IsStaleOwnedName(
        string databaseName,
        DateTimeOffset nowUtc,
        TimeSpan minimumAge)
    {
        var match = OwnedDatabaseNamePattern.Match(databaseName);
        if (!match.Success)
            return false;

        if (!DateTimeOffset.TryParseExact(
                match.Groups["created"].Value,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var createdAtUtc))
        {
            return false;
        }

        return createdAtUtc <= nowUtc - minimumAge;
    }

    internal static bool OwnershipMarkerMatchesName(
        string databaseName,
        string? marker)
    {
        var match = OwnedDatabaseNamePattern.Match(databaseName);
        if (!match.Success || string.IsNullOrWhiteSpace(marker))
            return false;

        var parts = marker.Split('|');
        if (parts.Length != 4 || parts[0] != "v1")
            return false;

        if (!Guid.TryParseExact(parts[1], "N", out var runId) ||
            !Guid.TryParseExact(parts[2], "N", out var ownershipToken))
        {
            return false;
        }

        var createdAt = match.Groups["created"].Value;
        return string.Equals(
                   runId.ToString("N")[..8],
                   match.Groups["run"].Value,
                   StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                   ownershipToken.ToString("N")[..8],
                   match.Groups["token"].Value,
                   StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[3], createdAt, StringComparison.Ordinal);
    }

    private static void EnsureStaleRecoveryAttempted()
    {
        lock (RecoveryLock)
        {
            if (_staleRecoveryAttempted)
                return;

            // A shared run lock is held for this test-host process. The stale
            // recovery path must acquire the matching exclusive run lock before
            // it can drop databases from a previous run, so a second live
            // testhost cannot reap databases owned by a long-running test.
            EnsureCurrentRunLease();

            try
            {
                RecoverStaleOwnedDatabases();
            }
            catch (Exception exception)
            {
                ReportCleanupFailure(
                    "<stale Hive test database scan>",
                    exception);
            }
            finally
            {
                _staleRecoveryAttempted = true;
            }
        }
    }

    private static void EnsureCurrentRunLease()
    {
        if (_runLeaseConnection?.State == ConnectionState.Open)
            return;

        _runLeaseConnection?.Dispose();
        _runLeaseConnection = null;

        var connection = new SqlConnection(CreateMasterConnectionString());
        try
        {
            connection.Open();
            var result = ExecuteRunLock(connection, CurrentRunId, "Shared");
            if (result < 0)
            {
                throw new InvalidOperationException(
                    $"Hive.Tests could not acquire its SQL test-run lease (sp_getapplock returned {result}).");
            }

            _runLeaseConnection = connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static SqlConnection? TryAcquireStaleRunLock(
        string masterConnectionString,
        Guid staleRunId)
    {
        var connection = new SqlConnection(masterConnectionString);
        try
        {
            connection.Open();
            var result = ExecuteRunLock(connection, staleRunId, "Exclusive");
            if (result >= 0)
                return connection;

            connection.Dispose();
            if (result == -1)
                return null;

            throw new InvalidOperationException(
                $"Hive.Tests could not establish stale-run ownership safely (sp_getapplock returned {result}).");
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static int ExecuteRunLock(
        SqlConnection connection,
        Guid runId,
        string lockMode)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @LockResult int;
            EXEC @LockResult = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = @LockMode,
                @LockOwner = N'Session',
                @LockTimeout = 0,
                @DbPrincipal = N'public';
            SELECT @LockResult;
            """;
        command.Parameters.Add(
            new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
            {
                Value = $"Hive.Tests.Run.{runId:N}"
            });
        command.Parameters.Add(
            new SqlParameter("@LockMode", SqlDbType.VarChar, 32)
            {
                Value = lockMode
            });

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static void RecoverStaleOwnedDatabases()
    {
        var masterConnectionString = CreateMasterConnectionString();
        var candidates = new List<string>();

        using (var connection = new SqlConnection(masterConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT [name]
                FROM [sys].[databases]
                WHERE [name] LIKE N'Hive_TestOwned[_]%'
                  AND [database_id] > 4;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var databaseName = reader.GetString(0);
                if (IsStaleOwnedName(
                        databaseName,
                        DateTimeOffset.UtcNow,
                        StaleDatabaseAge))
                {
                    candidates.Add(databaseName);
                }
            }
        }

        foreach (var databaseName in candidates)
        {
            try
            {
                var targetBuilder = new SqlConnectionStringBuilder(
                    HivePersistenceTestConfiguration.ConnectionString)
                {
                    InitialCatalog = databaseName,
                    ApplicationName = "Hive.Tests stale recovery",
                    Pooling = false,
                    ConnectTimeout = 5
                };

                var marker = ReadOwnershipMarker(
                    targetBuilder.ConnectionString);

                // A reserved-looking name alone is not enough. Keep the database
                // untouched unless its marker agrees with the run/token/timestamp
                // encoded in the generated physical name.
                if (!OwnershipMarkerMatchesName(databaseName, marker))
                {
                    Console.Error.WriteLine(
                        $"Hive.Tests stale database recovery left '{databaseName}' untouched because its ownership marker was missing or inconsistent.");
                    continue;
                }

                var markerParts = marker!.Split('|');
                if (!Guid.TryParseExact(markerParts[1], "N", out var ownerRunId))
                {
                    Console.Error.WriteLine(
                        $"Hive.Tests stale database recovery left '{databaseName}' untouched because its run identity was invalid.");
                    continue;
                }

                using var staleRunLock = TryAcquireStaleRunLock(
                    masterConnectionString,
                    ownerRunId);
                if (staleRunLock is null)
                {
                    Console.Error.WriteLine(
                        $"Hive.Tests stale database recovery left '{databaseName}' untouched because its owning test run is still active.");
                    continue;
                }

                DropDatabaseWithoutMarkerCheck(
                    masterConnectionString,
                    databaseName);
            }
            catch (Exception exception)
            {
                ReportCleanupFailure(databaseName, exception);
            }
        }
    }

    private static string SanitizeLabel(string logicalName)
    {
        var sanitized = Regex.Replace(
                logicalName,
                @"[^A-Za-z0-9_]",
                "_",
                RegexOptions.CultureInvariant)
            .Trim('_');

        if (sanitized.Length == 0)
            sanitized = "Database";

        return sanitized.Length <= 40 ? sanitized : sanitized[..40];
    }

    private static string CreateOwnershipMarker(
        Guid runId,
        Guid ownershipToken,
        string timestamp) =>
        $"v1|{runId:N}|{ownershipToken:N}|{timestamp}";

    private static string CreateMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(
            HivePersistenceTestConfiguration.ConnectionString)
        {
            InitialCatalog = "master",
            ApplicationName = "Hive.Tests database lifecycle",
            Pooling = false
        };

        return builder.ConnectionString;
    }

    private static void CreateDatabase(
        string masterConnectionString,
        string databaseName)
    {
        using var connection = new SqlConnection(masterConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)};";
        command.ExecuteNonQuery();
    }

    private static void WriteOwnershipMarker(
        string masterConnectionString,
        string databaseName,
        string marker)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName,
            ApplicationName = "Hive.Tests ownership registration",
            Pooling = false
        };

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            EXEC sys.sp_addextendedproperty
                @name = @PropertyName,
                @value = @Marker;
            """;
        command.Parameters.Add(
            new SqlParameter("@PropertyName", SqlDbType.NVarChar, 128)
            {
                Value = OwnershipPropertyName
            });
        command.Parameters.Add(
            new SqlParameter("@Marker", SqlDbType.NVarChar, 256)
            {
                Value = marker
            });
        command.ExecuteNonQuery();
    }

    private static async Task<bool> DatabaseExistsAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@DatabaseName);";
        command.Parameters.Add(
            new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128)
            {
                Value = databaseName
            });

        var result = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        return result is not null && result is not DBNull;
    }

    private static async Task<string?> ReadOwnershipMarkerAsync(
        string databaseConnectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(databaseConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONVERT(nvarchar(256), [value])
            FROM sys.extended_properties
            WHERE [class] = 0
              AND [name] = @PropertyName;
            """;
        command.Parameters.Add(
            new SqlParameter("@PropertyName", SqlDbType.NVarChar, 128)
            {
                Value = OwnershipPropertyName
            });

        var value = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        return value as string;
    }

    private static string? ReadOwnershipMarker(string databaseConnectionString)
    {
        using var connection = new SqlConnection(databaseConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONVERT(nvarchar(256), [value])
            FROM sys.extended_properties
            WHERE [class] = 0
              AND [name] = @PropertyName;
            """;
        command.Parameters.Add(
            new SqlParameter("@PropertyName", SqlDbType.NVarChar, 128)
            {
                Value = OwnershipPropertyName
            });

        var value = command.ExecuteScalar();
        return value as string;
    }

    private static async Task DropDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = $"""
            IF DB_ID(@DatabaseName) IS NOT NULL
            BEGIN
                ALTER DATABASE {QuoteIdentifier(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE {QuoteIdentifier(databaseName)};
            END;
            """;
        command.Parameters.Add(
            new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128)
            {
                Value = databaseName
            });

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void DropDatabaseWithoutMarkerCheck(
        string masterConnectionString,
        string databaseName)
    {
        using var connection = new SqlConnection(masterConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = $"""
            IF DB_ID(@DatabaseName) IS NOT NULL
            BEGIN
                ALTER DATABASE {QuoteIdentifier(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE {QuoteIdentifier(databaseName)};
            END;
            """;
        command.Parameters.Add(
            new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128)
            {
                Value = databaseName
            });
        command.ExecuteNonQuery();
    }

    private static string QuoteIdentifier(string databaseName) =>
        $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void ReportCleanupFailure(
        string databaseName,
        Exception exception)
    {
        var sqlNumber = exception is SqlException sqlException
            ? $" SQL error {sqlException.Number}."
            : string.Empty;

        Console.Error.WriteLine(
            $"Hive.Tests database cleanup failed for '{databaseName}'.{sqlNumber} Exception: {exception.GetType().Name}. The database was not treated as safely deleted.");
    }

    internal sealed record OwnedDatabase(
        string DatabaseName,
        string LogicalName,
        Guid RunId,
        Guid OwnershipToken,
        string CreatedAtTimestamp,
        string OwnershipMarker,
        string ConnectionString);
}
