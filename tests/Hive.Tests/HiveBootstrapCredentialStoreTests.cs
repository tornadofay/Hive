using System.Text;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Management;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class HiveBootstrapCredentialStoreTests
{
    [Fact]
    public async Task DpapiStore_RoundTrips_AndDoesNotPersistPlaintext()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var storage = TemporaryDirectory.Create();
        var store = new DpapiHiveBootstrapCredentialStore(storage.Path);
        var reference = new HiveBootstrapCredentialReference(SecretId.New());

        using var material = SecretMaterial.Create("bootstrap-secret-1");

        var saved = await store.SetAsync(reference, material);

        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var files = Directory.GetFiles(storage.Path);
        Assert.Single(files);

        var raw = await File.ReadAllBytesAsync(files[0]);
        var rawHex = Convert.ToHexString(raw);
        var secretHex = Convert.ToHexString(
            Encoding.UTF8.GetBytes("bootstrap-secret-1"));

        Assert.DoesNotContain(secretHex, rawHex, StringComparison.OrdinalIgnoreCase);

        var resolved = await store.ResolveAsync(reference);

        Assert.True(resolved.IsSuccess, resolved.Error?.Message);

        using var resolvedMaterial = resolved.Value!;
        Assert.Equal("bootstrap-secret-1", resolvedMaterial.Reveal());
    }

    [Fact]
    public async Task DpapiStore_ReplacesAndClearsCredential()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var storage = TemporaryDirectory.Create();
        var store = new DpapiHiveBootstrapCredentialStore(storage.Path);
        var reference = new HiveBootstrapCredentialReference(SecretId.New());

        using var first = SecretMaterial.Create("first-secret");
        using var second = SecretMaterial.Create("second-secret");

        Assert.True(
            (await store.SetAsync(reference, first)).IsSuccess);

        Assert.True(
            (await store.SetAsync(reference, second)).IsSuccess);

        var resolved = await store.ResolveAsync(reference);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);

        using var resolvedMaterial = resolved.Value!;
        Assert.Equal("second-secret", resolvedMaterial.Reveal());

        var cleared = await store.ClearAsync(reference);
        Assert.True(cleared.IsSuccess, cleared.Error?.Message);

        var missing = await store.ResolveAsync(reference);

        Assert.True(missing.IsFailure);
        Assert.Equal(
            "hive.host.bootstrap-credential-not-found",
            missing.Error!.Code);
    }

    [Fact]
    public async Task DpapiStore_ReportsCorruptEncryptedStateWithoutLeakingMaterial()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var storage = TemporaryDirectory.Create();
        var store = new DpapiHiveBootstrapCredentialStore(storage.Path);
        var reference = new HiveBootstrapCredentialReference(SecretId.New());

        using var material = SecretMaterial.Create("secret-never-in-error");

        Assert.True(
            (await store.SetAsync(reference, material)).IsSuccess);

        var file = Assert.Single(Directory.GetFiles(storage.Path));
        await File.WriteAllBytesAsync(
            file,
            [1, 2, 3, 4, 5, 6, 7, 8]);

        var result = await store.ResolveAsync(reference);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.bootstrap-credential-decrypt-failed",
            result.Error!.Code);
        Assert.DoesNotContain(
            "secret-never-in-error",
            result.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Management_BootstrapCredentialLifecycle_ProtectsReferencedCredential()
    {
        var configurationPath = Path.Combine(
            Path.GetTempPath(),
            $"hive-bootstrap-settings-{Guid.NewGuid():N}.json");

        try
        {
            var configurationStore =
                new JsonHiveConfigurationStore(configurationPath);
            var bootstrapStore =
                new InMemoryBootstrapCredentialStore();
            var options = HiveDatabaseOptions.LocalDevelopment();

            var management = new HiveManagementFacade(
                new SqlProviderResourceStore(options),
                new SqlAgentDefinitionResourceStore(options),
                new SqlWorkItemResourceStore(options),
                configurationStore: configurationStore,
                bootstrapCredentials: bootstrapStore);

            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            using var material = SecretMaterial.Create("management-secret");

            var saved = await management.SaveBootstrapCredentialAsync(
                material,
                existingReference: null,
                context);

            Assert.True(saved.IsSuccess, saved.Error?.Message);

            var reference = saved.Value;

            var passwordConfiguration = new HivePersistenceConfiguration(
                HivePersistenceBackend.SqlServer,
                "sql.example.test",
                1433,
                "Hive",
                HiveSqlAuthenticationMode.SqlPassword,
                "hive-user",
                reference,
                encrypt: true,
                trustServerCertificate: false,
                createDatabaseIfMissing: false);

            var saveConfiguration =
                await management.SavePersistenceConfigurationAsync(
                    passwordConfiguration,
                    context);

            Assert.True(
                saveConfiguration.IsSuccess,
                saveConfiguration.Error?.Message);

            var settingsJson = await File.ReadAllTextAsync(configurationPath);
            Assert.Contains(
                reference.Id.Value.ToString(),
                settingsJson,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "management-secret",
                settingsJson,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "credentialSecretId",
                settingsJson,
                StringComparison.Ordinal);

            var protectedDelete =
                await management.RemoveBootstrapCredentialAsync(
                    reference,
                    context);

            Assert.True(protectedDelete.IsFailure);
            Assert.Equal(
                "hive.management.bootstrap-credential-still-referenced",
                protectedDelete.Error!.Code);

            var integrated =
                HivePersistenceConfiguration.LocalDevelopment();

            var replaceConfiguration =
                await management.SavePersistenceConfigurationAsync(
                    integrated,
                    context);

            Assert.True(
                replaceConfiguration.IsSuccess,
                replaceConfiguration.Error?.Message);

            var delete =
                await management.RemoveBootstrapCredentialAsync(
                    reference,
                    context);

            Assert.True(delete.IsSuccess, delete.Error?.Message);
            Assert.False(bootstrapStore.Contains(reference));
        }
        finally
        {
            if (File.Exists(configurationPath))
                File.Delete(configurationPath);
        }
    }

    private sealed class InMemoryBootstrapCredentialStore :
        IHiveBootstrapCredentialStore
    {
        private readonly Dictionary<Guid, string> _values = [];

        public bool Contains(
            HiveBootstrapCredentialReference reference) =>
            _values.ContainsKey(reference.Id.Value);

        public Task<Result> SetAsync(
            HiveBootstrapCredentialReference reference,
            SecretMaterial material,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[reference.Id.Value] = material.Reveal();

            return Task.FromResult(Result.Success());
        }

        public Task<Result<SecretMaterial>> ResolveAsync(
            HiveBootstrapCredentialReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _values.TryGetValue(
                    reference.Id.Value,
                    out var value)
                    ? Result<SecretMaterial>.Success(
                        SecretMaterial.Create(value))
                    : Result<SecretMaterial>.Failure(
                        Error.NotFound(
                            "hive.host.bootstrap-credential-not-found",
                            "The referenced bootstrap credential was not found.")));
        }

        public Task<Result> ClearAsync(
            HiveBootstrapCredentialReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.Remove(reference.Id.Value);

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(path);
        }

        public string Path { get; }

        public static TemporaryDirectory Create() =>
            new(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "HiveBootstrapCredentialStoreTests",
                Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
