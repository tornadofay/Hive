using System.Data.Common;
using Hive.Core;

namespace Hive.Persistence;

internal interface IHiveSecretStoreMigrationWriter
{
    Task<Result> ImportForMigrationAsync(
        DbConnection connection,
        DbTransaction transaction,
        Secret secret,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}
