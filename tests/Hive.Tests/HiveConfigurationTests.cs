using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class HiveConfigurationTests
{
    [Fact]
    public async Task Management_SaveLoadPersistenceConfiguration_RoundTripsWithoutSecretMaterial()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"hive-settings-{Guid.NewGuid():N}.json");

        try
        {
            var options = HiveDatabaseOptions.LocalDevelopment();
            var store = new JsonHiveConfigurationStore(filePath);
            var facade = new HiveManagementFacade(
                new SqlProviderResourceStore(options),
                new SqlAgentDefinitionResourceStore(options),
                new SqlWorkItemResourceStore(options),
                configurationStore: store);

            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            var secretId = SecretId.New();

            var configuration = new HivePersistenceConfiguration(
                HivePersistenceBackend.SqlServer,
                "sql.example.test",
                1433,
                "HiveProduction",
                HiveSqlAuthenticationMode.SqlPassword,
                "hive-user",
                new SecretReference(secretId),
                encrypt: true,
                trustServerCertificate: false,
                createDatabaseIfMissing: false,
                commandTimeoutSeconds: 45);

            var saved = await facade.SavePersistenceConfigurationAsync(
                configuration,
                context);

            Assert.True(saved.IsSuccess, saved.Error?.Message);

            var loaded = await facade.GetPersistenceConfigurationAsync(context);

            Assert.True(loaded.IsSuccess, loaded.Error?.Message);
            Assert.Equal(configuration, loaded.Value);

            var json = await File.ReadAllTextAsync(filePath);
            Assert.Contains("credentialSecretId", json, StringComparison.Ordinal);
            Assert.Contains(secretId.Value.ToString(), json, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "hive-user-password",
                json,
                StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Management_TestPersistenceConnection_UsesNonDestructiveTester()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"hive-settings-{Guid.NewGuid():N}.json");

        try
        {
            var tester = new RecordingPersistenceConnectionTester();
            var facade = new HiveManagementFacade(
                new SqlProviderResourceStore(
                    HiveDatabaseOptions.LocalDevelopment()),
                new SqlAgentDefinitionResourceStore(
                    HiveDatabaseOptions.LocalDevelopment()),
                new SqlWorkItemResourceStore(
                    HiveDatabaseOptions.LocalDevelopment()),
                configurationStore: new JsonHiveConfigurationStore(filePath),
                persistenceConnectionTester: tester);

            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            var configuration = HivePersistenceConfiguration.LocalDevelopment();

            var result = await facade.TestPersistenceConnectionAsync(
                configuration,
                context);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.True(tester.Called);
            Assert.Equal(configuration, tester.Configuration);
            Assert.Equal(
                HiveDatabaseState.Current,
                result.Value!.DatabaseState);
            Assert.Equal(
                HiveDatabaseSchema.CurrentSchemaVersion,
                result.Value.CurrentSchemaVersion);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private sealed class RecordingPersistenceConnectionTester
        : IHivePersistenceConnectionTester
    {
        public bool Called { get; private set; }

        public HivePersistenceConfiguration? Configuration { get; private set; }

        public Task<Result<HivePersistenceConnectionTest>> TestAsync(
            HivePersistenceConfiguration configuration,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            Configuration = configuration;

            return Task.FromResult(
                Result<HivePersistenceConnectionTest>.Success(
                    new HivePersistenceConnectionTest(
                        true,
                        HiveDatabaseState.Current,
                        HiveDatabaseSchema.CurrentSchemaVersion,
                        HiveDatabaseSchema.CurrentSchemaVersion,
                        "Fake non-destructive persistence test.")));
        }
    }
}
