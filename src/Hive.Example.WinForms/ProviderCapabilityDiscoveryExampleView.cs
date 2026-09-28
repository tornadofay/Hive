using System.Net;
using System.Net.Sockets;
using System.Text;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Persistence;
using Hive.Management;

namespace Hive.Example.WinForms;

internal sealed class ProviderCapabilityDiscoveryExampleView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly IHiveExampleOutput _output;
    private readonly HiveExampleTestSurface _surface;
    private readonly ResourceAccessContext _context = new(
        DeploymentId.New(),
        TenantId.New(),
        PrincipalId.New());

    public ProviderCapabilityDiscoveryExampleView(
        IHiveManagementFacade management,
        IHiveExampleOutput output)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run capability discovery"
        };

        _surface.SetInformation(
            "Discovers provider models through Hive Management using a local deterministic OpenAI-compatible endpoint, then applies discovered model capabilities to the existing ExecutionTarget selection boundary.",
            "The configured vision state starts as Unknown. Discovery reports vision as Supported, so selection succeeds without mutating the stored target. A second selection demonstrates that an explicit Unsupported configuration remains authoritative over discovered Supported data.",
            "Providers / Target Selection / Capability Discovery",
            "Uses a local TCP test endpoint; no provider credentials or vendor accounts are required.");

        _surface.CodeSnippet = """
            var discovery = await management.GetProviderDiscoveryAsync(
                target.Id,
                context);

            var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
                target,
                discovery.Value,
                DateTimeOffset.UtcNow);

            var selection = ExecutionTargetSelector.Select(
                request,
                new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>
                {
                    [target.Id] = effective
                });
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
        await using var server = new DiscoveryExampleServer();

        var provider = CreateProvider();
        var providerResult = await _management
            .CreateProviderAsync(provider, _context, cancellationToken);
        EnsureSuccess(providerResult, "Provider creation");

        var account = CreateAccount(provider.Id);
        var accountResult = await _management
            .CreateProviderAccountAsync(account, _context, cancellationToken);
        EnsureSuccess(accountResult, "ProviderAccount creation");

        var target = CreateTarget(
            provider.Id,
            account.Id,
            server.BaseUri);

        var targetResult = await _management
            .CreateExecutionTargetAsync(target, _context, cancellationToken);
        EnsureSuccess(targetResult, "ExecutionTarget creation");

        try
        {
            var discovery = await _management
                .GetProviderDiscoveryAsync(
                    target.Id,
                    _context,
                    cancellationToken: cancellationToken);

            EnsureSuccess(discovery, "Provider discovery");

            var model = discovery.Value!.Models.Single();

            var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
                target,
                discovery.Value,
                DateTimeOffset.UtcNow);

            var request = new ExecutionTargetSelectionRequest(
                [target],
                [
                    new CapabilityRequirement(
                        new CapabilityKey("vision"),
                        CapabilityRequirementKind.Required)
                ]);

            var selected = ExecutionTargetSelector.Select(
                request,
                new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>
                {
                    [target.Id] = effective
                });

            EnsureSuccess(selected, "Discovered-capability selection");

            var configuredOverrideTarget = target.WithCapabilities(
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unsupported)
            ]);

            var configuredOverride = ExecutionTargetCapabilityResolver.ResolveCapabilities(
                configuredOverrideTarget,
                discovery.Value,
                DateTimeOffset.UtcNow);

            var rejected = ExecutionTargetSelector.Select(
                new ExecutionTargetSelectionRequest(
                    [configuredOverrideTarget],
                    [
                        new CapabilityRequirement(
                            new CapabilityKey("vision"),
                            CapabilityRequirementKind.Required)
                    ]),
                new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>
                {
                    [configuredOverrideTarget.Id] = configuredOverride
                });

            if (rejected.IsSuccess ||
                rejected.Error?.Category != ErrorCategory.Unsupported)
            {
                throw new InvalidOperationException(
                    "Configured capability override did not remain authoritative.");
            }

            _output.Write(
                "Provider / Model Capability Discovery",
                $"""
                Model enumeration: {discovery.Value.ModelEnumerationState}
                Models discovered: {discovery.Value.Models.Count}
                Model: {model.ModelId}
                Model availability: {model.Availability}
                Model health: {model.Health}
                Discovered capabilities: {string.Join(
                    ", ",
                    model.DiscoveredCapabilities.Select(
                        capability => $"{capability.Capability}={capability.State}"))}
                Configured vision before discovery: Unknown
                Effective vision after fresh discovery: {effective.Single(
                    capability => capability.Capability == new CapabilityKey("vision")).State}
                Selection: {selected.Value!.SelectedTarget.Key}
                Configured Unsupported override: remained authoritative
                Refresh requests served by local endpoint: {server.RequestCount}
                Credentials present in example: none
                """);
        }
        finally
        {
            var retiredTarget = await _management
                .DeleteExecutionTargetAsync(
                    target.Id,
                    _context,
                    cancellationToken);

            EnsureSuccess(retiredTarget, "ExecutionTarget retirement");

            var retiredAccount = await _management
                .DeleteProviderAccountAsync(
                    account.Id,
                    _context,
                    cancellationToken);

            EnsureSuccess(retiredAccount, "ProviderAccount retirement");

            var retiredProvider = await _management
                .DeleteProviderAsync(
                    provider.Id,
                    _context,
                    cancellationToken);

            EnsureSuccess(retiredProvider, "Provider retirement");
        }
    }

    private Provider CreateProvider()
    {
        var now = DateTimeOffset.UtcNow;

        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                _context.PrincipalId!.Value,
                ResourceScope.Tenant(_context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    _context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            $"example-discovery-{Guid.NewGuid():N}",
            "Example Discovery Provider",
            "openai-compatible");
    }

    private ProviderAccount CreateAccount(ProviderId providerId)
    {
        var now = DateTimeOffset.UtcNow;

        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                _context.PrincipalId!.Value,
                ResourceScope.Tenant(_context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    _context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            $"example-discovery-account-{Guid.NewGuid():N}",
            "Example Discovery Account",
            "local-example");
    }

    private ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        Uri endpoint)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                _context.PrincipalId!.Value,
                ResourceScope.Tenant(_context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    _context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            $"example-discovery-target-{Guid.NewGuid():N}",
            "Example Discovery Target",
            endpoint,
            "vision-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unknown)
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

    private sealed class DiscoveryExampleServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(
            IPAddress.Loopback,
            0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serverTask;

        public DiscoveryExampleServer()
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = new Uri($"http://127.0.0.1:{port}/v1/");
            _serverTask = ServeAsync();
        }

        public Uri BaseUri { get; }

        public int RequestCount { get; private set; }

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

        private async Task ServeAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var client = await _listener
                        .AcceptTcpClientAsync(_stop.Token)
                        .ConfigureAwait(false);

                    await using var stream = client.GetStream();
                    var requestLine = await ReadRequestLineAsync(
                        stream,
                        _stop.Token).ConfigureAwait(false);

                    if (!requestLine.EndsWith(
                            " /v1/models HTTP/1.1",
                            StringComparison.Ordinal))
                    {
                        await WriteResponseAsync(
                            stream,
                            HttpStatusCode.NotFound,
                            "{"error":"not-found"}",
                            _stop.Token).ConfigureAwait(false);
                        continue;
                    }

                    RequestCount++;

                    const string body = """
                        {
                          "data": [
                            {
                              "id": "vision-model",
                              "owned_by": "example",
                              "available": true,
                              "health": "healthy",
                              "capabilities": {
                                "vision": true,
                                "tool_calling": true,
                                "structured_output": false
                              }
                            }
                          ]
                        }
                        """;

                    await WriteResponseAsync(
                        stream,
                        HttpStatusCode.OK,
                        body,
                        _stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
                when (_stop.IsCancellationRequested)
            {
            }
            catch (SocketException)
                when (_stop.IsCancellationRequested)
            {
            }
        }

        private static async Task<string> ReadRequestLineAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            var buffer = new List<byte>(256);

            while (true)
            {
                var value = new byte[1];
                var read = await stream.ReadAsync(
                    value,
                    cancellationToken).ConfigureAwait(false);

                if (read == 0)
                    throw new IOException(
                        "The example client closed the connection before the request line was received.");

                if (value[0] == (byte)'\n')
                    break;

                if (value[0] != (byte)'\r')
                    buffer.Add(value[0]);

                if (buffer.Count > 2048)
                    throw new InvalidOperationException(
                        "The example request line exceeded the safety limit.");
            }

            return Encoding.ASCII.GetString(buffer.ToArray());
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            HttpStatusCode statusCode,
            string body,
            CancellationToken cancellationToken)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            var headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)statusCode} {statusCode}\r\n" +
                $"Content-Type: application/json\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(headers, cancellationToken)
                .ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
