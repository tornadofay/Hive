using Hive.Core;
using Microsoft.Data.Sqlite;

namespace Hive.Persistence;

internal static class EmbeddedPersistenceError
{
    public static Error From(
        string operationCode,
        string operation,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(operationCode);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode switch
            {
                3 or 8 or 10 or 14 =>
                    HivePersistenceError.External(
                        $"{operationCode}.storage-access",
                        $"The Embedded persistence {operation} could not access the configured storage.",
                        exception),

                5 or 6 =>
                    HivePersistenceError.External(
                        $"{operationCode}.storage-busy",
                        $"The Embedded persistence {operation} could not complete because the storage is busy or locked.",
                        exception),

                11 or 26 =>
                    HivePersistenceError.External(
                        $"{operationCode}.storage-corrupt",
                        $"The Embedded persistence {operation} could not use the storage because it is corrupt or is not a valid database file.",
                        exception),

                13 =>
                    HivePersistenceError.External(
                        $"{operationCode}.storage-full",
                        $"The Embedded persistence {operation} could not complete because the storage is full.",
                        exception),

                _ =>
                    HivePersistenceError.External(
                        $"{operationCode}.sqlite-failure",
                        $"The Embedded persistence {operation} failed at the SQLite boundary.",
                        exception)
            };
        }

        if (exception is UnauthorizedAccessException or IOException)
        {
            return HivePersistenceError.External(
                $"{operationCode}.storage-access",
                $"The Embedded persistence {operation} could not access the configured storage.",
                exception);
        }

        return HivePersistenceError.Internal(
            $"{operationCode}.unexpected",
            $"The Embedded persistence {operation} failed unexpectedly.",
            exception);
    }

    public static Error InvalidMetadata() =>
        new Error(
            "hive.persistence.embedded.metadata-inconsistent",
            ErrorCategory.External,
            "The Embedded persistence metadata is inconsistent or incomplete.");

    public static Error StorageNotEmpty() =>
        new Error(
            "hive.persistence.embedded.storage-not-empty",
            ErrorCategory.External,
            "The configured Embedded storage already contains non-Hive application tables.");

    public static Error StorageNotFound() =>
        new(
            "hive.persistence.embedded.storage-not-found",
            ErrorCategory.NotFound,
            "The configured Embedded storage file does not exist.");

    public static Error FutureSchema(int version) =>
        Error.Unsupported(
            "hive.persistence.embedded.future-schema",
            $"The Embedded persistence schema version {version} is newer than the supported version {EmbeddedPersistenceSchema.CurrentSchemaVersion}.");
}
