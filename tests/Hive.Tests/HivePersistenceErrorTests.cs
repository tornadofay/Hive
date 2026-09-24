using Hive.Core;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class HivePersistenceErrorTests
{
    [Fact]
    public void TechnicalExceptionDetails_AreNotReturnedInPersistenceErrors()
    {
        const string secretDetails = "server=sql.example.test;password=super-secret";
        var exception = new InvalidOperationException(secretDetails);

        var external = HivePersistenceError.External(
            "hive.persistence.sql-failure",
            "The SQL Server operation failed.",
            exception);
        var internalError = HivePersistenceError.Internal(
            "hive.persistence.invalid-state",
            "The persistence operation failed unexpectedly.",
            exception);
        var serialization = HivePersistenceError.Serialization(
            "hive.persistence.serialization-failure",
            "The persisted payload is invalid.",
            exception);

        Assert.DoesNotContain(secretDetails, external.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretDetails, internalError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretDetails, serialization.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCategory.External, external.Category);
        Assert.Equal(ErrorCategory.Internal, internalError.Category);
        Assert.Equal(ErrorCategory.Serialization, serialization.Category);
    }
}
