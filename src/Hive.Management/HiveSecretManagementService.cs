using Hive.Core;

using Hive.Persistence;



namespace Hive.Management;



internal sealed class HiveSecretManagementService : HiveManagementServiceBase

{

    private readonly ISecretStore? _secrets



    internal HiveSecretManagementService(ISecretStore? secrets)

    {

        _secrets = secrets;

    }



    internal Task<Result<Secret>> CreateSecretAsync(
        string key,
        string displayName,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Failure<Secret>(contextError);

        if (_secrets is null)
        {
            return Failure<Secret>(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        if (accessContext.TenantId is null)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret-tenant-required",
                    "A tenant identity is required to create a Hive secret."));
        }

        var now = DateTimeOffset.UtcNow;
        Secret secret;

        try
        {
            secret = new Secret(
                new ResourceEnvelope<SecretId>(
                    ResourceKind.Secret,
                    SecretId.New(),
                    accessContext.PrincipalId!.Value,
                    ResourceScope.Tenant(accessContext.TenantId.Value),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        accessContext.PrincipalId.Value,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                key,
                displayName);
        }
        catch (ArgumentException)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret-invalid",
                    "The Hive secret definition is invalid."));
        }

        return _secrets.CreateAsync(
            secret,
            material,
            accessContext,
            cancellationToken);
    }


    internal Task<Result<Secret>> GetSecretDescriptorAsync(
        SecretId secretId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Failure<Secret>(contextError);

        if (secretId == default)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret.identity-required",
                    "The secret identity is required."));
        }

        if (_secrets is null)
        {
            return Failure<Secret>(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        return _secrets.GetDescriptorAsync(
            secretId,
            accessContext,
            cancellationToken);
    }


    internal Task<Result<Secret>> ReplaceSecretAsync(
        SecretId secretId,
        SecretMaterial replacement,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Failure<Secret>(contextError);

        if (secretId == default)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret.identity-required",
                    "The secret identity is required."));
        }

        if (expectedVersion.Value <= 0)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret.version-invalid",
                    "A positive secret version is required."));
        }

        if (_secrets is null)
        {
            return Failure<Secret>(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        return _secrets.ReplaceAsync(
            secretId,
            replacement,
            accessContext,
            expectedVersion,
            cancellationToken);
    }


}