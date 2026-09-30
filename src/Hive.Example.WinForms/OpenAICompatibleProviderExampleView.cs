using System.Net;
using System.Net.Sockets;
using System.Text;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Providers.OpenAICompatible;

namespace Hive.Example.WinForms;

internal sealed class OpenAICompatibleProviderExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public OpenAICompatibleProviderExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run provider adapter"
        };

        _surface.SetInformation(
            "Runs the public OpenAI-compatible provider adapter against a local in-process HTTP endpoint. No vendor account or network provider is required.",
            "The output verifies a normal chat response and JSON structured output while keeping the transport boundary vendor-neutral.",
            "Provider Transport",
            "The local endpoint is created for the run and is discarded afterward. No credential is used.");

        _surface.CodeSnippet = """
            using var httpClient = new HttpClient();

            var adapter = new OpenAICompatibleProviderAdapter(
                httpClient,
                new OpenAICompatibleProviderOptions(
                    endpoint,
                    apiKey));

            var result = await adapter.CompleteChatAsync(
                request,
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
        await using var server = new LoopbackProviderServer();

        using var client = new HttpClient();
        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                server.BaseUri,
                timeout: TimeSpan.FromSeconds(5)));

        using var schema = System.Text.Json.JsonDocument.Parse(
            """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"],"additionalProperties":false}""");

        var result = await adapter.CompleteChatAsync(
            new OpenAICompatibleChatRequest(
                "example-model",
                [
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.System,
                        "Return concise JSON."),
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.User,
                        "Return the name Alice.")
                ],
                new OpenAICompatibleStructuredOutput(
                    "person",
                    schema.RootElement)),
            cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Provider adapter failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }

        var structured = result.Value!.StructuredContent
            ?? throw new InvalidOperationException(
                "Structured output was not returned.");

        _output.Write(
            "OpenAI-compatible Provider Adapter",
            $"""
            Endpoint: local fake HTTP server
            Model: {result.Value.Model}
            Response ID: {result.Value.Id ?? "(none)"}
            Assistant content: {result.Value.Content}
            Structured name: {structured.GetProperty("name").GetString()}
            Authentication: none
            Vendor SDK: none
            """);
    }

    private sealed class LoopbackProviderServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serverTask;

        public LoopbackProviderServer()
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
                await ConsumeRequestAsync(stream, _stop.Token).ConfigureAwait(false);

                const string body =
                    """{"id":"chatcmpl-example","model":"example-model","choices":[{"message":{"role":"assistant","content":"{\"name\":\"Alice\"}"}}]}""";

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var headerBytes = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    "Connection: close\r\n\r\n");

                await stream.WriteAsync(
                    headerBytes,
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
    }
}
