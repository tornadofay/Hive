using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Management;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class HiveHostCompositionTests
{
    [Fact]
    public async Task NoSavedConfiguration_UsesLocalDevelopmentDefault()
    {
        using var settings = TemporarySettingsFile.Create();
        var store = new JsonHiveConfigurationStore(
            settings.Path,
            "Hive.Example.WinForms");
        using var composition = new HiveHostComposition(
            store,
            new SqlHiveHostServiceGraphFactory(
                new UnavailableHiveBootstrapCredentialStore(),
                store));

        var result = await composition.InitializeAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(composition.Current);
        Assert.Equal(
            @"(localdb)\MSSQLLocalDB",
            composition.Current!.PersistenceConfiguration.ServerName);
        Assert.Equal(
            "Hive-Hive.Example.WinForms",
            composition.Current.PersistenceConfiguration.DatabaseName);
        Assert.Equal(
            HiveHostCompositionState.Ready,
            composition.Status.State);
    }

    [Fact]
    public async Task SavedConfiguration_IsConsumedByComposition()
    {
        using var settings = TemporarySettingsFile.Create();
        var store = new JsonHiveConfigurationStore(settings.Path);
        var saved = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            "configured-server",
            1544,
            "HiveConfigured",
            HiveSqlAuthenticationMode.WindowsIntegrated,
            null,
            null,
            encrypt: true,
            trustServerCertificate: false,
            createDatabaseIfMissing: false,
            commandTimeoutSeconds: 47);

        var save = await store.SavePersistenceConfigurationAsync(saved);
        Assert.True(save.IsSuccess, save.Error?.Message);

        using var composition = new HiveHostComposition(
            store,
            new SqlHiveHostServiceGraphFactory(
                new UnavailableHiveBootstrapCredentialStore(),
                store));

        var result = await composition.InitializeAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            saved,
            result.Value!.PersistenceConfiguration);
        Assert.Equal(
            HiveHostCompositionState.Ready,
            composition.Status.State);
    }

    [Fact]
    public async Task PublishedGraph_UsesTheSameConfigurationStoreAsComposition()
    {
        var configuration = HivePersistenceConfiguration.LocalDevelopment(
            "Hive_Composition_SharedStore");
        var store = new InMemoryConfigurationStore(configuration);

        using var composition = new HiveHostComposition(
            store,
            new SqlHiveHostServiceGraphFactory(
                new UnavailableHiveBootstrapCredentialStore(),
                store));

        var initialization = await composition.InitializeAsync();
        Assert.True(initialization.IsSuccess, initialization.Error?.Message);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var loaded = await initialization.Value!.Management
            .GetPersistenceConfigurationAsync(context);

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(configuration, loaded.Value);
    }

    [Fact]
    public async Task InvalidSavedConfiguration_DoesNotFallBackToLocalDevelopment()
    {
        using var settings = TemporarySettingsFile.Create();
        var store = new JsonHiveConfigurationStore(settings.Path);
        await File.WriteAllTextAsync(
            settings.Path,
            "{ \"backend\": \"SqlServer\", \"serverName\": ");

        using var composition = new HiveHostComposition(
            store,
            new SqlHiveHostServiceGraphFactory(
                new UnavailableHiveBootstrapCredentialStore(),
                store));

        var result = await composition.InitializeAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.management.configuration-invalid",
            result.Error!.Code);
        Assert.Null(composition.Current);
        Assert.Equal(
            HiveHostCompositionState.Unavailable,
            composition.Status.State);
    }

    [Fact]
    public async Task SqlPasswordConfiguration_RequiresAvailableBootstrapCredential()
    {
        using var settings = TemporarySettingsFile.Create();
        var store = new JsonHiveConfigurationStore(settings.Path);
        var saved = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            "configured-server",
            1433,
            "HiveConfigured",
            HiveSqlAuthenticationMode.SqlPassword,
            "hive-user",
            new HiveBootstrapCredentialReference(SecretId.New()),
            encrypt: true,
            trustServerCertificate: false,
            createDatabaseIfMissing: false);

        var save = await store.SavePersistenceConfigurationAsync(saved);
        Assert.True(save.IsSuccess, save.Error?.Message);

        using var composition = new HiveHostComposition(
            store,
            new SqlHiveHostServiceGraphFactory(
                new UnavailableHiveBootstrapCredentialStore(),
                store));

        var result = await composition.InitializeAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.host.bootstrap-credential-store-unavailable",
            result.Error!.Code);
        Assert.Null(composition.Current);
        Assert.Equal(
            HiveHostCompositionState.Unavailable,
            composition.Status.State);
    }

    [Fact]
    public async Task ApplyPersistedConfiguration_WhenConfigurationIsUnchanged_KeepsCurrentGraph()
    {
        var configuration = HivePersistenceConfiguration.LocalDevelopment(
            "Hive_Composition_Apply_Unchanged");
        var configurationStore = new InMemoryConfigurationStore(configuration);
        var graph = CreateGraph(configuration);

        using var composition = new HiveHostComposition(
            configurationStore,
            new ScriptedGraphFactory(
                Result<HiveHostServiceGraph>.Success(graph)));

        var initialization = await composition.InitializeAsync();
        Assert.True(initialization.IsSuccess, initialization.Error?.Message);

        var current = composition.Current;

        var apply = await composition.ApplyPersistedConfigurationAsync();

        Assert.True(apply.IsSuccess, apply.Error?.Message);
        Assert.Same(current, composition.Current);
        Assert.Same(current, apply.Value);
        Assert.Equal(HiveHostCompositionState.Ready, composition.Status.State);
    }

    [Fact]
    public async Task ApplyPersistedConfiguration_WhenPersistenceConfigurationChanges_ReplacesGraph()
    {
        var firstConfiguration = HivePersistenceConfiguration.LocalDevelopment(
            "Hive_Composition_Apply_First");
        var secondConfiguration = HivePersistenceConfiguration.LocalDevelopment(
            "Hive_Composition_Apply_Second");
        var configurationStore = new InMemoryConfigurationStore(firstConfiguration);
        var firstGraph = CreateGraph(firstConfiguration);
        var secondGraph = CreateGraph(secondConfiguration);

        using var composition = new HiveHostComposition(
            configurationStore,
            new ScriptedGraphFactory(
                Result<HiveHostServiceGraph>.Success(firstGraph),
                Result<HiveHostServiceGraph>.Success(secondGraph)));

        var initialization = await composition.InitializeAsync();
        Assert.True(initialization.IsSuccess, initialization.Error?.Message);

        configurationStore.Configuration = secondConfiguration;

        var apply = await composition.ApplyPersistedConfigurationAsync();

        Assert.True(apply.IsSuccess, apply.Error?.Message);
        Assert.Same(secondGraph, composition.Current);
        Assert.Equal(
            secondConfiguration,
            composition.Current!.PersistenceConfiguration);
        Assert.True(firstGraph.IsDisposed);
    }

    [Fact]
    public async Task ApplyPersistedConfiguration_WhenReplacementFails_PreservesCurrentGraph()
    {
        var firstConfiguration = HivePersistenceConfiguration.LocalDevelopment(
            "Hive_Composition_Apply_Failure");
        var secondConfiguration = HivePersistenceConfiguration.LocalDevelopment(
            "Hive_Composition_Apply_Failure_New");
        var configurationStore = new InMemoryConfigurationStore(firstConfiguration);
        var firstGraph = CreateGraph(firstConfiguration);
        var factory = new ScriptedGraphFactory(
            Result<HiveHostServiceGraph>.Success(firstGraph),
            Result<HiveHostServiceGraph>.Failure(
                new Error(
                    "hive.host.test-apply-failed",
                    ErrorCategory.External,
                    "Candidate graph construction failed.")));

        using var composition = new HiveHostComposition(
            configurationStore,
            factory);

        var initialization = await composition.InitializeAsync();
        Assert.True(initialization.IsSuccess, initialization.Error?.Message);

        configurationStore.Configuration = secondConfiguration;

        var apply = await composition.ApplyPersistedConfigurationAsync();

        Assert.True(apply.IsFailure);
        Assert.Same(firstGraph, composition.Current);
        Assert.Equal(
            firstConfiguration,
            composition.Current!.PersistenceConfiguration);
        Assert.Equal(
            HiveHostCompositionState.ReplacementFailed,
            composition.Status.State);
        Assert.Equal(
            "hive.host.test-apply-failed",
            composition.Status.LastError!.Code);
    }

    [Fact]
    public async Task FailedReplacement_PreservesCurrentGraph()
    {
        var configurationStore = new InMemoryConfigurationStore(
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Replacement"));
        var firstGraph = CreateGraph(
            configurationStore.Configuration!);
        var factory = new ScriptedGraphFactory(
            Result<HiveHostServiceGraph>.Success(firstGraph),
            Result<HiveHostServiceGraph>.Failure(
                new Error(
                    "hive.host.test-replacement-failed",
                    ErrorCategory.External,
                    "Candidate graph construction failed.")));

        using var composition = new HiveHostComposition(
            configurationStore,
            factory);

        var first = await composition.InitializeAsync();
        Assert.True(first.IsSuccess, first.Error?.Message);

        var published = composition.Current;

        configurationStore.Configuration =
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Replacement_New");

        var replacement = await composition.ReloadAsync();

        Assert.True(replacement.IsFailure);
        Assert.Same(published, composition.Current);
        Assert.Equal(
            "Hive_Composition_Replacement",
            composition.Current!.PersistenceConfiguration.DatabaseName);
        Assert.Equal(
            HiveHostCompositionState.ReplacementFailed,
            composition.Status.State);
        Assert.Equal(
            "hive.host.test-replacement-failed",
            composition.Status.LastError!.Code);
    }

    [Fact]
    public async Task ReplacedGraph_IsDisposedExactlyOnce()
    {
        var configurationStore = new InMemoryConfigurationStore(
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Disposal"));
        var firstResource = new TrackingDisposable();
        var secondResource = new TrackingDisposable();

        var firstGraph = CreateGraph(
            configurationStore.Configuration!,
            firstResource);
        var secondGraph = CreateGraph(
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Disposal_2"),
            secondResource);

        var factory = new ScriptedGraphFactory(
            Result<HiveHostServiceGraph>.Success(firstGraph),
            Result<HiveHostServiceGraph>.Success(secondGraph));

        using var composition = new HiveHostComposition(
            configurationStore,
            factory);

        var first = await composition.InitializeAsync();
        Assert.True(first.IsSuccess, first.Error?.Message);

        configurationStore.Configuration =
            secondGraph.PersistenceConfiguration;

        var second = await composition.ReloadAsync();
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(1, firstResource.DisposeCount);
        Assert.Equal(0, secondResource.DisposeCount);

        composition.Dispose();

        Assert.Equal(1, firstResource.DisposeCount);
        Assert.Equal(1, secondResource.DisposeCount);

        composition.Dispose();

        Assert.Equal(1, firstResource.DisposeCount);
        Assert.Equal(1, secondResource.DisposeCount);
    }

    [Fact]
    public async Task ConcurrentCompositionRequests_SerializeCandidateConstruction()
    {
        var configurationStore = new InMemoryConfigurationStore(
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Serial"));
        var factory = new SerializingGraphFactory(
            configurationStore.Configuration!);

        using var composition = new HiveHostComposition(
            configurationStore,
            factory);

        var first = composition.InitializeAsync();
        await factory.FirstCandidateStarted.Task;

        var second = composition.ReloadAsync();
        await Task.Yield();

        factory.ReleaseFirstCandidate();

        var results = await Task.WhenAll(first, second);

        Assert.All(
            results,
            result => Assert.True(
                result.IsSuccess,
                result.Error?.Message));
        Assert.Equal(1, factory.MaxConcurrentCandidates);
        Assert.Equal(2, factory.CreationCount);
    }

    [Fact]
    public async Task Dispose_CancelsActiveCompositionAndDoesNotPublishCandidate()
    {
        var configuration =
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Dispose_Active");
        var store = new InMemoryConfigurationStore(configuration);
        var factory = new SerializingGraphFactory(configuration);

        using var composition = new HiveHostComposition(store, factory);

        var initialization = composition.InitializeAsync();
        await factory.FirstCandidateStarted.Task;

        composition.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => initialization);

        Assert.Null(composition.Current);
        Assert.Equal(
            HiveHostCompositionState.Disposed,
            composition.Status.State);
    }

    [Fact]
    public async Task Dispose_DoesNotPublishOrLeakLateCandidate()
    {
        var configuration =
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Late_Candidate");
        var store = new InMemoryConfigurationStore(configuration);
        var resource = new TrackingDisposable();
        var candidate = CreateGraph(configuration, resource);
        var factory = new CancellationIgnoringGraphFactory(candidate);

        var composition = new HiveHostComposition(store, factory);

        try
        {
            var initialization = composition.InitializeAsync();
            await factory.CandidateStarted.Task;

            composition.Dispose();
            factory.ReleaseCandidate();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => initialization);

            Assert.Null(composition.Current);
            Assert.Equal(
                HiveHostCompositionState.Disposed,
                composition.Status.State);
            Assert.Equal(1, resource.DisposeCount);
            Assert.True(candidate.IsDisposed);
        }
        finally
        {
            composition.Dispose();
            candidate.Dispose();
        }
    }

    [Fact]
    public async Task Dispose_StillDisposesLifetimeAfterCurrentGraphThrows()
    {
        var configuration =
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Dispose_ThrowingGraph");
        var store = new InMemoryConfigurationStore(configuration);
        var resource = new ThrowingDisposable();
        var graph = CreateGraph(configuration, resource);

        var composition = new HiveHostComposition(
            store,
            new ScriptedGraphFactory(
                Result<HiveHostServiceGraph>.Success(graph)));

        var initialization = await composition.InitializeAsync();
        Assert.True(initialization.IsSuccess, initialization.Error?.Message);

        var exception = Assert.Throws<InvalidOperationException>(
            composition.Dispose);

        Assert.Equal("synthetic disposal failure", exception.Message);
        Assert.Equal(HiveHostCompositionState.Disposed, composition.Status.State);

        composition.Dispose();
    }

    [Fact]
    public async Task Dispose_DoesNotOverwriteTerminalStatusAfterLateFactoryFailure()
    {
        var configuration =
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_Late_Failure");
        var store = new InMemoryConfigurationStore(configuration);
        var factory = new CancellationIgnoringFailureGraphFactory();

        var composition = new HiveHostComposition(store, factory);

        try
        {
            var initialization = composition.InitializeAsync();
            await factory.FailureStarted.Task;

            composition.Dispose();
            factory.ReleaseFailure();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => initialization);

            Assert.Null(composition.Current);
            Assert.Equal(
                HiveHostCompositionState.Disposed,
                composition.Status.State);
            Assert.Null(composition.Status.LastError);
        }
        finally
        {
            composition.Dispose();
        }
    }

    [Fact]
    public async Task GraphFactory_SanitizesBootstrapResolutionErrors()
    {
        var configuration = new HivePersistenceConfiguration(
            HivePersistenceBackend.SqlServer,
            "sql.example.test",
            1433,
            "Hive_HostFactory_ErrorBoundary",
            HiveSqlAuthenticationMode.SqlPassword,
            "hive-user",
            new HiveBootstrapCredentialReference(SecretId.New()),
            encrypt: true,
            trustServerCertificate: false,
            createDatabaseIfMissing: false);

        var factory = new SqlHiveHostServiceGraphFactory(
            new LeakyBootstrapCredentialStore(),
            new InMemoryConfigurationStore(configuration));

        var result = await factory.CreateAsync(configuration);

        Assert.True(result.IsFailure);
        Assert.Equal("test.bootstrap.failure", result.Error!.Code);
        Assert.Equal(ErrorCategory.External, result.Error.Category);
        Assert.Equal(
            "The Hive bootstrap credential could not be resolved.",
            result.Error.Message);
        Assert.DoesNotContain(
            "sensitive",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Composition_SanitizesUnexpectedFailureDetails()
    {
        var configuration =
            HivePersistenceConfiguration.LocalDevelopment(
                "Hive_Composition_ErrorBoundary");
        var composition = new HiveHostComposition(
            new InMemoryConfigurationStore(configuration),
            new ThrowingGraphFactory());

        try
        {
            var result = await composition.InitializeAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(
                "hive.host.composition-failed",
                result.Error!.Code);
            Assert.Equal(
                "Hive host composition failed.",
                result.Error.Message);
            Assert.DoesNotContain(
                "sensitive",
                result.Error.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            composition.Dispose();
        }
    }

    private static HiveHostServiceGraph CreateGraph(
        HivePersistenceConfiguration configuration,
        params IDisposable[] resources)
    {
        var options = HiveDatabaseOptions.FromConfiguration(configuration);
        var facade = new HiveManagementFacade(
            new SqlProviderResourceStore(options),
            new SqlAgentDefinitionResourceStore(options),
            new SqlWorkItemResourceStore(options));

        return new HiveHostServiceGraph(
            configuration,
            facade,
            resources);
    }

    private sealed class CancellationIgnoringGraphFactory :
        IHiveHostServiceGraphFactory
    {
        private readonly HiveHostServiceGraph _candidate;
        private readonly TaskCompletionSource<bool> _candidateStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseCandidate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationIgnoringGraphFactory(
            HiveHostServiceGraph candidate)
        {
            _candidate = candidate
                ?? throw new ArgumentNullException(nameof(candidate));
        }

        public TaskCompletionSource<bool> CandidateStarted =>
            _candidateStarted;

        public void ReleaseCandidate() =>
            _releaseCandidate.TrySetResult(true);

        public async Task<Result<HiveHostServiceGraph>> CreateAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            _candidateStarted.TrySetResult(true);
            await _releaseCandidate.Task.ConfigureAwait(false);
            return Result<HiveHostServiceGraph>.Success(_candidate);
        }
    }

    private sealed class CancellationIgnoringFailureGraphFactory :
        IHiveHostServiceGraphFactory
    {
        private readonly TaskCompletionSource<bool> _failureStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFailure =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> FailureStarted =>
            _failureStarted;

        public void ReleaseFailure() =>
            _releaseFailure.TrySetResult(true);

        public async Task<Result<HiveHostServiceGraph>> CreateAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            _failureStarted.TrySetResult(true);
            await _releaseFailure.Task.ConfigureAwait(false);

            return Result<HiveHostServiceGraph>.Failure(
                new Error(
                    "test.late-factory-failure",
                    ErrorCategory.External,
                    "late factory failure"));
        }
    }

    private sealed class LeakyBootstrapCredentialStore :
        IHiveBootstrapCredentialStore
    {
        public Task<Result> SetAsync(
            HiveBootstrapCredentialReference reference,
            SecretMaterial material,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result.Failure(
                    new Error(
                        "test.bootstrap.failure",
                        ErrorCategory.External,
                        "sensitive bootstrap details")));

        public Task<Result<SecretMaterial>> ResolveAsync(
            HiveBootstrapCredentialReference reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result<SecretMaterial>.Failure(
                    new Error(
                        "test.bootstrap.failure",
                        ErrorCategory.External,
                        "sensitive bootstrap details")));

        public Task<Result> ClearAsync(
            HiveBootstrapCredentialReference reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result.Failure(
                    new Error(
                        "test.bootstrap.failure",
                        ErrorCategory.External,
                        "sensitive bootstrap details")));
    }

    private sealed class ThrowingGraphFactory :
        IHiveHostServiceGraphFactory
    {
        public Task<Result<HiveHostServiceGraph>> CreateAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "sensitive graph construction details");
    }

    private sealed class InMemoryConfigurationStore :
        IHiveConfigurationStore
    {
        public InMemoryConfigurationStore(
            HivePersistenceConfiguration configuration)
        {
            Configuration = configuration;
        }

        public HivePersistenceConfiguration? Configuration { get; set; }

        public Task<Result<HivePersistenceConfiguration>> LoadPersistenceConfigurationAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Result<HivePersistenceConfiguration>.Success(
                    Configuration!));
        }

        public Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Configuration = configuration;

            return Task.FromResult(
                Result<HivePersistenceConfiguration>.Success(
                    configuration));
        }
    }

    private sealed class ScriptedGraphFactory :
        IHiveHostServiceGraphFactory
    {
        private readonly Queue<Result<HiveHostServiceGraph>> _results;

        public ScriptedGraphFactory(
            params Result<HiveHostServiceGraph>[] results)
        {
            _results = new Queue<Result<HiveHostServiceGraph>>(results);
        }

        public Task<Result<HiveHostServiceGraph>> CreateAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_results.Count == 0)
            {
                return Task.FromResult(
                    Result<HiveHostServiceGraph>.Failure(
                        new Error(
                            "hive.host.test-factory-exhausted",
                            ErrorCategory.Internal,
                            "The test graph factory has no remaining results.")));
            }

            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class SerializingGraphFactory :
        IHiveHostServiceGraphFactory
    {
        private readonly TaskCompletionSource<bool> _firstCandidateStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstCandidate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeCandidates;
        private int _maxConcurrentCandidates;
        private int _creationCount;

        public SerializingGraphFactory(
            HivePersistenceConfiguration configuration)
        {
        }

        public TaskCompletionSource<bool> FirstCandidateStarted =>
            _firstCandidateStarted;

        public int MaxConcurrentCandidates =>
            Volatile.Read(ref _maxConcurrentCandidates);

        public int CreationCount =>
            Volatile.Read(ref _creationCount);

        public void ReleaseFirstCandidate() =>
            _releaseFirstCandidate.TrySetResult(true);

        public async Task<Result<HiveHostServiceGraph>> CreateAsync(
            HivePersistenceConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var active = Interlocked.Increment(ref _activeCandidates);
            UpdateMax(active);
            var count = Interlocked.Increment(ref _creationCount);

            try
            {
                if (count == 1)
                {
                    _firstCandidateStarted.TrySetResult(true);
                    await _releaseFirstCandidate.Task
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                return Result<HiveHostServiceGraph>.Success(
                    CreateGraph(configuration));
            }
            finally
            {
                Interlocked.Decrement(ref _activeCandidates);
            }
        }

        private void UpdateMax(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrentCandidates);
                if (active <= current)
                    return;

                if (Interlocked.CompareExchange(
                        ref _maxConcurrentCandidates,
                        active,
                        current) == current)
                    return;
            }
        }
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public void Dispose() =>
            throw new InvalidOperationException(
                "synthetic disposal failure");
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class TemporarySettingsFile : IDisposable
    {
        private TemporarySettingsFile(string directory, string path)
        {
            Directory = directory;
            Path = path;
        }

        public string Directory { get; }

        public string Path { get; }

        public static TemporarySettingsFile Create()
        {
            var directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "HiveHostCompositionTests",
                Guid.NewGuid().ToString("N"));

            System.IO.Directory.CreateDirectory(directory);

            return new TemporarySettingsFile(
                directory,
                System.IO.Path.Combine(
                    directory,
                    "hive-settings.json"));
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
