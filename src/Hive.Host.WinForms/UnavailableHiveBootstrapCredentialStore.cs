using Hive.Core;

namespace Hive.Host.WinForms;

public sealed class UnavailableHiveBootstrapCredentialStore :
    IHiveBootstrapCredentialStore
{
    public Task<Result<SecretMaterial>> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            Result<SecretMaterial>.Failure(
                Error.Unsupported(
                    "hive.host.bootstrap-credential-store-unavailable",
                    "The Hive bootstrap credential store is not available in this host composition.")));
    }
}
