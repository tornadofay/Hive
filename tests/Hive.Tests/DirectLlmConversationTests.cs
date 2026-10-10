using System.Net;
using System.Text;
using System.Text.Json;
using Hive.Core;
using Hive.Coordination;
using Hive.Management;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class DirectLlmConversationTests
{
    [Fact]
    public async Task SendDirectLlmMessageAsync_PinsTargetPersistsHistoryAndEnforcesOwnerAfterReopen()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "Hive.Tests",
            "DirectLlm",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "hive.db");
        var configuration = HivePersistenceConfiguration.Embedded(path);
        EmbeddedPersistenceDatabase? database = null;
        HiveManagementFacade? management = null;
        HttpClient? httpClient = null;

        try
        {
            database = new EmbeddedPersistenceDatabase(configuration);
            var initialized = await database.InitializeAsync();
            Assert.True(initialized.IsSuccess, initialized.Error?.Message);

            var now = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
            var deployment = DeploymentId.New();
            var tenant = TenantId.New();
            var principal = PrincipalId.New();
            var accessContext = new ResourceAccessContext(deployment, tenant, principal);

            var handler = new RecordingChatHandler(
                """
                {"id":"chatcmpl-direct-1","model":"provider-resolved-model","choices":[{"index":0,"message":{"role":"assistant","content":"Hello from direct LLM."},"finish_reason":"stop"}],"usage":{"prompt_tokens":4,"completion_tokens":5,"total_tokens":9}}
                """);
            httpClient = new HttpClient(handler);
            management = CreateManagement(database, httpClient);

            var provider = CreateProvider(principal, tenant, now);
            var providerResult = await management.CreateProviderAsync(provider, accessContext);
            Assert.True(providerResult.IsSuccess, providerResult.Error?.Message);

            var firstAccount = CreateAccount(
                provider.Id,
                principal,
                tenant,
                now,
                "direct-account-one");
            var firstAccountResult = await management.CreateProviderAccountAsync(
                firstAccount,
                accessContext);
            Assert.True(firstAccountResult.IsSuccess, firstAccountResult.Error?.Message);

            var secondAccount = CreateAccount(
                provider.Id,
                principal,
                tenant,
                now,
                "direct-account-two");
            var secondAccountResult = await management.CreateProviderAccountAsync(
                secondAccount,
                accessContext);
            Assert.True(secondAccountResult.IsSuccess, secondAccountResult.Error?.Message);

            var firstTarget = CreateTarget(
                provider.Id,
                firstAccount.Id,
                principal,
                tenant,
                now,
                "first",
                "model-must-not-be-used");
            var firstTargetResult = await management.CreateExecutionTargetAsync(
                firstTarget,
                accessContext);
            Assert.True(firstTargetResult.IsSuccess, firstTargetResult.Error?.Message);

            var chosenTarget = CreateTarget(
                provider.Id,
                secondAccount.Id,
                principal,
                tenant,
                now,
                "chosen",
                "chosen-direct-model");
            var chosenTargetResult = await management.CreateExecutionTargetAsync(
                chosenTarget,
                accessContext);
            Assert.True(chosenTargetResult.IsSuccess, chosenTargetResult.Error?.Message);

            var createdConversation = await management.CreateDirectLlmConversationAsync(
                accessContext);
            Assert.True(createdConversation.IsSuccess, createdConversation.Error?.Message);
            var conversationId = createdConversation.Value!.Id;

            var sent = await management.SendDirectLlmMessageAsync(
                conversationId,
                chosenTarget.Id,
                "Say hello.",
                accessContext);

            Assert.True(sent.IsSuccess, sent.Error?.Message);
            Assert.Equal(DirectLlmConversationStatus.Completed, sent.Value!.Summary.Status);
            Assert.Equal(chosenTarget.Id, sent.Value.Summary.LastExecutionTargetId!.Value);
            Assert.Equal(2, sent.Value.Summary.MessageCount);
            Assert.Collection(
                sent.Value.Messages,
                message =>
                {
                    Assert.Equal(DirectLlmConversationMessageRole.User, message.Role);
                    Assert.Equal("Say hello.", message.Content);
                    Assert.Equal(chosenTarget.Id, message.ExecutionTargetId!.Value);
                },
                message =>
                {
                    Assert.Equal(DirectLlmConversationMessageRole.Assistant, message.Role);
                    Assert.Equal("Hello from direct LLM.", message.Content);
                    Assert.Equal("provider-resolved-model", message.ProviderReportedModelId);
                });

            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(
                "chosen-direct-model",
                handler.LastRequestModel);
            Assert.True(
                handler.LastRequestUri!.AbsolutePath.EndsWith(
                    "/chat/completions",
                    StringComparison.OrdinalIgnoreCase));

            var summaries = await management.ListDirectLlmConversationsAsync(accessContext);
            Assert.True(summaries.IsSuccess, summaries.Error?.Message);
            Assert.Single(summaries.Value!);
            Assert.Equal(conversationId, summaries.Value![0].Id);

            management.Dispose();
            management = null;
            httpClient.Dispose();
            httpClient = null;
            await database.DisposeAsync();
            database = null;

            // Reopen the same file through a fresh persistence object and Management graph.
            database = new EmbeddedPersistenceDatabase(configuration);
            var reopened = await database.InitializeAsync();
            Assert.True(reopened.IsSuccess, reopened.Error?.Message);

            httpClient = new HttpClient(new RecordingChatHandler(
                """{"id":"unused","model":"unused","choices":[{"message":{"role":"assistant","content":"unused"}}]}"""));
            management = CreateManagement(database, httpClient);

            var restored = await management.GetDirectLlmConversationAsync(
                conversationId,
                accessContext);
            Assert.True(restored.IsSuccess, restored.Error?.Message);
            Assert.Equal("Say hello.", restored.Value!.Messages[0].Content);
            Assert.Equal("Hello from direct LLM.", restored.Value.Messages[1].Content);
            Assert.Equal(DirectLlmConversationStatus.Completed, restored.Value.Summary.Status);

            var foreignContext = new ResourceAccessContext(
                deployment,
                tenant,
                PrincipalId.New());
            var foreignRead = await management.GetDirectLlmConversationAsync(
                conversationId,
                foreignContext);

            Assert.True(foreignRead.IsFailure);
            Assert.Equal(ErrorCategory.NotFound, foreignRead.Error!.Category);
            Assert.Equal(
                "hive.direct-llm.conversation-not-found",
                foreignRead.Error.Code);
        }
        finally
        {
            management?.Dispose();
            httpClient?.Dispose();

            if (database is not null)
                await database.DisposeAsync();

            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Preserve the test's primary result; temporary files are outside the repo.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the test's primary result; temporary files are outside the repo.
            }
        }
    }

    [Fact]
    public async Task SendDirectLlmMessageAsync_RejectsMissingTargetBeforePersistingAMessage()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "Hive.Tests",
            "DirectLlm",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "hive.db");
        var configuration = HivePersistenceConfiguration.Embedded(path);
        EmbeddedPersistenceDatabase? database = null;
        HiveManagementFacade? management = null;
        HttpClient? httpClient = null;

        try
        {
            database = new EmbeddedPersistenceDatabase(configuration);
            var initialized = await database.InitializeAsync();
            Assert.True(initialized.IsSuccess, initialized.Error?.Message);

            var context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());
            var handler = new RecordingChatHandler("{}");
            httpClient = new HttpClient(handler);
            management = CreateManagement(database, httpClient);

            var conversation = await management.CreateDirectLlmConversationAsync(context);
            Assert.True(conversation.IsSuccess, conversation.Error?.Message);

            var result = await management.SendDirectLlmMessageAsync(
                conversation.Value!.Id,
                default,
                "This must not be sent.",
                context);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCategory.Validation, result.Error!.Category);
            Assert.Equal("hive.direct-llm.execution-target-required", result.Error.Code);
            Assert.Equal(0, handler.RequestCount);

            var history = await management.GetDirectLlmConversationAsync(
                conversation.Value.Id,
                context);
            Assert.True(history.IsSuccess, history.Error?.Message);
            Assert.Empty(history.Value!.Messages);
            Assert.Equal(DirectLlmConversationStatus.Ready, history.Value.Summary.Status);
        }
        finally
        {
            management?.Dispose();
            httpClient?.Dispose();

            if (database is not null)
                await database.DisposeAsync();

            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static HiveManagementFacade CreateManagement(
        EmbeddedPersistenceDatabase database,
        HttpClient httpClient)
    {
        var eventPersistence = HiveEventPersistence.CreateEmbedded(database);
        return new HiveManagementFacade(
            new EmbeddedProviderResourceStore(database),
            new EmbeddedAgentDefinitionResourceStore(database),
            new EmbeddedWorkItemResourceStore(database),
            directLlmCompletion: new DirectLlmCompletionService(httpClient),
            eventPersistence: eventPersistence);
    }

    private static Provider CreateProvider(
        PrincipalId principal,
        TenantId tenant,
        DateTimeOffset now) =>
        new(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(principal, now),
                ResourceLifecycle.Active(now)),
            $"direct-test-{Guid.NewGuid():N}",
            "Direct LLM Test Provider",
            "openai-compatible");

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        PrincipalId principal,
        TenantId tenant,
        DateTimeOffset now,
        string key) =>
        new(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(principal, now),
                ResourceLifecycle.Active(now)),
            providerId,
            key,
            key);

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        PrincipalId principal,
        TenantId tenant,
        DateTimeOffset now,
        string suffix,
        string model) =>
        new(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                Provenance(principal, now),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            $"direct-target-{suffix}",
            $"Direct Target {suffix}",
            new Uri("https://llm.example.test/v1/"),
            model,
            deployment: null,
            [
                new CapabilityStateEntry(
                    HiveCapabilityKeys.TextGeneration,
                    CapabilityState.Supported)
            ]);

    private static ResourceProvenance Provenance(
        PrincipalId principal,
        DateTimeOffset now) =>
        new(principal, now, CorrelationId.New());

    private sealed class RecordingChatHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private int _requestCount;

        public RecordingChatHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public Uri? LastRequestUri { get; private set; }

        public string? LastRequestModel { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            LastRequestUri = request.RequestUri;

            if (request.Content is not null)
            {
                var json = await request.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("model", out var model))
                    LastRequestModel = model.GetString();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseJson,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

}
