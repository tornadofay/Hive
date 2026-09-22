using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Hive.Core;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

public sealed class OpenAICompatibleProviderAdapterTests
{
    [Fact]
    public async Task CompleteChatAsync_ReturnsAssistantContent_AndSendsBearerCredential()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Json(
                """{"id":"chatcmpl-test","model":"test-model","choices":[{"message":{"role":"assistant","content":"hello from fake provider"}}]}"""));

        using var credential = SecretMaterial.Create("test-api-key");
        using var client = new HttpClient();
        var adapter = CreateAdapter(server, client, credential);

        var result = await adapter.CompleteChatAsync(
            new OpenAICompatibleChatRequest(
                "test-model",
                [
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.System,
                        "You are a test assistant."),
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.User,
                        "Say hello.")
                ]));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("hello from fake provider", result.Value!.Content);
        Assert.Equal("chatcmpl-test", result.Value.Id);
        Assert.Equal("test-model", result.Value.Model);
        Assert.Contains(
            "\"model\":\"test-model\"",
            server.RequestBody,
            StringComparison.Ordinal);
        Assert.Equal("Bearer", server.AuthorizationScheme);
        Assert.Equal("test-api-key", server.AuthorizationParameter);
    }

    [Fact]
    public async Task CompleteChatAsync_ReturnsSerializationError_ForMalformedResponse()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Json("""{"choices":[]}"""));
        using var client = new HttpClient();

        var result = await CreateAdapter(server, client)
            .CompleteChatAsync(CreateRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Serialization, result.Error!.Category);
    }

    [Fact]
    public async Task CompleteChatAsync_ReturnsTimeout_WhenProviderDoesNotRespond()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Waiting());
        using var client = new HttpClient();

        var result = await CreateAdapter(
                server,
                client,
                timeout: TimeSpan.FromMilliseconds(150))
            .CompleteChatAsync(CreateRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Timeout, result.Error!.Category);
    }

    [Fact]
    public async Task CompleteChatAsync_PropagatesCallerCancellation()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Waiting());
        using var client = new HttpClient();
        var adapter = CreateAdapter(
            server,
            client,
            timeout: TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => adapter.CompleteChatAsync(CreateRequest(), cancellation.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CompleteChatAsync_MapsAuthenticationFailures(
        HttpStatusCode statusCode)
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Json(
                """{"error":{"message":"invalid api key"}}""",
                statusCode));
        using var client = new HttpClient();

        var result = await CreateAdapter(server, client)
            .CompleteChatAsync(CreateRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Unauthorized, result.Error!.Category);
    }

    [Fact]
    public async Task CompleteChatAsync_MapsRateLimit()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Json(
                """{"error":{"message":"slow down"}}""",
                HttpStatusCode.TooManyRequests));
        using var client = new HttpClient();

        var result = await CreateAdapter(server, client)
            .CompleteChatAsync(CreateRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.provider.openai-compatible.rate-limit",
            result.Error!.Code);
    }

    [Fact]
    public async Task CompleteChatAsync_MapsTransportFailure_WithoutLeakingCredential()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.AbruptClose());
        using var credential = SecretMaterial.Create("secret-key");
        using var client = new HttpClient();

        var adapter = CreateAdapter(
            server,
            client,
            credential,
            TimeSpan.FromSeconds(2));

        var result = await adapter.CompleteChatAsync(CreateRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.External, result.Error!.Category);
        Assert.Equal(
            "hive.provider.openai-compatible.transport-failed",
            result.Error.Code);
        Assert.DoesNotContain(
            "secret-key",
            result.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteChatAsync_ParsesStructuredOutput()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Json(
                """{"id":"chatcmpl-structured","model":"structured-model","choices":[{"message":{"role":"assistant","content":"{\"name\":\"Alice\",\"amount\":42}"}}]}"""));
        using var client = new HttpClient();
        using var schemaDocument = System.Text.Json.JsonDocument.Parse(
            """{"type":"object","properties":{"name":{"type":"string"}}}""");

        var result = await CreateAdapter(server, client).CompleteChatAsync(
            new OpenAICompatibleChatRequest(
                "structured-model",
                [
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.User,
                        "Return structured data.")
                ],
                new OpenAICompatibleStructuredOutput(
                    "person",
                    schemaDocument.RootElement)));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            JsonValueKind.Object,
            result.Value!.StructuredContent!.Value.ValueKind);
        Assert.Equal(
            "Alice",
            result.Value.StructuredContent.Value
                .GetProperty("name")
                .GetString());
        Assert.Contains("\"response_format\":{", server.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"person\"", server.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteChatAsync_RejectsMalformedStructuredOutput()
    {
        await using var server = new LocalFakeHttpServer(
            _ => LocalFakeHttpResponse.Json(
                """{"choices":[{"message":{"role":"assistant","content":"not json"}}]}"""));
        using var client = new HttpClient();
        using var schemaDocument = System.Text.Json.JsonDocument.Parse(
            """{"type":"object"}""");

        var result = await CreateAdapter(server, client).CompleteChatAsync(
            new OpenAICompatibleChatRequest(
                "structured-model",
                [
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.User,
                        "Return JSON.")
                ],
                new OpenAICompatibleStructuredOutput(
                    "person",
                    schemaDocument.RootElement)));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Serialization, result.Error!.Category);
    }

    [Fact]
    public void Contracts_RejectInvalidModelAndMessages()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenAICompatibleChatRequest(
                " ",
                [new OpenAICompatibleMessage(
                    OpenAICompatibleMessageRole.User,
                    "test")]));

        Assert.Throws<ArgumentException>(
            () => new OpenAICompatibleChatRequest(
                "model",
                []));

        Assert.Throws<ArgumentException>(
            () => new OpenAICompatibleMessage(
                OpenAICompatibleMessageRole.User,
                new string('x', 64 * 1024 + 1)));
    }

    [Fact]
    public void Options_RejectInvalidEndpointAndTimeout()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenAICompatibleProviderOptions(
                new Uri("ftp://example.test/v1/")));

        Assert.Throws<ArgumentException>(
            () => new OpenAICompatibleProviderOptions(
                new Uri("https://user:password@example.test/v1/")));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/"),
                timeout: TimeSpan.Zero));
    }

    [Fact]
    public void StructuredOutput_RequiresObjectSchema()
    {
        using var document = System.Text.Json.JsonDocument.Parse("[]");

        Assert.Throws<ArgumentException>(
            () => new OpenAICompatibleStructuredOutput(
                "test",
                document.RootElement));
    }

    private static OpenAICompatibleChatRequest CreateRequest() =>
        new(
            "test-model",
            [
                new OpenAICompatibleMessage(
                    OpenAICompatibleMessageRole.User,
                    "test")
            ]);

    private static OpenAICompatibleProviderAdapter CreateAdapter(
        LocalFakeHttpServer server,
        HttpClient client,
        SecretMaterial? credential = null,
        TimeSpan? timeout = null) =>
        new(
            client,
            new OpenAICompatibleProviderOptions(
                server.BaseUri,
                credential,
                timeout ?? TimeSpan.FromSeconds(2)));

    private sealed class LocalFakeHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Func<LocalFakeHttpRequest, LocalFakeHttpResponse> _handler;
        private readonly Task _serverTask;

        public LocalFakeHttpServer(
            Func<LocalFakeHttpRequest, LocalFakeHttpResponse> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = new Uri($"http://127.0.0.1:{port}/v1/");
            _serverTask = RunAsync();
        }

        public Uri BaseUri { get; }

        public string RequestBody { get; private set; } = string.Empty;

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

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

                RequestBody = request.Body;
                AuthorizationScheme = request.AuthorizationScheme;
                AuthorizationParameter = request.AuthorizationParameter;

                var response = _handler(request);

                if (response.CloseConnection)
                    return;

                if (response.WaitForCancellation)
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        _stop.Token).ConfigureAwait(false);
                }

                var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
                var headerBytes = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n" +
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

        private static async Task<LocalFakeHttpRequest> ReadRequestAsync(
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
                        "Client closed before headers.");

                memory.Write(buffer, 0, read);
                headerEnd = FindHeaderEnd(
                    memory.GetBuffer(),
                    checked((int)memory.Length));
            }

            var allBytes = memory.ToArray();
            var headerText = Encoding.ASCII.GetString(
                allBytes,
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
                    throw new InvalidOperationException(
                        "Client closed before body.");

                memory.Write(buffer, 0, read);
            }

            var requestBytes = memory.ToArray();
            var body = Encoding.UTF8.GetString(
                requestBytes,
                headerEnd + 4,
                contentLength);

            string? authorizationScheme = null;
            string? authorizationParameter = null;

            foreach (var line in headerText.Split(
                         "\r\n",
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith(
                        "Authorization:",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = line["Authorization:".Length..].Trim();
                var separator = value.IndexOf(' ');

                if (separator > 0)
                {
                    authorizationScheme = value[..separator];
                    authorizationParameter = value[(separator + 1)..];
                }
            }

            return new LocalFakeHttpRequest(
                body,
                authorizationScheme,
                authorizationParameter);
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

    private sealed record LocalFakeHttpRequest(
        string Body,
        string? AuthorizationScheme,
        string? AuthorizationParameter);

    private sealed record LocalFakeHttpResponse(
        HttpStatusCode StatusCode,
        string Body,
        bool WaitForCancellation,
        bool CloseConnection)
    {
        public static LocalFakeHttpResponse Json(
            string body,
            HttpStatusCode statusCode = HttpStatusCode.OK) =>
            new(statusCode, body, false, false);

        public static LocalFakeHttpResponse Waiting() =>
            new(HttpStatusCode.OK, string.Empty, true, false);

        public static LocalFakeHttpResponse AbruptClose() =>
            new(HttpStatusCode.OK, string.Empty, false, true);
    }
}
