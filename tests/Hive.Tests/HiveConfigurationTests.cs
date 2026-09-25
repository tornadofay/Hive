using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class HiveConfigurationTests
{
    [Fact]
    public void PersistenceConfiguration_RejectsBootstrapCredentialForWindowsAuthentication()
    {
        Assert.Throws<ArgumentException>(
            () => new HivePersistenceConfiguration(
                HivePersistenceBackend.SqlServer,
                "sql.example.test",
                1433,
                "Hive",
                HiveSqlAuthenticationMode.WindowsIntegrated,
                null,
                new HiveBootstrapCredentialReference(SecretId.New()),
                encrypt: true,
                trustServerCertificate: false,
                createDatabaseIfMissing: true));
    }

    [Fact]
    public void HiveBootstrapCredentialReference_RejectsDefaultIdentity()
    {
        Assert.Throws<ArgumentException>(
            () => new HiveBootstrapCredentialReference(default));
    }

    [Fact]
    public void HivePersistenceConfiguration_RejectsMalformedBootstrapReference()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new HivePersistenceConfiguration(
                HivePersistenceBackend.SqlServer,
                "sql.example.test",
                1433,
                "HiveProduction",
                HiveSqlAuthenticationMode.SqlPassword,
                "hive-user",
                (HiveBootstrapCredentialReference?)default(HiveBootstrapCredentialReference),
                encrypt: true,
                trustServerCertificate: false,
                createDatabaseIfMissing: false));

        Assert.Contains(
            "Bootstrap credential reference",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDatabaseName_ProducesBoundedDatabaseName()
    {
        var result = HivePersistenceConfiguration.BuildDatabaseName(
            new string('a', 10_000));

        Assert.Equal(128, result.Length);
        Assert.Equal(
            "Hive-" + new string('a', 123),
            result);
    }

    [Fact]
    public void BuildDatabaseName_UsesHiveApplicationPrefix()
    {
        Assert.Equal(
            "Hive-Hive.Example.WinForms",
            HivePersistenceConfiguration.BuildDatabaseName(
                "Hive.Example.WinForms"));
    }

    [Fact]
    public async Task JsonConfigurationStore_InvalidJson_DoesNotExposeParserDetails()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"hive-invalid-settings-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                "{ \"backend\": ");

            var result = await new JsonHiveConfigurationStore(filePath)
                .LoadPersistenceConfigurationAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.management.configuration-invalid",
                result.Error!.Code);
            Assert.Equal(
                "The Hive settings file is invalid.",
                result.Error.Message);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task JsonConfigurationStore_InvalidConfigurationValues_AreValidationFailures()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"hive-invalid-settings-values-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                """
                {
                  "backend": 999,
                  "serverName": "sql.example.test",
                  "databaseName": "Hive",
                  "authenticationMode": 0,
                  "encrypt": true,
                  "trustServerCertificate": false,
                  "createDatabaseIfMissing": false,
                  "commandTimeoutSeconds": 30
                }
                """);

            var result = await new JsonHiveConfigurationStore(filePath)
                .LoadPersistenceConfigurationAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.management.configuration-invalid",
                result.Error!.Code);
            Assert.Equal(ErrorCategory.Validation, result.Error.Category);
            Assert.Equal(
                "The Hive settings file contains invalid configuration values.",
                result.Error.Message);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task JsonConfigurationStore_CanReplaceSettingsWhileAReaderHasTheOldFileOpen()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"hive-settings-replace-{Guid.NewGuid():N}.json");

        try
        {
            var store = new JsonHiveConfigurationStore(filePath);
            var initial = HivePersistenceConfiguration.LocalDevelopmentForApplication(
                "Hive.Tests");

            var firstSave = await store.SavePersistenceConfigurationAsync(initial);
            Assert.True(firstSave.IsSuccess, firstSave.Error?.Message);

            await using var reader = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            var replacement = new HivePersistenceConfiguration(
                HivePersistenceBackend.SqlServer,
                "sql.example.test",
                1433,
                "HiveProduction",
                HiveSqlAuthenticationMode.WindowsIntegrated,
                null,
                null,
                encrypt: true,
                trustServerCertificate: false,
                createDatabaseIfMissing: false,
                commandTimeoutSeconds: 45);

            var saved = await store.SavePersistenceConfigurationAsync(replacement);

            Assert.True(saved.IsSuccess, saved.Error?.Message);
            Assert.Equal(replacement, saved.Value);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Management_CreateSecret_InvalidDefinition_UsesStablePublicError()
    {
        var options = HiveDatabaseOptions.LocalDevelopment();
        var management = new HiveManagementFacade(
            new SqlProviderResourceStore(options),
            new SqlAgentDefinitionResourceStore(options),
            new SqlWorkItemResourceStore(options),
            secrets: new SqlDpapiSecretStore(options));
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        using var material = SecretMaterial.Create("valid-secret-material");

        var result = await management.CreateSecretAsync(
            new string('k', 101),
            "Valid Secret",
            material,
            context);

        Assert.True(result.IsFailure);
        Assert.Equal("hive.management.secret-invalid", result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
        Assert.Equal(
            "The Hive secret definition is invalid.",
            result.Error.Message);
    }

    [Fact]
    public async Task Management_InitializePersistence_CreatesAndMigratesHiveDatabase()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementInitialization");
        database.Reset();

        var facade = new HiveManagementFacade(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options));

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var configuration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            database.Options.ServerName,
            null,
            database.Options.DatabaseName,
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            encrypt: false,
            trustServerCertificate: true,
            createDatabaseIfMissing: true);

        var result = await facade.InitializePersistenceAsync(
            configuration,
            context);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var state = await new HivePersistenceConnectionTester()
            .TestAsync(
                configuration,
                credential: null);

        Assert.True(state.IsSuccess, state.Error?.Message);
        Assert.Equal(HiveDatabaseState.Current, state.Value!.DatabaseState);
        Assert.Equal(
            HiveDatabaseSchema.CurrentSchemaVersion,
            state.Value.CurrentSchemaVersion);
    }

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
                new HiveBootstrapCredentialReference(secretId),
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
            Assert.Contains("bootstrapCredentialId", json, StringComparison.Ordinal);
            Assert.DoesNotContain("credentialSecretId", json, StringComparison.Ordinal);
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
    public async Task Management_LoadLegacyCredentialReference_RejectsSilentReinterpretation()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"hive-settings-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                """
                {
                  "backend": 0,
                  "serverName": "sql.example.test",
                  "port": 1433,
                  "databaseName": "HiveProduction",
                  "authenticationMode": 1,
                  "userName": "hive-user",
                  "credentialSecretId": "11111111-1111-1111-1111-111111111111",
                  "encrypt": true,
                  "trustServerCertificate": false,
                  "createDatabaseIfMissing": false,
                  "commandTimeoutSeconds": 45
                }
                """);

            var result = await new JsonHiveConfigurationStore(filePath)
                .LoadPersistenceConfigurationAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.management.legacy-bootstrap-credential-reference",
                result.Error!.Code);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Management_TestExecutionTargetConnection_LoadsProviderGraphAndInvokesTester()
    {
        var database = new PersistenceTestDatabase("Hive_Test_ManagementProviderConnection");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var tester = new RecordingProviderConnectionTester();
        var facade = new HiveManagementFacade(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            providerConnectionTester: tester);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var now = DateTimeOffset.UtcNow;
        var provider = new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "management-connection-provider",
            "Management Connection Provider",
            "openai-compatible");

        var providerCreated = await facade.CreateProviderAsync(
            provider,
            context);
        Assert.True(providerCreated.IsSuccess, providerCreated.Error?.Message);

        var account = new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context.PrincipalId.Value,
                ResourceScope.Tenant(context.TenantId.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            "management-connection-account",
            "Management Connection Account");

        var accountCreated = await facade.CreateProviderAccountAsync(
            account,
            context);
        Assert.True(accountCreated.IsSuccess, accountCreated.Error?.Message);

        var target = new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context.PrincipalId.Value,
                ResourceScope.Tenant(context.TenantId.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            account.Id,
            "management-connection-target",
            "Management Connection Target",
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);

        var targetCreated = await facade.CreateExecutionTargetAsync(
            target,
            context);
        Assert.True(targetCreated.IsSuccess, targetCreated.Error?.Message);

        var result = await facade.TestExecutionTargetConnectionAsync(
            target.Id,
            context);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(tester.Called);
        Assert.Equal(provider.Id, tester.ProviderId);
        Assert.Equal(account.Id, tester.AccountId);
        Assert.Equal(target.Id, tester.TargetId);
        Assert.Null(tester.Credential);
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

    private sealed class RecordingProviderConnectionTester
        : IProviderConnectionTester
    {
        public bool Called { get; private set; }

        public ProviderId ProviderId { get; private set; }

        public ProviderAccountId AccountId { get; private set; }

        public ExecutionTargetId TargetId { get; private set; }

        public SecretMaterial? Credential { get; private set; }

        public Task<Result<ProviderConnectionTestResult>> TestAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            ProviderId = provider.Id;
            AccountId = account.Id;
            TargetId = target.Id;
            Credential = credential;

            return Task.FromResult(
                Result<ProviderConnectionTestResult>.Success(
                    new ProviderConnectionTestResult(
                        provider.Key,
                        target.Key,
                        target.Model ?? target.Deployment ?? string.Empty,
                        TimeSpan.FromMilliseconds(1),
                        "Fake provider connection test.")));
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
