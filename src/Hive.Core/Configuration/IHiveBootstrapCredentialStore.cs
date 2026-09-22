using Hive.Core;

namespace Hive.Core;

public interface IHiveBootstrapCredentialStore
{
    Task<Result> SetAsync(
        HiveBootstrapCredentialReference reference,
        SecretMaterial material,
        CancellationToken cancellationToken = default);

    Task<Result<SecretMaterial>> ResolveAsync(
        HiveBootstrapCredentialReference reference,
        CancellationToken cancellationToken = default);

    Task<Result> ClearAsync(
        HiveBootstrapCredentialReference reference,
        CancellationToken cancellationToken = default);
}
