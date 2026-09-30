using System.Net;
using System.Text;
using Hive.Core;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderModelMetadataProviderTests
{
    [Fact]
    public async Task GroqDiscovery_MapsProviderReportedActiveAndContextMetadata()
    {
        var handler = new RecordingHandler(
            """
            {
              "object": "list",
              "data": [
                {
                  "id": "llama-3.1-8b-instant",
                  "object": "model",
                  "created": 1693721698,
                  "owned_by": "Meta",
                  "active": true,
                  "context_window": 131072,
                  "max_completion_tokens": 8192
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            "groq",
            new Uri("https://api.groq.com/openai/v1/"),
            handler);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var model = Assert.Single(result.Value!.Models);
        Assert.Equal(
            ProviderAvailabilityStatus.Available,
            model.Availability);
        Assert.Equal(
            131072,
            model.Limits!.ContextWindowTokens);
        Assert.Equal(
            8192,
            model.Limits.MaxOutputTokens);
        Assert.Empty(model.DiscoveredCapabilities);
        Assert.Equal(
            "/openai/v1/models",
            handler.Request!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CerebrasDiscovery_UsesPublicOpenRouterFormatAndMapsRichMetadata()
    {
        var handler = new RecordingHandler(
            """
            {
              "object": "list",
              "data": [
                {
                  "id": "gpt-oss-120b",
                  "owned_by": "OpenAI",
                  "name": "OpenAI GPT OSS",
                  "description": "Reasoning model.",
                  "input_modalities": ["text"],
                  "output_modalities": ["text"],
                  "context_length": 131072,
                  "max_output_length": 40960,
                  "pricing": {
                    "input": 0.35,
                    "output": 0.75
                  },
                  "capabilities": {
                    "function_calling": true,
                    "structured_outputs": true,
                    "vision": false,
                    "reasoning": true
                  }
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            "cerebras",
            new Uri("https://api.cerebras.ai/v1/"),
            handler);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            "/public/v1/models",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(
            "?format=openrouter",
            handler.Request.RequestUri.Query);

        var model = Assert.Single(result.Value!.Models);
        Assert.Equal("OpenAI GPT OSS", model.DisplayName);
        Assert.Equal("Reasoning model.", model.Description);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("tool.calling")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("structured.output")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("reasoning")).State);
        Assert.Equal(
            131072,
            model.Limits!.ContextWindowTokens);
        Assert.Equal(
            40960,
            model.Limits.MaxOutputTokens);
    }

    [Fact]
    public async Task GeminiDiscovery_UsesNativeModelsApiAndMapsThinkingAndLimits()
    {
        var handler = new RecordingHandler(
            """
            {
              "models": [
                {
                  "name": "models/gemini-3.8-flash",
                  "version": "3.8",
                  "displayName": "Gemini 3.8 Flash",
                  "description": "Fast multimodal model.",
                  "baseModelId": "gemini-3.8-flash",
                  "inputTokenLimit": 1048576,
                  "outputTokenLimit": 65536,
                  "supportedGenerationMethods": [
                    "generateContent",
                    "streamGenerateContent",
                    "countTokens"
                  ],
                  "thinking": true
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            "google-gemini",
            new Uri(
                "https://generativelanguage.googleapis.com/v1beta/openai/"),
            handler,
            credentialText: "gemini-secret");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            "/v1beta/models",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(
            "gemini-secret",
            handler.Request.Headers.GetValues("x-goog-api-key").Single());
        Assert.Null(handler.Request.Headers.Authorization);

        var model = Assert.Single(result.Value!.Models);
        Assert.Equal("gemini-3.8-flash", model.ModelId);
        Assert.Equal("Gemini 3.8 Flash", model.DisplayName);
        Assert.Equal("3.8", model.Version);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("text.generate")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("reasoning")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("thinking")).State);
        Assert.Equal(
            1048576,
            model.Limits!.ContextWindowTokens);
        Assert.Equal(
            65536,
            model.Limits.MaxOutputTokens);
    }

    [Fact]
    public async Task LmStudioDiscovery_UsesNativeModelsApiAndMapsCapabilities()
    {
        var handler = new RecordingHandler(
            """
            {
              "models": [
                {
                  "type": "llm",
                  "publisher": "google",
                  "key": "google/gemma-4-26b-a4b",
                  "display_name": "Gemma 4 26B A4B",
                  "architecture": "gemma4",
                  "max_context_length": 262144,
                  "capabilities": {
                    "vision": true,
                    "trained_for_tool_use": true,
                    "reasoning": {
                      "allowed_options": ["off", "on", "low", "medium", "high"],
                      "default": "medium"
                    }
                  },
                  "description": "Local multimodal model."
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            "lm-studio",
            new Uri("http://localhost:1234/v1/"),
            handler,
            credentialText: "optional-token");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            "/api/v1/models",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(
            "Bearer",
            handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(
            "optional-token",
            handler.Request.Headers.Authorization.Parameter);

        var model = Assert.Single(result.Value!.Models);
        Assert.Equal("Gemma 4 26B A4B", model.DisplayName);
        Assert.Equal("google", model.OwnedBy);
        Assert.Equal("gemma4", model.Family);
        Assert.Equal("llm", model.ModelType);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("vision")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("tool.calling")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("reasoning")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("thinking")).State);
        Assert.Equal(
            ["off", "on", "low", "medium", "high"],
            model.ThinkingOptions);
        Assert.Equal("medium", model.DefaultThinkingLevel);
        Assert.Equal(
            262144,
            model.Limits!.ContextWindowTokens);
    }

    [Fact]
    public async Task OllamaDiscovery_UsesBulkTagsAndDoesNotProbeEveryModel()
    {
        var handler = new RecordingHandler(
            """
            {
              "models": [
                {
                  "name": "llama3.2:latest",
                  "model": "llama3.2:latest",
                  "modified_at": "2026-09-30T01:02:03Z",
                  "size": 123456789,
                  "details": {
                    "format": "gguf",
                    "family": "llama",
                    "parameter_size": "3B",
                    "quantization_level": "Q4_K_M"
                  }
                },
                {
                  "name": "nomic-embed-text:latest",
                  "model": "nomic-embed-text:latest",
                  "details": {
                    "format": "gguf",
                    "family": "nomic-bert",
                    "parameter_size": "137M"
                  }
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            "ollama",
            new Uri("http://localhost:11434/v1/"),
            handler);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            "/api/tags",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(1, handler.CallCount);

        var models = result.Value!.Models;
        Assert.Equal(2, models.Count);
        Assert.Empty(models[0].DiscoveredCapabilities);
        Assert.Equal("llama", models[0].Family);
        Assert.Equal("gguf", models[0].ModelType);
        Assert.NotNull(models[0].ExtensionData);
        Assert.Contains(
            models[0].ExtensionData!.Keys,
            key => key == "size");
    }

    [Fact]
    public async Task CloudflareDiscovery_UsesAccountModelSearchInOpenRouterFormat()
    {
        var handler = new RecordingHandler(
            """
            {
              "success": true,
              "result": [
                {
                  "id": "@cf/meta/llama-3.1-8b-instruct",
                  "name": "Llama 3.1 8B Instruct",
                  "input_modalities": ["text"],
                  "output_modalities": ["text"],
                  "context_length": 131072,
                  "pricing": {
                    "input": 0,
                    "output": 0
                  },
                  "supported_parameters": [
                    "tools",
                    "structured_outputs"
                  ],
                  "architecture": {
                    "input_modalities": ["text"],
                    "output_modalities": ["text"]
                  }
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            "cloudflare",
            new Uri(
                "https://api.cloudflare.com/client/v4/accounts/account-123/ai/v1/"),
            handler,
            credentialText: "cloudflare-token");

        Assert.True(result.IsSuccess, result.Error?.Message);

        Assert.Equal(
            "/client/v4/accounts/account-123/ai/models/search",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(
            "?format=openrouter&per_page=100",
            handler.Request.RequestUri.Query);

        var model = Assert.Single(result.Value!.Models);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("tool.calling")).State);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single(
                capability => capability.Capability == new CapabilityKey("structured.output")).State);
        Assert.True(model.Pricing!.ExplicitFreeEvidence);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("nvidia")]
    public async Task BasicOpenAiCompatibleProviders_DoNotInferCapabilities(
        string providerKey)
    {
        var handler = new RecordingHandler(
            """
            {
              "data": [
                {
                  "id": "basic-model",
                  "created": 1700000000,
                  "owned_by": "provider"
                }
              ]
}
            """);

        var result = await DiscoverAsync(
            providerKey,
            providerKey == "openai"
                ? new Uri("https://api.openai.com/v1/")
                : new Uri("https://integrate.api.nvidia.com/v1/"),
            handler);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var model = Assert.Single(result.Value!.Models);
        Assert.Empty(model.DiscoveredCapabilities);
        Assert.Null(model.Limits);
    }

    private static async Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
        string providerKey,
        Uri endpoint,
        RecordingHandler handler,
        string? credentialText = null)
    {
        using var client = new HttpClient(handler);

        SecretMaterial? credential = null;

        try
        {
            if (credentialText is not null)
                credential = SecretMaterial.Create(credentialText);

            var principal = PrincipalId.New();
            var tenant = TenantId.New();
            var now = DateTimeOffset.UtcNow;

            var provider = new Provider(
                new ResourceEnvelope<ProviderId>(
                    ResourceKind.Provider,
                    ProviderId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                $"provider-{providerKey}",
                providerKey,
                "openai-compatible");

            var account = new ProviderAccount(
                new ResourceEnvelope<ProviderAccountId>(
                    ResourceKind.ProviderAccount,
                    ProviderAccountId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                provider.Id,
                $"account-{providerKey}",
                "Provider account",
                "fixture");

            var target = new ExecutionTarget(
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
                provider.Id,
                account.Id,
                $"target-{providerKey}",
                "Provider target",
                endpoint,
                "fixture-model",
                null,
                Array.Empty<CapabilityStateEntry>());

            var discovery = new OpenAICompatibleProviderCapabilityDiscovery(
                client,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(5));

            return await discovery.DiscoverAsync(
                provider,
                account,
                target,
                credential);
        }
        finally
        {
            credential?.Dispose();
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;

        public RecordingHandler(string body)
        {
            _body = body;
        }

        public HttpRequestMessage? Request { get; private set; }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            Request = request;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        _body,
                        Encoding.UTF8,
                        "application/json")
                });
        }
    }
}
