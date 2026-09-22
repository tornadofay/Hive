using Hive.Core;

namespace Hive.Host.WinForms;

public sealed class UnavailableHiveBootstrapCredentialStore :
    IHiveBootstrapCredentialStore
{
    public Task<Result> SetAsync(
        HiveBootstrapCredentialReference reference,
        SecretMaterial material,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            Result.Failure(
                Error.Unsupported(
                    "hive.host.bootstrap-credential-store-unavailable",
                    "The Hive bootstrap credential store is not available in this host composition.")));
    }

    public Task<Result<SecretMaterial>> ResolveAsync(
        HiveBootstrapCredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            Result<SecretMaterial>.Failure(
                Error.Unsupported(
                    "hive.host.bootstrap-credential-store-unavailable",
                    "The Hive bootstrap credential store is not available in this host composition.")));
    }

    public Task<Result> ClearAsync(
        HiveBootstrapCredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            Result.Failure(
                Error.Unsupported(
                    "hive.host.bootstrap-credential-store-unavailable",
                    "The Hive bootstrap credential store is not available in this host composition.")));
    }
}
