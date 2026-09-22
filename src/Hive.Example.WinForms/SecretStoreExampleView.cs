using System.Security.Cryptography;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class SecretStoreExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly HiveDatabaseOptions _databaseOptions;
    private readonly IHiveExampleOutput _output;

    public SecretStoreExampleView(
        HiveDatabaseOptions databaseOptions,
        IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(output);

        _databaseOptions = databaseOptions;
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run secret store"
        };

        _surface.SetInformation(
            "Creates a DPAPI-protected secret through the public ISecretStore boundary, reads it without printing the value, replaces it with a new value, checks authorization, and hard-deletes it.",
            "The output shows versions, redaction behavior, authorization failures, and post-delete NotFound without exposing secret material.",
            "Security",
            "A fresh random test value is generated at runtime; it is never written to source control or example output.");

        _surface.CodeSnippet = """
            var store = new SqlDpapiSecretStore(
                HiveDatabaseOptions.LocalDevelopment());

            using var material = SecretMaterial.Create(input);
            await store.CreateAsync(
                secret,
                material,
                accessContext,
                cancellationToken);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(
        CancellationToken cancellationToken)
    {
        var migration = await new HiveDatabaseMigrator(
                _databaseOptions)
            .MigrateAsync(cancellationToken);

        if (migration.IsFailure)
        {
            throw new InvalidOperationException(
                $"Hive database migration failed: {migration.Error?.Message}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var store = new SqlDpapiSecretStore(_databaseOptions);

        var now = DateTimeOffset.UtcNow;
        var secret = new Secret(
            new ResourceEnvelope<SecretId>(
                ResourceKind.Secret,
                SecretId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            $"example-secret-{Guid.NewGuid():N}",
            "Example DPAPI Secret");

        var originalBytes = RandomNumberGenerator.GetBytes(32);
        var replacementBytes = RandomNumberGenerator.GetBytes(32);

        try
        {
            using var originalMaterial =
                SecretMaterial.Create(
                    Convert.ToBase64String(originalBytes));

            var created = await store.CreateAsync(
                secret,
                originalMaterial,
                context,
                cancellationToken);

            EnsureSuccess(created, "Secret creation");

            var loaded = await store.GetAsync(
                secret.Id,
                context,
                cancellationToken);

            EnsureSuccess(loaded, "Secret read");

            using (loaded.Value!.Material)
            {
                if (loaded.Value.Material.ToString() != "[REDACTED]")
                {
                    throw new InvalidOperationException(
                        "Secret material redaction failed.");
                }

                if (loaded.Value.Material.Reveal() == originalMaterial.Reveal())
                {
                    _surface.InputText =
                        "Secret round-trip verified without printing the secret.";
                }
            }

            var ownerCheck = await store.GetDescriptorAsync(
                secret.Id,
                new ResourceAccessContext(
                    context.DeploymentId,
                    tenant,
                    PrincipalId.New()),
                cancellationToken);

            EnsureForbidden(
                ownerCheck,
                "Owner isolation");

            var scopeCheck = await store.GetDescriptorAsync(
                secret.Id,
                new ResourceAccessContext(
                    context.DeploymentId,
                    TenantId.New(),
                    principal),
                cancellationToken);

            EnsureForbidden(
                scopeCheck,
                "Scope isolation");

            using var replacementMaterial =
                SecretMaterial.Create(
                    Convert.ToBase64String(replacementBytes));

            var replaced = await store.ReplaceAsync(
                secret.Id,
                replacementMaterial,
                context,
                ResourceVersion.Initial,
                cancellationToken);

            EnsureSuccess(replaced, "Secret replacement");

            var deleted = await store.DeleteAsync(
                secret.Id,
                context,
                cancellationToken);

            EnsureSuccess(deleted, "Secret deletion");

            var afterDelete = await store.GetDescriptorAsync(
                secret.Id,
                context,
                cancellationToken);

            if (afterDelete.IsSuccess ||
                afterDelete.Error?.Category != ErrorCategory.NotFound)
            {
                throw new InvalidOperationException(
                    "Post-delete secret lookup did not return NotFound.");
            }

            _output.Write(
                "DPAPI Secret Store Example",
                $"""
                Migration: {migration.Value!.Status}; schema {migration.Value.CurrentSchemaVersion}
                Created secret: {secret.Id}; version {created.Value!.Resource.Version}
                Redaction: {loaded.Value!.Material}; value never printed
                Ownership check: Forbidden
                Scope check: Forbidden
                Replaced secret version: {replaced.Value!.Resource.Version}
                Deleted secret: success
                After delete: NotFound
                """);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalBytes);
            CryptographicOperations.ZeroMemory(replacementBytes);
        }
    }

    private static void EnsureForbidden<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsSuccess ||
            result.Error?.Category != ErrorCategory.Forbidden)
        {
            throw new InvalidOperationException(
                $"{operation} did not return Forbidden.");
        }
    }

    private static void EnsureSuccess(
        Result result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }
}
