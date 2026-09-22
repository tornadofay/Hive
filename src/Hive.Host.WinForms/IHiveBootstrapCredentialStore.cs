using Hive.Core;

namespace Hive.Host.WinForms;

public interface IHiveBootstrapCredentialStore
{
    Task<Result<SecretMaterial>> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default);
}
