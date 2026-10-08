            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task ManagementMigration_RequiresQuiescenceAndLeavesDestinationInactive()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_Management_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("management");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceConfiguration = CreateSqlConfiguration(sourceDatabaseName);
            // Deliberately point the persisted active configuration somewhere else.
            // Management migration must use the explicit request source instead.
            var configurationStore = new TestConfigurationStore(
                HivePersistenceConfiguration.LocalDevelopment(
                    $"Hive_Not_The_Migration_Source_{Guid.NewGuid():N}"));
            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            using (var unavailable = new HiveManagementFacade(
                       new SqlProviderResourceStore(sourceOptions),
                       new SqlAgentDefinitionResourceStore(sourceOptions),
                       new SqlWorkItemResourceStore(sourceOptions),
                       configurationStore: configurationStore))
            {
                var unavailableResult = await unavailable.MigratePersistenceDataAsync(
                    new HivePersistenceMigrationRequest(
                        sourceConfiguration,
                        HivePersistenceConfiguration.Embedded(embeddedPath)),
                    context);

                Assert.False(unavailableResult.IsSuccess);
                Assert.Equal(
                    "hive.management.persistence-migration-quiescence-unavailable",
                    unavailableResult.Error!.Code);
            }

            var quiescence = new RecordingQuiescence();
            using var management = new HiveManagementFacade(
                new SqlProviderResourceStore(sourceOptions),
                new SqlAgentDefinitionResourceStore(sourceOptions),
                new SqlWorkItemResourceStore(sourceOptions),
                configurationStore: configurationStore,
                persistenceMigrationQuiescence: quiescence);

            var result = await management.MigratePersistenceDataAsync(
                new HivePersistenceMigrationRequest(
                    sourceConfiguration,
                    HivePersistenceConfiguration.Embedded(embeddedPath)),
                context);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(
                HivePersistenceMigrationDirection.SqlServerToEmbedded,
                result.Value!.Direction);
            Assert.True(result.Value.DestinationVerified);
            Assert.False(result.Value.DestinationActivated);
            Assert.True(quiescence.Acquired);
            Assert.True(quiescence.Released);
        }
        finally
        {
            await DropSqlDatabaseAsync(sourceDatabaseName);
            TryDeleteDirectory(embeddedDirectory);
        }
    }

    [Fact]
    public async Task ManagementMigration_RejectsUnquiescentSource()
    {
        var sourceDatabaseName = $"Hive_Test_Migration_Busy_{Guid.NewGuid():N}";
        var embeddedDirectory = CreateEmbeddedDirectory("busy");
        var embeddedPath = Path.Combine(embeddedDirectory, "hive.db");

        try
        {
            var sourceOptions = await CreateSqlDatabaseAsync(sourceDatabaseName);
            var sourceConfiguration = CreateSqlConfiguration(sourceDatabaseName);
            var configurationStore = new TestConfigurationStore(
                sourceConfiguration);
            var quiescence = new RecordingQuiescence
            {
                Failure = Error.Conflict(
                    "hive.persistence.data-migration.source-not-quiescent",
                    "The source persistence graph is not quiescent.")
            };

            using var management = new HiveManagementFacade(
                new SqlProviderResourceStore(sourceOptions),
                new SqlAgentDefinitionResourceStore(sourceOptions),
                new SqlWorkItemResourceStore(sourceOptions),
                configurationStore: configurationStore,
                persistenceMigrationQuiescence: quiescence);