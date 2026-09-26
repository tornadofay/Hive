using System.Net;
using System.Net.Sockets;
using System.Text;
using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class FirstRealAgentExecutionExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public FirstRealAgentExecutionExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run MAF agent execution"
        };

        _surface.SetInformation(
            "Creates one base Agent and RuntimeInstance, runs one request through Microsoft Agent Framework using Hive's OpenAI-compatible provider boundary, and records durable execution lifecycle events.",
            "The selected ExecutionTarget points at a local fake HTTP endpoint. No vendor account, network provider, tool calling, workflow engine, or cognitive behavior is involved.",
            "Scope",
            "Phase 1.9 first real Base Agent execution only; one request, one provider call, durable started/terminal lifecycle events");

        _surface.CodeSnippet = """
            var service = new AgentExecutionService(
                eventStore,
                httpClient);

            var result = await service.ExecuteAsync(
                new AgentExecutionRequest(
                    agent,
                    runtime,
                    selectedTarget,
                    accessContext,
                    "Say hello.",
                    apiKey: null),
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
        await using var server = new LoopbackAgentServer();

        var options = HiveDatabaseOptions.LocalDevelopment(
            "Hive_Example_FirstAgentExecution");

        var migration = await new HiveDatabaseMigrator(options)
            .MigrateAsync(cancellationToken);

        EnsureSuccess(migration, "Hive database migration");

        var eventStore = new SqlEventPersistenceStore(options);

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
                    "example-first-execution-agent",
                    "Example First Execution Agent",
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
            eventStore,
            httpClient,
            TimeSpan.FromSeconds(5));

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                accessContext,
                "Say hello from Hive.",
                apiKey: null),
            cancellationToken);

        EnsureSuccess(result, "Agent execution");

        var execution = result.Value!;
        var stream = new ResourceReference(
            ResourceKind.Execution,
            execution.Execution.Id.Value);

        var events = await eventStore.ReadEventsAsync(
            stream,
            cancellationToken: cancellationToken);

        EnsureSuccess(events, "Execution lifecycle read");

        _output.Write(
            "First Real Agent Execution",
            $"""
            Database: {options.DatabaseName}
            Agent: {agent.Id}; generation={agent.Generation}
            Runtime: {runtime.Id}
            Execution: {execution.Execution.Id}; status={execution.Execution.Status}
            Target: {target.Id}; model={target.Model}
            Response: {execution.ResponseText}
            Lifecycle event count: {events.Value!.Count}
            Event 1: {events.Value[0].Envelope.EventType}; version={events.Value[0].StreamVersion}
            Event 2: {events.Value[1].Envelope.EventType}; version={events.Value[1].StreamVersion}
            Correlation: {execution.CorrelationId}
            Causation: {events.Value[1].Envelope.CausationId}
            Provider credentials: none
            Provider endpoint: local fake HTTP server
            MAF workflow/orchestration: none
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
            "example-execution-target",
            "Example Execution Target",
            endpoint,
            "example-model",
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

    private sealed class LoopbackAgentServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serverTask;

        public LoopbackAgentServer()
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
                    """{"id":"chatcmpl-hive-example","model":"example-model","choices":[{"message":{"role":"assistant","content":"Hello from Hive through MAF."}}]}""";

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
