using System.Net;
using System.Net.Sockets;
using System.Text;
using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class AgentExecutionIntegrationTests
{
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
            succeeded.Envelope.CausationId!.Value);
        Assert.Equal(
            result.Value.StartedEventId,
            started.Envelope.EventId);
        Assert.Equal(
            result.Value.TerminalEventId,
            succeeded.Envelope.EventId);
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

        var executionId = server.ExecutionIdFromRequest.IsCompleted
            ? server.ExecutionIdFromRequest.Result
            : throw new InvalidOperationException("The fake provider did not receive the request.");

        var events = await store.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                executionId));

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
            events.Value[1].Envelope.CausationId!.Value);
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

        await server.RequestObserved.Task;
        cancellation.Cancel();

        var result = await executionTask;

        Assert.True(result.IsFailure);
        Assert.Equal(
            ErrorCategory.Cancelled,
            result.Error!.Category);

        var executionId = server.ExecutionIdFromRequest.IsCompleted
            ? server.ExecutionIdFromRequest.Result
            : throw new InvalidOperationException("The fake provider did not receive the request.");

        var events = await store.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                executionId));

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
            events.Value[1].Envelope.CausationId!.Value);
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

        public TaskCompletionSource<Guid> ExecutionIdFromRequest { get; } =
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

                if (request.ExecutionId is Guid executionId)
                    ExecutionIdFromRequest.TrySetResult(executionId);

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

            Guid? executionId = null;

            try
            {
                using var document =
                    System.Text.Json.JsonDocument.Parse(body);

                if (document.RootElement.TryGetProperty(
                        "messages",
                        out var messages) &&
                    messages.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var first = messages[0];

                    if (first.TryGetProperty(
                            "content",
                            out var content) &&
                        content.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        executionId = null;
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }

            return new ParsedRequest(body, executionId);
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
            string Body,
            Guid? ExecutionId);
    }
}
