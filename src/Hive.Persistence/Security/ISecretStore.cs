using Hive.Core;

namespace Hive.Persistence;

public interface ISecretStore
{
    Task<Result<Secret>> CreateAsync(
        Secret secret,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<SecretReadResult>> GetAsync(
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<Secret>> ReplaceAsync(
        SecretId id,
        SecretMaterial replacement,
        ResourceAccessContext accessContext,
        ResourceVersion expectedVersion,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<Secret>> GetDescriptorAsync(
        SecretId id,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}
