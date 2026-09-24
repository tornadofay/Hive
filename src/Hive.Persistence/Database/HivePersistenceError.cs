using Hive.Core;

namespace Hive.Persistence;

internal static class HivePersistenceError
{
    public static Error External(
        string code,
        string message,
        Exception exception) =>
        Create(code, ErrorCategory.External, message, exception);

    public static Error Internal(
        string code,
        string message,
        Exception exception) =>
        Create(code, ErrorCategory.Internal, message, exception);

    public static Error Serialization(
        string code,
        string message,
        Exception exception) =>
        Create(code, ErrorCategory.Serialization, message, exception);

    private static Error Create(
        string code,
        ErrorCategory category,
        string message,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new Error(code, category, message);
    }
}
