using Hive.Core;

namespace Hive.Management;

public interface IHiveConfigurationStore
{
    Task<Result<HivePersistenceConfiguration>> LoadPersistenceConfigurationAsync(
        CancellationToken cancellationToken = default);

    Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
        HivePersistenceConfiguration configuration,
        CancellationToken cancellationToken = default);
}
