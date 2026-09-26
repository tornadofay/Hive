namespace Hive.Core;

public interface IHivePersistenceConnectionTester
{
    Task<Result<HivePersistenceConnectionTest>> TestAsync(
        HivePersistenceConfiguration configuration,
        SecretMaterial? credential,
        CancellationToken cancellationToken = default);
}
