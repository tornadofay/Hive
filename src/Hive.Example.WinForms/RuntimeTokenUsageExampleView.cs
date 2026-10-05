using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class RuntimeTokenUsageExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public RuntimeTokenUsageExampleView(IHiveExampleOutput output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run token usage foundation"
        };

        _surface.SetInformation(
            "Runs one Base Agent request against a deterministic loopback provider response containing provider-reported input, output, total, cached-input, and reasoning token usage, then reads the same usage back from the immutable execution event.",
            "The example proves the provider-to-execution-to-persistence usage boundary without a vendor account, external provider, tokenizer, billing service, or reporting subsystem.",
            "Providers / Runtime / Token Usage Foundation",
            "Uses a deterministic local HTTP server and the existing Hive execution event persistence boundary.");

        _surface.CodeSnippet = """
            var result = await service.ExecuteAsync(
                new AgentExecutionRequest(
                    agent,
                    runtime,
                    target,
                    accessContext,
                    "Report the usage evidence.",
                    apiKey: null),
                cancellationToken);

            // Provider-reported usage is available on result.Usage
            // and is persisted in the terminal execution event.
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);
    }

    private async Task RunExampleAsync(
        CancellationToken cancellationToken)
    {
        await using var server = new LoopbackUsageServer();

        var options = HiveDatabaseOptions.LocalDevelopment(
            "Hive_Example_RuntimeTokenUsage");

        var migration = await new HiveDatabaseMigrator(options)
            .MigrateAsync(cancellationToken);

        EnsureSuccess(migration, "Hive database migration");

        var persistence = HiveEventPersistence.CreateSql(options);

        var principal = PrincipalId.New();
        var deployment = DeploymentId.New();
        var tenant = TenantId.New();

        var accessContext = new ResourceAccessContext(
            deployment,
            tenant,
            principal);

        var agentResult = new AgentFactory(
                new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition(
                    "example-runtime-token-usage-agent",
                    "Example Runtime Token Usage Agent",
                    AgentGeneration.Base),
                new AgentCreationContext(accessContext));

        EnsureSuccess(agentResult, "Agent creation");

        var agent = agentResult.Value!;
        var runtime = agent.CreateRuntimeInstance();

        var target = CreateTarget(
            server.BaseUri,
            principal,
            tenant);

        using var httpClient = new HttpClient();

        var service = new AgentExecutionService(
            persistence,
            httpClient,
            TimeSpan.FromSeconds(5));

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "Report the usage evidence.",
                apiKey: null),
            cancellationToken);

        EnsureSuccess(result, "Agent execution");

        var execution = result.Value!;

        var events = await persistence.EventStore
            .ReadEventsAsync(
                new ResourceReference(
                    ResourceKind.Execution,
                    execution.Execution.Id.Value),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        EnsureSuccess(events, "Read execution events");

        var terminal = events.Value!
            .Single(eventItem =>
                eventItem.Envelope.EventType.Value ==
                "agent.execution.succeeded");

        var usage = terminal.Envelope.Payload.GetProperty("usage");

        _output.Write(
            "Runtime Token Usage Foundation",
            $"""
            Execution: {execution.Execution.Id}
            Provider response: {execution.ProviderResponseId}
            Usage evidence: {execution.Usage.Evidence}
            Input tokens: {execution.Usage.InputTokenCount}
            Output tokens: {execution.Usage.OutputTokenCount}
            Total tokens: {execution.Usage.TotalTokenCount}
            Cached input tokens: {execution.Usage.CachedInputTokenCount}
            Reasoning tokens: {execution.Usage.ReasoningTokenCount}
            Persisted evidence: {usage.GetProperty("evidence").GetString()}
            Persisted input tokens: {usage.GetProperty("inputTokenCount").GetInt64()}
            Persisted output tokens: {usage.GetProperty("outputTokenCount").GetInt64()}
            Persisted total tokens: {usage.GetProperty("totalTokenCount").GetInt64()}
            Persisted cached input tokens: {usage.GetProperty("cachedInputTokenCount").GetInt64()}
            Persisted reasoning tokens: {usage.GetProperty("reasoningTokenCount").GetInt64()}
            Provider credentials: none
            External provider call: no (loopback fixture)
            Tokenizer estimation: no
            Durable usage evidence: yes
            Migration: {migration.Value!.Status}; schema={migration.Value.CurrentSchemaVersion}
            """);
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
            "example-runtime-token-usage-target",
            "Example Runtime Token Usage Target",
            endpoint,
            "example-runtime-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);
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

    private sealed class LoopbackUsageServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serverTask;

        public LoopbackUsageServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();

            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = new Uri($"http://127.0.0.1:{port}/v1/");
            _serverTask = RunAsync();
        }

        public Uri BaseUri { get; }

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

                await ConsumeRequestAsync(
                    stream,
                    _stop.Token).ConfigureAwait(false);

                const string body =
                    """{"id":"chatcmpl-runtime-usage","model":"example-runtime-model","usage":{"prompt_tokens":120,"completion_tokens":45,"total_tokens":165,"prompt_tokens_details":{"cached_tokens":20},"completion_tokens_details":{"reasoning_tokens":10},"accepted_prediction_tokens":3},"choices":[{"message":{"role":"assistant","content":"Usage evidence captured by Hive."}}]}""";

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
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

        private static async Task ConsumeRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            var buffer = new byte[4096];

            while (true)
            {
                var read = await stream
                    .ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                    return;

                memory.Write(buffer, 0, read);

                if (FindHeaderEnd(
                        memory.GetBuffer(),
                        checked((int)memory.Length)) >= 0)
                {
                    return;
                }
            }
        }

        private static int FindHeaderEnd(
            byte[] bytes,
            int length)
        {
            for (var index = 0; index <= length - 4; index++)
            {
                if (bytes[index] == 13 &&
                    bytes[index + 1] == 10 &&
                    bytes[index + 2] == 13 &&
                    bytes[index + 3] == 10)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
