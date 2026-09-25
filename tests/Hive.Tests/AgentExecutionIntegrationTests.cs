using System.Net;
using System.Net.Sockets;
using System.Text;
using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class AgentExecutionIntegrationTests
{
    [Fact]
    public void AgentExecutionRequest_RejectsDefaultCorrelationIdentity()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var agent = CreateAgent(context);
        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            new Uri("https://example.invalid/v1"),
            context.PrincipalId!.Value,
            context.TenantId!.Value);

        Assert.Throws<ArgumentException>(
            () => new AgentExecutionRequest(
                agent,
                runtime,
                target,
                context,
                "Test request.",
                correlationId: (CorrelationId?)default(CorrelationId)));
    }

    [Fact]
    public async Task ExecuteConfiguredAgentAsync_UsesPersistedTargetAndRefreshesAfterTargetChange()
    {
        var database = await PrepareDatabase("Hive_Test_ConfiguredAgentExecution");

        await using var firstServer = new LocalAgentServer(
            HttpStatusCode.OK,
            """{"id":"chatcmpl-configured-first","model":"configured-first","choices":[{"message":{"role":"assistant","content":"Configured target one."}}]}""");

        await using var secondServer = new LocalAgentServer(
            HttpStatusCode.OK,
            """{"id":"chatcmpl-configured-second","model":"configured-second","choices":[{"message":{"role":"assistant","content":"Configured target two."}}]}""");

        var eventStore = new SqlEventPersistenceStore(database.Options);
        var secretStore = new SqlDpapiSecretStore(database.Options);
        using var httpClient = new HttpClient();
        var executionService = new AgentExecutionService(
            eventStore,
            httpClient,
            TimeSpan.FromSeconds(5));

        var facade = new HiveManagementFacade(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            agentExecution: executionService);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var provider = CreateConfiguredProvider(context);
        var providerResult = await facade.CreateProviderAsync(provider, context);
        Assert.True(providerResult.IsSuccess, providerResult.Error?.Message);

        using var credentialMaterial = SecretMaterial.Create("test-api-key");

        var secretResult = await facade.CreateSecretAsync(
            "configured-agent-key",
            "Configured Agent Key",
            credentialMaterial,
            context);
        Assert.True(secretResult.IsSuccess, secretResult.Error?.Message);

        var account = CreateConfiguredAccount(
            provider.Id,
            secretResult.Value!.Id,
            context);
        var accountResult = await facade.CreateProviderAccountAsync(account, context);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);

        var firstTarget = CreateConfiguredTarget(
            provider.Id,
            account.Id,
            firstServer.BaseUri,
            "configured-first",
            context);
        var firstTargetResult = await facade.CreateExecutionTargetAsync(
            firstTarget,
            context);
        Assert.True(firstTargetResult.IsSuccess, firstTargetResult.Error?.Message);

        var secondTarget = CreateConfiguredTarget(
            provider.Id,
            account.Id,
            secondServer.BaseUri,
            "configured-second",
            context);
        var secondTargetResult = await facade.CreateExecutionTargetAsync(
            secondTarget,
            context);
        Assert.True(secondTargetResult.IsSuccess, secondTargetResult.Error?.Message);

        var definition = CreateConfiguredAgentDefinition(
            firstTarget.Id,
            context);
        var definitionResult = await facade.CreateAgentDefinitionAsync(
            definition,
            context);
        Assert.True(definitionResult.IsSuccess, definitionResult.Error?.Message);

        var firstExecution = await facade.ExecuteConfiguredAgentAsync(
            definition.Id,
            context,
            "Use configured target one.");

        Assert.True(firstExecution.IsSuccess, firstExecution.Error?.Message);
        Assert.Equal(firstTarget.Id, firstExecution.Value!.TargetId);
        Assert.Equal(
            "Configured target one.",
            firstExecution.Value.ResponseText);
        Assert.True(firstServer.RequestObserved.Task.IsCompletedSuccessfully);
        Assert.False(secondServer.RequestObserved.Task.IsCompletedSuccessfully);

        var updatedDefinition = definitionResult.Value!
            .WithConfiguredExecutionTarget(secondTarget.Id);

        var updateResult = await facade.UpdateAgentDefinitionAsync(
            updatedDefinition,
            context);

        Assert.True(updateResult.IsSuccess, updateResult.Error?.Message);

        var secondExecution = await facade.ExecuteConfiguredAgentAsync(
            definition.Id,
            context,
            "Use configured target two.");

        Assert.True(secondExecution.IsSuccess, secondExecution.Error?.Message);
        Assert.Equal(secondTarget.Id, secondExecution.Value!.TargetId);
        Assert.Equal(
            "Configured target two.",
            secondExecution.Value.ResponseText);
        Assert.True(secondServer.RequestObserved.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteConfiguredAgentAsync_RejectsUnconfiguredAgent()
    {
        var database = await PrepareDatabase("Hive_Test_UnconfiguredAgentExecution");
        var eventStore = new SqlEventPersistenceStore(database.Options);

        using var httpClient = new HttpClient();
        var executionService = new AgentExecutionService(
            eventStore,
            httpClient,
            TimeSpan.FromSeconds(5));

        var facade = new HiveManagementFacade(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            agentExecution: executionService);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var definition = CreateConfiguredAgentDefinition(
            null,
            context);

        var created = await facade.CreateAgentDefinitionAsync(
            definition,
            context);

        Assert.True(created.IsSuccess, created.Error?.Message);

        var result = await facade.ExecuteConfiguredAgentAsync(
            created.Value!.Id,
            context,
            "This must be rejected.");

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.management.agent-definition.execution-target-required",
            result.Error!.Code);
        Assert.Equal(
            ErrorCategory.Validation,
            result.Error.Category);
    }

    [Fact]
    public async Task ExecuteConfiguredAgentAsync_RejectsInactiveProviderAccount()
    {
        var database = await PrepareDatabase("Hive_Test_InactiveProviderAccountExecution");
        var eventStore = new SqlEventPersistenceStore(database.Options);
        var secretStore = new SqlDpapiSecretStore(database.Options);

        using var httpClient = new HttpClient();
        var executionService = new AgentExecutionService(
            eventStore,
            httpClient,
            TimeSpan.FromSeconds(5));

        var facade = new HiveManagementFacade(
            new SqlProviderResourceStore(database.Options),
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            agentExecution: executionService);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var provider = CreateConfiguredProvider(context);
        var providerResult = await facade.CreateProviderAsync(
            provider,
            context);

        Assert.True(providerResult.IsSuccess, providerResult.Error?.Message);

        using var credentialMaterial = SecretMaterial.Create("test-api-key");

        var secretResult = await facade.CreateSecretAsync(
            "inactive-account-agent-key",
            "Inactive Account Agent Key",
            credentialMaterial,
            context);

        Assert.True(secretResult.IsSuccess, secretResult.Error?.Message);

        var account = CreateConfiguredAccount(
            provider.Id,
            secretResult.Value!.Id,
            context);

        var accountResult = await facade.CreateProviderAccountAsync(
            account,
            context);

        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);

        var target = CreateConfiguredTarget(
            provider.Id,
            account.Id,
            new Uri("https://example.invalid/v1"),
            "inactive-account-model",
            context);

        var targetResult = await facade.CreateExecutionTargetAsync(
            target,
            context);

        Assert.True(targetResult.IsSuccess, targetResult.Error?.Message);

        var definition = CreateConfiguredAgentDefinition(
            target.Id,
            context);

        var definitionResult = await facade.CreateAgentDefinitionAsync(
            definition,
            context);

        Assert.True(definitionResult.IsSuccess, definitionResult.Error?.Message);

        var retiredAccount = await facade.DeleteProviderAccountAsync(
            account.Id,
            context);

        Assert.True(retiredAccount.IsSuccess, retiredAccount.Error?.Message);
        Assert.Equal(
            ResourceLifecycleStatus.Retired,
            retiredAccount.Value!.Resource!.Lifecycle.Status);

        var result = await facade.ExecuteConfiguredAgentAsync(
            definition.Id,
            context,
            "This must be rejected.");

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.management.agent-execution.provider-account-inactive",
            result.Error!.Code);
        Assert.Equal(
            ErrorCategory.Unsupported,
            result.Error.Category);
    }

    [Fact]
    public async Task ExecuteAsync_CompletesAndPersistsCorrelatedLifecycle()
    {
        var database = await PrepareDatabase("Hive_Test_AgentExecution");
        var store = new SqlEventPersistenceStore(database.Options);

        await using var server = new LocalAgentServer(
            HttpStatusCode.OK,
            """{"id":"chatcmpl-agent-test","model":"test-model","choices":[{"message":{"role":"assistant","content":"Hello from MAF."}}]}""");

        using var httpClient = new HttpClient();
        var service = new AgentExecutionService(
            store,
            httpClient,
            TimeSpan.FromSeconds(5));

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var agent = CreateAgent(accessContext);
        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            server.BaseUri,
            principal,
            tenant);

        var correlationId = CorrelationId.New();

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "Say hello.",
                correlationId: correlationId));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Hello from MAF.", result.Value!.ResponseText);
        Assert.Equal(
            correlationId,
            result.Value.CorrelationId);
        Assert.Equal(
            ExecutionStatus.Succeeded,
            result.Value.Execution.Status);
        Assert.Equal(
            "chatcmpl-agent-test",
            result.Value.ProviderResponseId);

        var events = await store.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                result.Value.Execution.Id.Value));

        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Equal(2, events.Value!.Count);

        var started = events.Value[0];
        var succeeded = events.Value[1];

        Assert.Equal(1, started.StreamVersion.Value);
        Assert.Equal(2, succeeded.StreamVersion.Value);
        Assert.Equal(
            "agent.execution.started",
            started.Envelope.EventType.Value);
        Assert.Equal(
            "agent.execution.succeeded",
            succeeded.Envelope.EventType.Value);
        Assert.Equal(
            correlationId,
            started.Envelope.CorrelationId);
        Assert.Equal(
            correlationId,
            succeeded.Envelope.CorrelationId);
        Assert.Equal(
            started.Envelope.EventId.Value,
            succeeded.Envelope.CausationId!.Value.Value);
        Assert.Equal(
            result.Value.StartedEventId,
            started.Envelope.EventId);
        Assert.Equal(
            result.Value.TerminalEventId,
            succeeded.Envelope.EventId);
        Assert.Equal(
            "chatcmpl-agent-test",
            succeeded.Envelope.Payload.GetProperty("providerResponseId").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_PersistsFailedLifecycle()
    {
        var database = await PrepareDatabase("Hive_Test_AgentExecutionFailure");
        var store = new SqlEventPersistenceStore(database.Options);

        await using var server = new LocalAgentServer(
            HttpStatusCode.InternalServerError,
            """{"error":{"message":"provider unavailable"}}""");

        using var httpClient = new HttpClient();
        var service = new AgentExecutionService(
            store,
            httpClient,
            TimeSpan.FromSeconds(5));

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var agent = CreateAgent(accessContext);
        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            server.BaseUri,
            principal,
            tenant);

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "Trigger provider failure."));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategory.External, result.Error.Category);

        var executionId = await ReadLatestExecutionIdAsync(
            database.Options);

        var events = await store.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                executionId.Value));

        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Equal(2, events.Value!.Count);
        Assert.Equal(
            "agent.execution.started",
            events.Value[0].Envelope.EventType.Value);
        Assert.Equal(
            "agent.execution.failed",
            events.Value[1].Envelope.EventType.Value);
        Assert.Equal(
            events.Value[0].Envelope.EventId.Value,
            events.Value[1].Envelope.CausationId!.Value.Value);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedFailure_DoesNotExposeExceptionMessage()
    {
        var database = await PrepareDatabase("Hive_Test_AgentExecutionUnexpectedFailure");
        var store = new SqlEventPersistenceStore(database.Options);

        using var httpClient = new HttpClient(
            new ThrowingHttpMessageHandler(
                "secret transport implementation detail"));
        var service = new AgentExecutionService(
            store,
            httpClient,
            TimeSpan.FromSeconds(5));

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var agent = CreateAgent(accessContext);
        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            new Uri("https://example.invalid/v1"),
            principal,
            tenant);

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "Trigger an unexpected transport failure."));

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorCategory.Internal,
            result.Error!.Category);
        Assert.Equal(
            "hive.agent.execution.failed",
            result.Error.Code);
        Assert.Equal(
            "Agent execution failed unexpectedly.",
            result.Error.Message);
        Assert.DoesNotContain(
            "secret transport implementation detail",
            result.Error.Message,
            StringComparison.Ordinal);

        var executionId = await ReadLatestExecutionIdAsync(
            database.Options);

        var events = await store.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                executionId.Value));

        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Equal(2, events.Value!.Count);
        Assert.Equal(
            "agent.execution.started",
            events.Value[0].Envelope.EventType.Value);
        Assert.Equal(
            "agent.execution.failed",
            events.Value[1].Envelope.EventType.Value);
    }

    [Fact]
    public async Task ExecuteAsync_TargetScopeMismatchFailsBeforeExecution()
    {
        var database = await PrepareDatabase("Hive_Test_AgentExecutionScope");
        var store = new SqlEventPersistenceStore(database.Options);

        await using var server = new LocalAgentServer(
            HttpStatusCode.OK,
            """{"id":"chatcmpl-scope","model":"test-model","choices":[{"message":{"role":"assistant","content":"should not run"}}]}""");

        using var httpClient = new HttpClient();
        var service = new AgentExecutionService(
            store,
            httpClient,
            TimeSpan.FromSeconds(5));

        var principal = PrincipalId.New();
        var accessTenant = TenantId.New();
        var targetTenant = TenantId.New();

        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            accessTenant,
            principal);

        var agent = CreateAgent(accessContext);
        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            server.BaseUri,
            principal,
            targetTenant);

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "This must be rejected."));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.agent.execution.target-context-mismatch",
            result.Error!.Code);
        Assert.Equal(
            ErrorCategory.Validation,
            result.Error.Category);
        Assert.False(
            server.RequestObserved.Task.IsCompletedSuccessfully);

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            database.Options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM [dbo].[HiveEventLog]
            WHERE [EventType] = @EventType;
            """;
        command.Parameters.AddWithValue(
            "@EventType",
            "agent.execution.started");

        Assert.Equal(
            0,
            Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_PersistsCancelledLifecycle()
    {
        var database = await PrepareDatabase("Hive_Test_AgentExecutionCancellation");
        var store = new SqlEventPersistenceStore(database.Options);

        await using var server = new LocalAgentServer(
            HttpStatusCode.OK,
            string.Empty,
            waitForResponse: true);

        using var httpClient = new HttpClient();
        var service = new AgentExecutionService(
            store,
            httpClient,
            TimeSpan.FromSeconds(5));

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var accessContext = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var agent = CreateAgent(accessContext);
        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            server.BaseUri,
            principal,
            tenant);

        using var cancellation = new CancellationTokenSource();

        var executionTask = service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "Cancel this request."),
            cancellation.Token);

        await server.RequestObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var result = await executionTask;

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorCategory.Cancelled,
            result.Error!.Category);

        var executionId = await ReadLatestExecutionIdAsync(
            database.Options);

        var events = await store.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                executionId.Value));

        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Equal(2, events.Value!.Count);
        Assert.Equal(
            "agent.execution.started",
            events.Value[0].Envelope.EventType.Value);
        Assert.Equal(
            "agent.execution.cancelled",
            events.Value[1].Envelope.EventType.Value);
        Assert.Equal(
            events.Value[0].Envelope.EventId.Value,
            events.Value[1].Envelope.CausationId!.Value.Value);
    }

    private static async Task<ExecutionId> ReadLatestExecutionIdAsync(
        HiveDatabaseOptions options)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) [StreamIdentity]
            FROM [dbo].[HiveEventLog]
            WHERE [StreamKind] = @StreamKind
              AND [EventType] = @EventType
            ORDER BY [OccurredAtUtc] DESC, [EventId] DESC;
            """;
        command.Parameters.AddWithValue(
            "@StreamKind",
            (int)ResourceKind.Execution);
        command.Parameters.AddWithValue(
            "@EventType",
            "agent.execution.started");

        var value = await command.ExecuteScalarAsync();

        Assert.NotNull(value);
        return new ExecutionId((Guid)value!);
    }

    private static Provider CreateConfiguredProvider(
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new Provider(
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
            "configured-provider",
            "Configured Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateConfiguredAccount(
        ProviderId providerId,
        SecretId secretId,
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            "configured-account",
            "Configured Account",
            credentialSecret: new SecretReference(secretId));
    }

    private static ExecutionTarget CreateConfiguredTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        Uri endpoint,
        string model,
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            $"configured-target-{model}",
            $"Configured {model}",
            endpoint,
            model,
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);
    }

    private static AgentDefinition CreateConfiguredAgentDefinition(
        ExecutionTargetId? targetId,
        ResourceAccessContext context)
    {
        var now = DateTimeOffset.UtcNow;

        return new AgentDefinition(
            new ResourceEnvelope<AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                AgentDefinitionId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "configured-agent",
            "Configured Agent",
            AgentGeneration.Base,
            targetId);
    }

    private static Agent CreateAgent(
        ResourceAccessContext accessContext)
    {
        var result = new AgentFactory(
                new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition(
                    "execution-test-agent",
                    "Execution Test Agent"),
                new AgentCreationContext(accessContext));

        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private static ExecutionTarget CreateTarget(
        Uri endpoint,
        PrincipalId principal,
        TenantId tenant)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            ProviderId.New(),
            ProviderAccountId.New(),
            "execution-target",
            "Execution Target",
            endpoint,
            "test-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _message;

        public ThrowingHttpMessageHandler(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            _message = message;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new InvalidOperationException(_message));
    }

    private static async Task<PersistenceTestDatabase> PrepareDatabase(
        string name)
    {
        var database = new PersistenceTestDatabase(name);
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options)
            .MigrateAsync();

        Assert.True(
            migration.IsSuccess,
            migration.Error is null
                ? "Migration failed without an error."
                : $"Migration failed: {migration.Error.Code} [{migration.Error.Category}] {migration.Error.Message}");

        return database;
    }

    private sealed class LocalAgentServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly bool _waitForResponse;
        private readonly Task _serverTask;

        public LocalAgentServer(
            HttpStatusCode statusCode,
            string responseBody,
            bool waitForResponse = false)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _waitForResponse = waitForResponse;

            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();

            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = new Uri($"http://127.0.0.1:{port}/v1/");
            _serverTask = RunAsync();
        }

        public Uri BaseUri { get; }

        public TaskCompletionSource<bool> RequestObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();

            try
            {
                await _serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
                when (_stop.IsCancellationRequested)
            {
            }

            _stop.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener
                    .AcceptTcpClientAsync(_stop.Token)
                    .ConfigureAwait(false);

                await using var stream = client.GetStream();

                var request = await ReadRequestAsync(
                    stream,
                    _stop.Token).ConfigureAwait(false);

                RequestObserved.TrySetResult(true);

                if (_waitForResponse)
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        _stop.Token).ConfigureAwait(false);

                    return;
                }

                var bodyBytes = Encoding.UTF8.GetBytes(_responseBody);
                var reason = _statusCode == HttpStatusCode.OK
                    ? "OK"
                    : "Internal Server Error";

                var headers = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {(int)_statusCode} {reason}\r\n" +
                    "Content-Type: application/json\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    "Connection: close\r\n\r\n");

                await stream.WriteAsync(
                    headers,
                    _stop.Token).ConfigureAwait(false);

                await stream.WriteAsync(
                    bodyBytes,
                    _stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
                when (_stop.IsCancellationRequested)
            {
            }
        }

        private static async Task<ParsedRequest> ReadRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            var buffer = new byte[4096];
            var headerEnd = -1;

            while (headerEnd < 0)
            {
                var read = await stream
                    .ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                    throw new InvalidOperationException(
                        "The client closed before request headers were complete.");

                memory.Write(buffer, 0, read);

                headerEnd = FindHeaderEnd(
                    memory.GetBuffer(),
                    checked((int)memory.Length));
            }

            var bytes = memory.ToArray();
            var headerText = Encoding.ASCII.GetString(
                bytes,
                0,
                headerEnd);

            var contentLength = 0;
            foreach (var line in headerText.Split(
                         "\r\n",
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(
                        "Content-Length:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(
                        line["Content-Length:".Length..].Trim());
                }
            }

            while (memory.Length < headerEnd + 4 + contentLength)
            {
                var read = await stream
                    .ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                    break;

                memory.Write(buffer, 0, read);
            }

            var requestBytes = memory.ToArray();
            var body = contentLength == 0
                ? string.Empty
                : Encoding.UTF8.GetString(
                    requestBytes,
                    headerEnd + 4,
                    contentLength);

            return new ParsedRequest(body);
        }

        private static int FindHeaderEnd(
            byte[] bytes,
            int length)
        {
            for (var index = 0; index <= length - 4; index++)
            {
                if (bytes[index] == (byte)'\r' &&
                    bytes[index + 1] == (byte)'\n' &&
                    bytes[index + 2] == (byte)'\r' &&
                    bytes[index + 3] == (byte)'\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private sealed record ParsedRequest(
            string Body);
    }
}
